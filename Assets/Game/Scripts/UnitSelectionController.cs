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
    private const float InfoLineHeight = 20f;
    private const float HealthUnitScreenSelectRadius = 36f;
    private const float ScreenPadding = 24f;

    [SerializeField] private Camera targetCamera;
    [SerializeField] private Canvas hudCanvas;

    private TextMeshProUGUI infoLabel;
    private RectTransform infoPanelRect;
    private GameObject infoPanelObject;
    private SelectableUnit selectedUnit;
    private Health selectedHealth;

    private void Start()
    {
        if (targetCamera == null)
            targetCamera = GetComponentInChildren<Camera>();
        if (targetCamera == null)
            targetCamera = Camera.main;
        if (hudCanvas == null)
            hudCanvas = FindFirstObjectByType<Canvas>();

        CreateInfoPanel();
        SetSelectedUnit(null, null);
    }

    private void Update()
    {
        if (selectedHealth != null && (selectedHealth.IsDead || !selectedHealth.gameObject.activeInHierarchy))
            SetSelectedUnit(null, null);
        else if (selectedUnit == null && selectedHealth == null && infoPanelObject != null && infoPanelObject.activeSelf)
            SetSelectedUnit(null, null);

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
            Health health = hit.collider.GetComponentInParent<Health>();
            if (unit == null && health == null)
                continue;

            Debug.Log($"Unit selection physics hit: collider='{hit.collider.gameObject.name}', selected unit='{(unit != null ? unit.name : health.name)}'.", this);
            SetSelectedUnit(unit, health);
            return;
        }

        Health screenSelectedHealth = FindHealthUnitAtScreenPosition(Input.mousePosition);
        if (screenSelectedHealth != null)
        {
            SetSelectedUnit(screenSelectedHealth.GetComponentInParent<SelectableUnit>(), screenSelectedHealth);
            return;
        }

        string firstPhysicsHit = hits.Length > 0 ? hits[0].collider.gameObject.name : "none";
        Debug.Log($"Unit selection found no unit; first physics hit='{firstPhysicsHit}'.", this);
        SetSelectedUnit(null, null);
    }

    private Health FindHealthUnitAtScreenPosition(Vector2 screenPosition)
    {
        Health nearestHealth = null;
        float nearestDistanceSquared = HealthUnitScreenSelectRadius * HealthUnitScreenSelectRadius;
        Health[] healthComponents = FindObjectsByType<Health>(FindObjectsSortMode.None);
        foreach (Health health in healthComponents)
        {
            if (health == null || health.IsDead || !health.gameObject.activeInHierarchy)
                continue;

            Vector3 projectedPosition = targetCamera.WorldToScreenPoint(health.transform.position);
            if (projectedPosition.z <= 0f)
                continue;

            float distanceSquared = ((Vector2)projectedPosition - screenPosition).sqrMagnitude;
            if (distanceSquared >= nearestDistanceSquared)
                continue;

            nearestDistanceSquared = distanceSquared;
            nearestHealth = health;
        }

        return nearestHealth;
    }

    private void LateUpdate()
    {
        if (selectedUnit == null && selectedHealth == null)
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
        infoPanelRect.pivot = new Vector2(0.5f, 1f);
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
        infoLabel.textWrappingMode = TextWrappingModes.Normal;
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

        Transform selectedTransform = selectedHealth != null ? selectedHealth.transform : selectedUnit.transform;
        Vector3 screenPosition = targetCamera.WorldToScreenPoint(selectedTransform.position);
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

        string panelText = BuildInfoText(out string unitType, out float attack, out float panelHeight);
        infoPanelRect.sizeDelta = new Vector2(PanelWidth, panelHeight);
        localPosition.y += PanelHeight + ScreenPadding;
        Rect canvasBounds = canvasRect.rect;
        float halfPanelWidth = PanelWidth * 0.5f;
        localPosition.x = Mathf.Clamp(localPosition.x, canvasBounds.xMin + halfPanelWidth + ScreenPadding, canvasBounds.xMax - halfPanelWidth - ScreenPadding);
        localPosition.y = Mathf.Clamp(localPosition.y, canvasBounds.yMin + panelHeight + ScreenPadding, canvasBounds.yMax - ScreenPadding);

        infoPanelRect.anchoredPosition = localPosition;
        if (!infoPanelObject.activeSelf)
            infoPanelObject.SetActive(true);
        infoLabel.text = panelText;
    }

    private string BuildInfoText(out string unitType, out float attack, out float panelHeight)
    {
        DifficultyStats stats = DifficultySettings.CurrentStats;
        bool hasHealth = selectedHealth != null;
        bool isPathogen = hasHealth && selectedHealth.IsPathogen;
        string displayName = selectedUnit != null
            ? selectedUnit.DisplayName
            : (isPathogen ? $"{selectedHealth.PathogenType} pathogen" : selectedHealth.name);
        unitType = isPathogen ? selectedHealth.PathogenType.ToString() : displayName;

        float currentHp = hasHealth ? selectedHealth.CurrentHp : (selectedUnit != null ? selectedUnit.CurrentHitPoints : 0f);
        float maxHp = hasHealth ? selectedHealth.MaxHp : currentHp;
        List<string> lines = new List<string> { displayName };
        if (isPathogen)
            lines.Add($"Type: {selectedHealth.PathogenType}");
        lines.Add($"HP: {currentHp:0.##} / {maxHp:0.##}");

        bool isWbc = !isPathogen && string.Equals(displayName, "WBC", System.StringComparison.OrdinalIgnoreCase);
        if (isPathogen)
            attack = selectedHealth.PathogenType == InfectionPathogenType.Viral ? stats.virusAttack : stats.bacteriaAttack;
        else
            attack = isWbc ? stats.wbcAttack : 0f;
        lines.Add($"Attack: {attack:0.##}");

        if (isWbc)
        {
            float damageVsBacteria = stats.wbcAttack * stats.wbcDamageMultiplierVsBacteria;
            float damageVsVirus = stats.wbcAttack * stats.wbcDamageMultiplierVsVirus;
            lines.Add($"Dmg vs bacteria: {damageVsBacteria:0.##}, vs virus: {damageVsVirus:0.##}");
        }

        panelHeight = Mathf.Max(PanelHeight, (lines.Count + (isWbc ? 1 : 0)) * InfoLineHeight + 16f);
        return string.Join("\n", lines);
    }

    private void SetSelectedUnit(SelectableUnit unit, Health health)
    {
        selectedUnit = unit;
        selectedHealth = health != null ? health : (unit != null ? unit.GetComponent<Health>() : null);
        if (infoLabel == null || infoPanelObject == null)
            return;

        if (selectedUnit == null && selectedHealth == null)
        {
            infoPanelObject.SetActive(false);
            return;
        }

        string panelText = BuildInfoText(out string unitType, out float attack, out _);
        float currentHp = selectedHealth != null ? selectedHealth.CurrentHp : selectedUnit.CurrentHitPoints;
        float maxHp = selectedHealth != null ? selectedHealth.MaxHp : currentHp;
        Debug.Log($"Unit selected: type={unitType}, HP={currentHp:0.##}/{maxHp:0.##}, Attack={attack:0.##}.", this);
        infoLabel.text = panelText;
        UpdateInfoPanelPosition();
    }
}
