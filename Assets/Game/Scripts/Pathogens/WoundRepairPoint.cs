using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shows a floating skin-breach prompt at one randomly selected wound point.</summary>
public sealed class WoundRepairPoint : MonoBehaviour
{
    private const string NickedOrScrapedEventName = "Nicked by a sharp object or tripped and scraped knees";
    private const float RepairDurationSeconds = 60f;
    private const float FloatingHeight = 3f;
    private const float CanvasWorldScale = 0.02f;
    private const float CanvasPixelsPerUnit = 10f;
    private const float StatusFontSize = 50f;
    private const float ButtonFontSize = 44f;

    private static readonly Vector2 CanvasSize = new Vector2(600f, 260f);
    private static readonly Vector2 StatusSize = new Vector2(560f, 100f);
    private static readonly Vector2 StatusPosition = new Vector2(0f, 68f);
    private static readonly Vector2 ButtonSize = new Vector2(340f, 88f);
    private static readonly Vector2 ButtonPosition = new Vector2(0f, -60f);
    private static readonly Vector2 ButtonLabelSize = new Vector2(320f, 80f);
    private static readonly Vector2 CenterAnchor = new Vector2(0.5f, 0.5f);

    private static readonly List<WoundRepairPoint> ActivePoints = new List<WoundRepairPoint>();
    private static bool isListeningForEvents;

    /// <summary>Raised when the count of active skin breaches changes.</summary>
    public static event System.Action<bool> OnSkinBreachStateChanged;

    /// <summary>Returns whether any enabled wound point currently has an unrepaired breach.</summary>
    public static bool HasActiveBreaches
    {
        get
        {
            foreach (WoundRepairPoint woundPoint in ActivePoints)
            {
                if (woundPoint != null && woundPoint.isActiveAndEnabled && woundPoint.isBreached)
                {
                    return true;
                }
            }

            return false;
        }
    }

    private Canvas floatingCanvas;
    private TextMeshProUGUI statusLabel;
    private TextMeshProUGUI repairButtonLabel;
    private Button repairButton;
    private bool isBreached;
    private bool isRepairing;

    private void OnEnable()
    {
        if (!ActivePoints.Contains(this))
        {
            ActivePoints.Add(this);
        }

        if (!isListeningForEvents)
        {
            RandomEventSystem.OnRandomEventTriggered += HandleRandomEventTriggered;
            isListeningForEvents = true;
        }

        if (isBreached)
        {
            NotifySkinBreachStateChanged();
        }
    }

    private void Awake()
    {
        CreateFloatingPrompt();
    }

    private void OnDisable()
    {
        bool hadBreach = isBreached;
        ActivePoints.Remove(this);
        if (ActivePoints.Count == 0 && isListeningForEvents)
        {
            RandomEventSystem.OnRandomEventTriggered -= HandleRandomEventTriggered;
            isListeningForEvents = false;
        }

        if (hadBreach)
        {
            NotifySkinBreachStateChanged();
        }
    }

    private void LateUpdate()
    {
        if (floatingCanvas == null)
        {
            return;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            floatingCanvas.worldCamera = mainCamera;
            floatingCanvas.transform.rotation = Quaternion.LookRotation(mainCamera.transform.position - floatingCanvas.transform.position);
        }
    }

    private void HandleRandomEventTriggered(RandomEventData eventData)
    {
        if (eventData == null || !string.Equals(eventData.eventName, NickedOrScrapedEventName, System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        List<WoundRepairPoint> availablePoints = new List<WoundRepairPoint>();
        foreach (WoundRepairPoint woundPoint in ActivePoints)
        {
            if (woundPoint != null && woundPoint.isActiveAndEnabled && !woundPoint.isBreached)
            {
                availablePoints.Add(woundPoint);
            }
        }

        if (availablePoints.Count == 0)
        {
            return;
        }

        int selectedIndex = Random.Range(0, availablePoints.Count);
        availablePoints[selectedIndex].ShowBreach();
    }

    private void CreateFloatingPrompt()
    {
        GameObject canvasObject = new GameObject("Skin Breached Floating UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        canvasObject.transform.localPosition = Vector3.up * FloatingHeight;
        canvasObject.transform.localScale = Vector3.one * CanvasWorldScale;

        floatingCanvas = canvasObject.GetComponent<Canvas>();
        floatingCanvas.renderMode = RenderMode.WorldSpace;
        floatingCanvas.overrideSorting = true;
        floatingCanvas.sortingOrder = 100;
        floatingCanvas.worldCamera = Camera.main;

        CanvasScaler canvasScaler = canvasObject.GetComponent<CanvasScaler>();
        canvasScaler.dynamicPixelsPerUnit = CanvasPixelsPerUnit;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = CanvasSize;

        GameObject panelObject = CreateUiChild("Panel", canvasRect, typeof(Image));
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        StretchToParent(panelRect);
        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0.055f, 0.075f, 0.095f, 0.94f);

        statusLabel = CreateText("Status", panelRect, StatusSize, StatusPosition, "Skin Breached", StatusFontSize);
        statusLabel.color = new Color(1f, 0.82f, 0.68f, 1f);

        GameObject buttonObject = CreateUiChild("Repair Button", panelRect, typeof(Image), typeof(Button));
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = CenterAnchor;
        buttonRect.anchorMax = CenterAnchor;
        buttonRect.pivot = CenterAnchor;
        buttonRect.sizeDelta = ButtonSize;
        buttonRect.anchoredPosition = ButtonPosition;

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = new Color(0.2f, 0.55f, 0.35f, 1f);
        repairButton = buttonObject.GetComponent<Button>();
        repairButton.targetGraphic = buttonImage;
        repairButton.onClick.AddListener(BeginRepair);

        repairButtonLabel = CreateText("Button Label", buttonRect, ButtonLabelSize, Vector2.zero, "Repair", ButtonFontSize);
        repairButtonLabel.raycastTarget = false;

        canvasObject.SetActive(false);
    }

    private void ShowBreach()
    {
        if (isBreached || floatingCanvas == null)
        {
            return;
        }

        isBreached = true;
        isRepairing = false;
        statusLabel.text = "Skin Breached";
        repairButton.interactable = true;
        repairButtonLabel.text = "Repair";
        floatingCanvas.gameObject.SetActive(true);
        NotifySkinBreachStateChanged();
    }

    private void BeginRepair()
    {
        if (!isBreached || isRepairing)
        {
            return;
        }

        StartCoroutine(RepairRoutine());
    }

    private IEnumerator RepairRoutine()
    {
        isRepairing = true;
        repairButton.interactable = false;
        float remainingSeconds = RepairDurationSeconds;

        while (remainingSeconds > 0f)
        {
            repairButtonLabel.text = $"Repairing {Mathf.CeilToInt(remainingSeconds)}s";
            remainingSeconds -= Time.deltaTime;
            yield return null;
        }

        isBreached = false;
        isRepairing = false;
        floatingCanvas.gameObject.SetActive(false);
        NotifySkinBreachStateChanged();
    }

    private static void NotifySkinBreachStateChanged()
    {
        OnSkinBreachStateChanged?.Invoke(HasActiveBreaches);
    }

    private static GameObject CreateUiChild(string objectName, Transform parent, params System.Type[] componentTypes)
    {
        System.Type[] requiredTypes = new System.Type[componentTypes.Length + 2];
        requiredTypes[0] = typeof(RectTransform);
        requiredTypes[1] = typeof(CanvasRenderer);
        for (int index = 0; index < componentTypes.Length; index++)
        {
            requiredTypes[index + 2] = componentTypes[index];
        }

        GameObject child = new GameObject(objectName, requiredTypes);
        child.transform.SetParent(parent, false);
        return child;
    }

    private static TextMeshProUGUI CreateText(string objectName, RectTransform parent, Vector2 size, Vector2 position, string initialText, float fontSize)
    {
        GameObject textObject = CreateUiChild(objectName, parent, typeof(TextMeshProUGUI));
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = CenterAnchor;
        textRect.anchorMax = CenterAnchor;
        textRect.pivot = CenterAnchor;
        textRect.sizeDelta = size;
        textRect.anchoredPosition = position;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = initialText;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private static void StretchToParent(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }
}
