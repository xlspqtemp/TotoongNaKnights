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
        if (selectedUnit == null && infoLabel != null && infoLabel.transform.parent.gameObject.activeSelf)
            SetSelectedUnit(null);

        if (Time.timeScale <= 0f || !Input.GetMouseButtonDown(0))
            return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;
        if (targetCamera == null)
            return;

        Ray selectionRay = targetCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(selectionRay, targetCamera.farClipPlane - targetCamera.nearClipPlane);
        System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

        foreach (RaycastHit hit in hits)
        {
            SelectableUnit unit = hit.collider.GetComponentInParent<SelectableUnit>();
            if (unit == null)
                continue;

            SetSelectedUnit(unit);
            return;
        }

        SetSelectedUnit(null);
    }

    private void CreateInfoPanel()
    {
        if (hudCanvas == null)
        {
            Debug.LogWarning("UnitSelectionController could not find a HUD Canvas for the unit info panel.", this);
            enabled = false;
            return;
        }

        GameObject panelObject = new GameObject("SelectedUnitInfoPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
        panelObject.transform.SetParent(hudCanvas.transform, false);
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 0f);
        panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot = new Vector2(1f, 0f);
        panelRect.anchoredPosition = new Vector2(-ScreenPadding, ScreenPadding);
        panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = PanelColor;
        panelImage.raycastTarget = false;

        Outline outline = panelObject.GetComponent<Outline>();
        outline.effectColor = AccentColor;
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        outline.useGraphicAlpha = true;

        GameObject labelObject = new GameObject("SelectedUnitInfoLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(panelObject.transform, false);
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
        panelObject.SetActive(false);
    }

    private void SetSelectedUnit(SelectableUnit unit)
    {
        selectedUnit = unit;
        if (infoLabel == null)
            return;

        bool hasSelection = selectedUnit != null;
        infoLabel.transform.parent.gameObject.SetActive(hasSelection);
        if (hasSelection)
            infoLabel.text = $"{selectedUnit.DisplayName}\nHP: {selectedUnit.CurrentHitPoints}";
    }
}
