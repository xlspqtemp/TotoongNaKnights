using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Handles world-click selection of deployed units and displays their name and current HP.</summary>
public sealed class UnitSelectionController : MonoBehaviour
{
    private static readonly Color PanelColor = new Color(0.02f, 0.055f, 0.075f, 0.94f);
    private static readonly Color AccentColor = new Color(0.1f, 0.75f, 0.88f, 1f);
    private const float PanelWidth = 210f;
    private const float PanelHeight = 64f;
    private const float ScreenPadding = 24f;

    [SerializeField] private Camera targetCamera;
    [SerializeField] private Canvas hudCanvas;

    private TextMeshProUGUI infoLabel;
    private RectTransform infoPanelRect;
    private GameObject infoPanelObject;
    private SelectableUnit selectedUnit;

    private void Start()
    {
        if (targetCamera == null)
            targetCamera = GetComponentInChildren<Camera>();
        if (targetCamera == null)
            targetCamera = Camera.main;
        if (hudCanvas == null)
            hudCanvas = FindFirstObjectByType<Canvas>();

        CreateInfoPanel();
        SetSelectedUnit(null);
    }

    private void Update()
    {
        if (selectedUnit == null && infoPanelObject != null && infoPanelObject.activeSelf)
            SetSelectedUnit(null);

        if (!Input.GetMouseButtonDown(0))
            return;

        EventSystem eventSystem = EventSystem.current;
        GameObject selectedUIObject = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
        bool pointerOnInteractiveUI = TryGetInteractiveUIHit(Input.mousePosition, out string uiHitName, out string clickHandlerName);
        Debug.Log(
            $"Unit selection click at {Input.mousePosition}: UI hit='{uiHitName}', click handler='{clickHandlerName}', current UI selection='{(selectedUIObject != null ? selectedUIObject.name : "none")}'; interactive UI={(pointerOnInteractiveUI ? "yes" : "no")}.",
            this);

        if (Time.timeScale <= 0f || pointerOnInteractiveUI || targetCamera == null)
            return;

        Ray selectionRay = targetCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(selectionRay, targetCamera.farClipPlane - targetCamera.nearClipPlane);
        System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

        foreach (RaycastHit hit in hits)
        {
            SelectableUnit unit = hit.collider.GetComponentInParent<SelectableUnit>();
            if (unit == null)
                continue;

            Debug.Log($"Unit selection physics hit: collider='{hit.collider.gameObject.name}', selected unit='{unit.name}'.", this);
            SetSelectedUnit(unit);
            return;
        }

        string firstPhysicsHit = hits.Length > 0 ? hits[0].collider.gameObject.name : "none";
        Debug.Log($"Unit selection found no unit; first physics hit='{firstPhysicsHit}'.", this);
        SetSelectedUnit(null);
    }

    private void LateUpdate()
    {
        if (selectedUnit == null)
        {
            if (infoPanelObject != null && infoPanelObject.activeSelf)
                infoPanelObject.SetActive(false);
            return;
        }

        UpdateInfoPanelPosition();
    }

    private void CreateInfoPanel()
    {
        if (hudCanvas == null)
        {
            Debug.LogWarning("UnitSelectionController could not find a HUD Canvas for the unit info panel.", this);
            enabled = false;
            return;
        }

        infoPanelObject = new GameObject("SelectedUnitInfoPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
        infoPanelObject.transform.SetParent(hudCanvas.transform, false);
        infoPanelRect = infoPanelObject.GetComponent<RectTransform>();
        infoPanelRect.anchorMin = new Vector2(0.5f, 0.5f);
        infoPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
        infoPanelRect.pivot = new Vector2(0.5f, 0.5f);
        infoPanelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);

        Image panelImage = infoPanelObject.GetComponent<Image>();
        panelImage.color = PanelColor;
        panelImage.raycastTarget = false;

        Outline outline = infoPanelObject.GetComponent<Outline>();
        outline.effectColor = AccentColor;
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        outline.useGraphicAlpha = true;

        GameObject labelObject = new GameObject("SelectedUnitInfoLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(infoPanelObject.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(12f, 8f);
        labelRect.offsetMax = new Vector2(-12f, -8f);

        infoLabel = labelObject.GetComponent<TextMeshProUGUI>();
        infoLabel.font = TMP_Settings.defaultFontAsset;
        infoLabel.fontSize = 16f;
        infoLabel.fontStyle = FontStyles.Bold;
        infoLabel.alignment = TextAlignmentOptions.MidlineLeft;
        infoLabel.color = AccentColor;
        infoLabel.textWrappingMode = TextWrappingModes.NoWrap;
        infoLabel.raycastTarget = false;
        infoPanelObject.SetActive(false);
    }

    private bool TryGetInteractiveUIHit(Vector2 screenPosition, out string uiHitName, out string clickHandlerName)
    {
        uiHitName = "none";
        clickHandlerName = "none";

        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return false;

        PointerEventData pointerData = new PointerEventData(eventSystem)
        {
            position = screenPosition
        };
        List<RaycastResult> raycastResults = new List<RaycastResult>();
        eventSystem.RaycastAll(pointerData, raycastResults);

        foreach (RaycastResult result in raycastResults)
        {
            if (!(result.module is GraphicRaycaster))
                continue;

            uiHitName = result.gameObject.name;
            GameObject clickHandler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(result.gameObject);
            if (clickHandler == null)
                return false;

            clickHandlerName = clickHandler.name;
            return true;
        }

        return false;
    }

    private void UpdateInfoPanelPosition()
    {
        if (infoPanelRect == null || infoPanelObject == null || targetCamera == null || hudCanvas == null)
        {
            if (infoPanelObject != null)
                infoPanelObject.SetActive(false);
            return;
        }

        Vector3 screenPosition = targetCamera.WorldToScreenPoint(selectedUnit.transform.position);
        bool isInFrontOfCamera = screenPosition.z > 0f;
        bool isOnScreen = screenPosition.x >= 0f && screenPosition.x <= Screen.width
            && screenPosition.y >= 0f && screenPosition.y <= Screen.height;
        if (!isInFrontOfCamera || !isOnScreen)
        {
            infoPanelObject.SetActive(false);
            return;
        }

        Camera canvasCamera = hudCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : (hudCanvas.worldCamera != null ? hudCanvas.worldCamera : targetCamera);
        RectTransform canvasRect = hudCanvas.transform as RectTransform;
        if (canvasRect == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, canvasCamera, out Vector2 localPosition))
        {
            infoPanelObject.SetActive(false);
            return;
        }

        localPosition.y += PanelHeight * 0.5f + ScreenPadding;
        Rect canvasBounds = canvasRect.rect;
        float halfPanelWidth = PanelWidth * 0.5f;
        float halfPanelHeight = PanelHeight * 0.5f;
        localPosition.x = Mathf.Clamp(localPosition.x, canvasBounds.xMin + halfPanelWidth + ScreenPadding, canvasBounds.xMax - halfPanelWidth - ScreenPadding);
        localPosition.y = Mathf.Clamp(localPosition.y, canvasBounds.yMin + halfPanelHeight + ScreenPadding, canvasBounds.yMax - halfPanelHeight - ScreenPadding);

        infoPanelRect.anchoredPosition = localPosition;
        if (!infoPanelObject.activeSelf)
            infoPanelObject.SetActive(true);
        infoLabel.text = $"{selectedUnit.DisplayName}\nHP: {selectedUnit.CurrentHitPoints}";
    }

    private void SetSelectedUnit(SelectableUnit unit)
    {
        selectedUnit = unit;
        if (infoLabel == null || infoPanelObject == null)
            return;

        if (selectedUnit == null)
        {
            infoPanelObject.SetActive(false);
            return;
        }

        infoLabel.text = $"{selectedUnit.DisplayName}\nHP: {selectedUnit.CurrentHitPoints}";
        UpdateInfoPanelPosition();
    }
}
