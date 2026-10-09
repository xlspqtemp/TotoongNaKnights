using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Maps one existing body-part group to one tactical deployment button.</summary>
[Serializable]
public sealed class InfectionBodyPartButtonReference
{
    public InfectionBodyPartGroup bodyPartGroup;
    public Button button;
    public Transform worldAnchor;
}

/// <summary>Creates, selects, and dispatches to infection markers on day advances.</summary>
[DisallowMultipleComponent]
public sealed class InfectionSpawner : MonoBehaviour
{
    private const int MinimumEventsPerDay = 2;
    private const int MaximumEventsPerDay = 5;
    private const int DefaultEventsPerDay = 4;
    private const int DefaultMaximumActiveWbcs = 5;
    private const int DefaultMaximumActiveInfections = 5;
    private const float SquadSpawnOffsetRadius = 1.75f;
    private const float SquadStandingOffsetRadius = 2.8f;
    private const float SquadOffsetAngleStep = 2.39996323f;
    private const float SquadPromptFadeDuration = 0.4f;
    private const float InfectionPopupFadeDuration = 0.3f;
    private const float InfectionPopupReferenceDistance = 5f;
    private static readonly Vector2 InfectionPopupSize = new Vector2(280f, 96f);
    private const string DefaultMarkerSpriteResourcePath = "HUDWhiteSwatch";
    private const string DefaultWbcAvatarResourcePath = "Manual/WBC";
    private const string OrderButtonSuffix = " Order";

    private static readonly Vector2 DefaultMarkerSize = new Vector2(18f, 18f);
    private static readonly Vector2 DefaultMarkerOffset = new Vector2(88f, 36f);
    private static readonly Vector2 DefaultDispatchAvatarSize = new Vector2(38f, 38f);
    private static readonly Vector2 DefaultDispatchAvatarOffset = new Vector2(0f, -44f);
    private static readonly Color DefaultBacterialColor = new Color(0.95f, 0.2f, 0.18f, 1f);
    private static readonly Color DefaultViralColor = new Color(0.2f, 0.55f, 1f, 1f);
    private static readonly Color DefaultSelectionOutlineColor = Color.white;
    private static readonly Color DefaultDispatchUnavailableColor = new Color(0.45f, 0.45f, 0.45f, 0.65f);

    [Header("Day Progression")]
    [SerializeField] private DayCounterUI dayCounter;
    [SerializeField] private bool spawnOnDayAdvance = true;
    [SerializeField, Range(MinimumEventsPerDay, MaximumEventsPerDay)] private int eventsPerDay = DefaultEventsPerDay;

    [Header("Infection Entries")]
    [SerializeField] private List<InfectionData> infectionEntries = CreateDefaultInfectionEntries();

    [Header("Body-Part Button Mapping")]
    [SerializeField] private List<InfectionBodyPartButtonReference> bodyPartButtons = CreateDefaultBodyPartButtons();
    [SerializeField, Min(1)] private int maxActiveInfections = DefaultMaximumActiveInfections;

    [Header("Legacy UI Toggles")]
    [SerializeField] private bool showBodyPartButtons;
    [SerializeField] private CanvasGroup bodyPartButtonsCanvasGroup;
    [SerializeField] private bool enableLegacyAvatarClickDispatch;

    [Header("New WBC HUD")]
    [SerializeField] private Canvas hudCanvas;
    [SerializeField] private Texture2D wbcHeadshotTexture;
    [SerializeField] private string wbcHeadshotResourcePath = "UI/WBC headshot";
    [SerializeField] private Vector2 headshotButtonSize = new Vector2(78f, 78f);
    [SerializeField] private Vector2 squadHudBottomRightOffset = new Vector2(-28f, 28f);
    [SerializeField] private Vector2 infectionRowBottomRightOffset = new Vector2(-24f, 122f);
    [SerializeField] private Vector2 infectionRowSize = new Vector2(320f, 96f);
    [SerializeField] private Color squadHudPanelColor = new Color(0.025f, 0.045f, 0.065f, 0.9f);
    [SerializeField] private Color squadCountColor = Color.white;
    [SerializeField, Min(0f)] private float markerPopDuration = 0.18f;
    [SerializeField, Min(0f)] private float markerFadeDuration = 0.22f;

    [Header("Random Body-Part Placement")]
    [SerializeField, Min(0f)] private float preferredBodyPartWeight = 4f;
    [SerializeField, Min(0f)] private float otherBodyPartWeight = 1f;

    [Header("Marker Appearance")]
    [SerializeField] private Sprite markerSprite;
    [SerializeField] private string markerSpriteResourcePath = DefaultMarkerSpriteResourcePath;
    [SerializeField] private Color bacterialColor = DefaultBacterialColor;
    [SerializeField] private Color viralColor = DefaultViralColor;
    [SerializeField] private Color selectionOutlineColor = DefaultSelectionOutlineColor;
    [SerializeField, Min(0f)] private float selectionOutlineThickness = 3f;
    [SerializeField] private Vector2 mildMarkerSize = DefaultMarkerSize;
    [SerializeField] private Vector2 moderateMarkerSize = new Vector2(24f, 24f);
    [SerializeField] private Vector2 severeMarkerSize = new Vector2(30f, 30f);
    [SerializeField] private Vector2 markerOffset = DefaultMarkerOffset;
    [SerializeField, Min(1f)] private float maximumMarkerRowWidth = 220f;
    [SerializeField, Min(0f)] private float markerSpacing = 8f;

    [Header("WBC Dispatch")]
    [SerializeField] private bool enableWbcDispatch = true;
    [SerializeField] private bool useLegacySelectFirst;
    [SerializeField, Min(1)] private int maxActiveWbcs = DefaultMaximumActiveWbcs;
    [SerializeField, Min(0.1f)] private float perWbcCooldownSeconds = 20f;
    [SerializeField, Min(0f)] private float dispatchArrivalDelaySeconds = 2f;
    [SerializeField] private Texture2D wbcAvatarTexture;
    [SerializeField] private string wbcAvatarResourcePath = DefaultWbcAvatarResourcePath;
    [SerializeField] private Vector2 dispatchAvatarSize = DefaultDispatchAvatarSize;
    [SerializeField] private Vector2 dispatchAvatarOffset = DefaultDispatchAvatarOffset;
    [SerializeField] private Color dispatchUnavailableColor = DefaultDispatchUnavailableColor;

    [Header("Infection Resolution")]
    [SerializeField] private bool useFixedDurationClash = false;
    [SerializeField, Min(0f)] private float bacterialClashDurationSeconds = 4f;
    [SerializeField] private float mildBacterialWellnessGain = 2f;
    [SerializeField] private float moderateBacterialWellnessGain = 4f;
    [SerializeField] private float severeBacterialWellnessGain = 6f;
    [SerializeField, Min(0f)] private float wellnessGainOnResolve = 5f;
    [Header("Daily Infection Wellness Costs")]
    [SerializeField, Min(0f)] private float mildDailyInfectionWellnessCost = 1f;
    [SerializeField, Min(0f)] private float moderateDailyInfectionWellnessCost = 2f;
    [SerializeField, Min(0f)] private float severeDailyInfectionWellnessCost = 4f;
    [SerializeField, Min(0f)] private float additionalUnresolvedDailyWellnessCost = 2f;
    [SerializeField, Min(0f)] private float wellnessLossOnSquadWipe = 3f;
    [SerializeField] private KeyCode debugWellnessLossKey = KeyCode.L;
    [SerializeField, Min(0f)] private float debugWellnessLossAmount = 10f;
    [SerializeField, Min(1)] private int containedSeverityDropSteps = 1;
    [SerializeField, Min(1)] private int containedSelfResolveDays = 2;
    [SerializeField, Min(0f)] private float containedDailyWellnessCost = 1f;
    [SerializeField] private Color containedMarkerColor = new Color(0.45f, 0.5f, 0.55f, 1f);

    [Header("Infection Threat Visuals")]
    [SerializeField, Min(0)] private int threatVisualSpawnCount = 5;
    [SerializeField, Min(0f)] private float threatVisualClusterRadius = 0.5f;
    [SerializeField] private GameObject bacteriaPrefab;

    [Header("WBC Squad Dispatch")]
    [SerializeField] private CirculatorySystemController circulatorySystemController;
    [SerializeField] private CameraScript cameraScript;
    [SerializeField] private Transform squadSpawnAnchor;
    [SerializeField, Min(1)] private int squadMemberCount = DefaultMaximumActiveWbcs;
    [SerializeField] private bool switchToCirculatoryOnSquadSpawn = true;
    [SerializeField] private Color idleSquadHighlightColor = new Color(0f, 1f, 1f, 1f);
    [SerializeField, Min(0f)] private float headshotCooldownSeconds = 5f;
    [SerializeField] private bool keepAliveUntilResolved = true;
    [SerializeField, Min(0.1f)] private float squadLifetimeSeconds = 20f;
    [SerializeField, Min(0.1f)] private float dispatchNavMeshSampleRadius = 25f;
    [SerializeField, Min(0.1f)] private float dispatchArrivalRadius = 0.5f;
    [SerializeField, Min(0f)] private float combatDetectionRange = 8f;
    [SerializeField, Min(0f)] private float combatMeleeRange = 1.5f;
    [SerializeField, Min(0f)] private float wbcCombatMoveSpeed = 3.5f;
    [SerializeField] private bool verboseCombatLogging;
    [SerializeField, Min(0f)] private float infectionAnchorMatchRadius = 2f;
    [SerializeField] private Color destinationHoverColor = new Color(0.15f, 1f, 0.9f, 0.85f);
    [SerializeField, Min(0.1f)] private float destinationHoverRadius = 1f;
    [SerializeField, Min(0.005f)] private float destinationHoverLineWidth = 0.06f;
    [SerializeField] private Vector2 promptBannerSize = new Vector2(700f, 58f);
    [SerializeField] private Vector2 promptBannerTopOffset = new Vector2(0f, -24f);
    [SerializeField] private Color promptBannerColor = new Color(0.025f, 0.045f, 0.065f, 0.92f);
    [SerializeField, Min(0f)] private float promptAutoFadeSeconds = 8f;

    private readonly Dictionary<Button, List<InfectionMarker>> markersByBodyPartButton = new Dictionary<Button, List<InfectionMarker>>();
    private readonly List<WbcDispatchSlot> wbcDispatchSlots = new List<WbcDispatchSlot>();
    private readonly List<InfectionMarker> activeInfectionMarkers = new List<InfectionMarker>();
    private readonly List<InfectionMarker> fadingInfectionMarkers = new List<InfectionMarker>();
    private InfectionMarker selectedMarker;
    private Sprite resolvedMarkerSprite;
    private Texture2D resolvedWbcAvatarTexture;
    private Texture2D resolvedHeadshotTexture;
    private RectTransform infectionRowRect;
    private Button headshotButton;
    private TextMeshProUGUI squadCountLabel;
    private Image headshotCooldownFill;
    private TextMeshProUGUI headshotCooldownLabel;
    private GameObject promptBannerObject;
    private TextMeshProUGUI promptBannerLabel;
    private CanvasGroup promptBannerCanvasGroup;
    private TextMeshProUGUI excessInfectionLabel;
    private GameObject selectedInfectionPopupObject;
    private CanvasGroup selectedInfectionPopupCanvasGroup;
    private TextMeshProUGUI selectedInfectionCategoryLabel;
    private TextMeshProUGUI selectedInfectionDetailsLabel;
    private InfectionMarker selectedInfectionPopupMarker;
    private GameObject[] idleSquad;
    private readonly List<DispatchedWbcSquad> dispatchedWbcSquads = new List<DispatchedWbcSquad>();
    private float headshotCooldownRemaining;
    private float promptAutoFadeRemaining;
    private float promptFadeElapsed;
    private float promptFadeStartAlpha;
    private int nextInfectionSpawnOrder;
    private int idleSquadDeploymentIndex;
    private bool awaitingSquadDestination;
    private bool awaitingInfectionTargetSelection;
    private bool promptFadingOut;
    private Camera gameplayCamera;
    private LineRenderer destinationHoverRing;
    private Material destinationHoverMaterial;

    public IReadOnlyList<InfectionMarker> ActiveInfections => activeInfectionMarkers;

    public bool IsSquadDispatchedTo(InfectionMarker marker)
    {
        if (marker == null)
            return false;

        for (int squadIndex = 0; squadIndex < dispatchedWbcSquads.Count; squadIndex++)
        {
            DispatchedWbcSquad squad = dispatchedWbcSquads[squadIndex];
            if (squad != null && squad.infectionTarget == marker)
                return true;
        }

        return false;
    }

    public bool IsAwaitingInfectionTargetSelection => awaitingInfectionTargetSelection;

    public sealed class InfectionMarker
    {
        public InfectionData infection;
        public InfectionBodyPartButtonReference bodyPartMapping;
        public string bodyPartName;
        public InfectionBodyPartGroup bodyPartGroup;
        public Button markerButton;
        public Image markerImage;
        public Outline selectionOutline;
        public RectTransform markerRect;
        public Button dispatchButton;
        public RawImage dispatchImage;
        public TextMeshProUGUI dispatchCountLabel;
        public Canvas canvas;
        public Vector2 canvasLocalBodyPartPosition;
        public Vector2 markerSize;
        public CanvasGroup canvasGroup;
        public float animationElapsed;
        public int spawnOrder;
        public bool isRemoving;
        public bool isContained;
        public bool resolutionPending;
        public int containedResolveDay;
        public int unresolvedDays;
        public bool wellnessGainAwardedOnResolve;
        public Vector3 worldPosition;
        public int vesselSpawnPointIndex;
        public readonly List<GameObject> threatVisuals = new List<GameObject>();
        public bool threatVisualsDespawned;
    }

    private sealed class WbcDispatchSlot
    {
        public float cooldownRemaining;
    }

    private sealed class DispatchedWbcSquad
    {
        public GameObject[] units;
        public int deploymentIndex;
        public float lifetimeRemaining;
        public bool attackOnArrival;
        public InfectionMarker infectionTarget;
        public bool resolutionTriggered;
        public bool arrivalMessageLogged;
        public bool pathFailureLogged;
        public bool combatStarted;
        public bool wipeWellnessPenaltyApplied;
        public readonly HashSet<GameObject> arrivedUnits = new HashSet<GameObject>();
    }

    private void Awake()
    {
        if (dayCounter == null)
            dayCounter = FindFirstObjectByType<DayCounterUI>();
        if (hudCanvas == null)
            hudCanvas = FindFirstObjectByType<Canvas>();
        if (circulatorySystemController == null)
            circulatorySystemController = FindFirstObjectByType<CirculatorySystemController>();
        if (cameraScript == null)
            cameraScript = FindFirstObjectByType<CameraScript>();
        if (gameplayCamera == null && cameraScript != null)
            gameplayCamera = cameraScript.GetComponentInChildren<Camera>();
        if (gameplayCamera == null)
            gameplayCamera = Camera.main;
        if (bodyPartButtonsCanvasGroup == null)
        {
            GameObject legacyButtonsObject = GameObject.Find("TacticalOrdersContainer");
            if (legacyButtonsObject != null)
                bodyPartButtonsCanvasGroup = legacyButtonsObject.GetComponent<CanvasGroup>();
        }

        eventsPerDay = Mathf.Clamp(DifficultySettings.CurrentStats.eventsPerDay, MinimumEventsPerDay, MaximumEventsPerDay);
        maxActiveInfections = Mathf.Max(1, maxActiveInfections);
        maxActiveWbcs = Mathf.Max(1, maxActiveWbcs);
        squadMemberCount = Mathf.Max(1, squadMemberCount);
        headshotCooldownSeconds = Mathf.Max(0f, headshotCooldownSeconds);
        squadLifetimeSeconds = Mathf.Max(0.1f, squadLifetimeSeconds);
        EnsureDispatchSlots();
        ApplyBodyPartButtonVisibility();
    }

    private void OnEnable()
    {
        if (dayCounter != null)
            dayCounter.OnDayAdvanced += HandleDayAdvanced;
    }

    private void Start()
    {
        DifficultyStats stats = DifficultySettings.CurrentStats;
        Debug.Log($"Difficulty: {stats.difficultyName} | eventsPerDay={stats.eventsPerDay} | wbcHp={stats.wbcHp:0.##} | wbcAttack={stats.wbcAttack:0.##} | bacteriaHp={stats.bacteriaHp:0.##} | virusHp={stats.virusHp:0.##} | bacteriaAttack={stats.bacteriaAttack:0.##} | virusAttack={stats.virusAttack:0.##} | attackIntervalSeconds={stats.attackIntervalSeconds:0.##} | wbcDamageMultiplierVsBacteria={stats.wbcDamageMultiplierVsBacteria:0.##} | wbcDamageMultiplierVsVirus={stats.wbcDamageMultiplierVsVirus:0.##}", this);
        CreateWbcSquadHud();
        CreateSquadPromptBanner();
        CreateDestinationHoverRing();
        RefreshWbcSquadHud();
        if (dayCounter == null)
            Debug.LogWarning("[InfectionSpawner] DayCounterUI is missing; day-advance infection spawning is disabled.", this);
        if (hudCanvas == null)
            Debug.LogWarning("[InfectionSpawner] HUD Canvas is missing; the WBC HUD and infection row are disabled.", this);
        if (circulatorySystemController == null)
            Debug.LogWarning("[InfectionSpawner] CirculatorySystemController is missing; WBC squads cannot be spawned.", this);
    }

    private void Update()
    {
        if (Time.timeScale > 0f && Input.GetKeyDown(debugWellnessLossKey))
            ApplyWellnessChange("Debug test", -debugWellnessLossAmount);

        UpdateInfectionMarkerAnimations();
        UpdateInfectionThreatVisuals();
        UpdateSelectedInfectionPopup();
        UpdateWbcCooldowns();
        UpdateIdleAndDispatchedSquads();
        UpdateInfectionCombat();
        HandleSquadPromptCancellation();
        UpdateSquadPrompt();
        UpdateDestinationHover();
        HandleMapClick();
        RefreshWbcSquadHud();
    }

    private void LateUpdate()
    {
        if (selectedInfectionPopupObject == null || !selectedInfectionPopupObject.activeSelf || selectedInfectionPopupMarker == null)
            return;

        Camera popupCamera = gameplayCamera != null ? gameplayCamera : Camera.main;
        if (popupCamera != null)
            PositionSelectedInfectionPopup(selectedInfectionPopupMarker, popupCamera);
    }

    private void OnDisable()
    {
        if (dayCounter != null)
            dayCounter.OnDayAdvanced -= HandleDayAdvanced;
        if (destinationHoverRing != null)
            destinationHoverRing.gameObject.SetActive(false);
    }

    private void OnValidate()
    {
        eventsPerDay = Mathf.Clamp(eventsPerDay, MinimumEventsPerDay, MaximumEventsPerDay);
        maxActiveInfections = Mathf.Max(1, maxActiveInfections);
        maxActiveWbcs = Mathf.Max(1, maxActiveWbcs);
        squadMemberCount = Mathf.Max(1, squadMemberCount);
        headshotCooldownSeconds = Mathf.Max(0f, headshotCooldownSeconds);
        squadLifetimeSeconds = Mathf.Max(0.1f, squadLifetimeSeconds);
        infectionAnchorMatchRadius = Mathf.Max(0f, infectionAnchorMatchRadius);

        dispatchNavMeshSampleRadius = Mathf.Max(0.1f, dispatchNavMeshSampleRadius);
        dispatchArrivalRadius = Mathf.Max(0.1f, dispatchArrivalRadius);
        combatDetectionRange = Mathf.Max(0f, combatDetectionRange);
        combatMeleeRange = Mathf.Max(0f, combatMeleeRange);
        wbcCombatMoveSpeed = Mathf.Max(0f, wbcCombatMoveSpeed);
        ClampMarkerSize(ref mildMarkerSize);
        ClampMarkerSize(ref moderateMarkerSize);
        ClampMarkerSize(ref severeMarkerSize);
        dispatchAvatarSize.x = Mathf.Max(1f, dispatchAvatarSize.x);
        dispatchAvatarSize.y = Mathf.Max(1f, dispatchAvatarSize.y);
        perWbcCooldownSeconds = Mathf.Max(0.1f, perWbcCooldownSeconds);
        dispatchArrivalDelaySeconds = Mathf.Max(0f, dispatchArrivalDelaySeconds);
        bacterialClashDurationSeconds = Mathf.Max(0f, bacterialClashDurationSeconds);
        mildBacterialWellnessGain = Mathf.Max(0f, mildBacterialWellnessGain);
        moderateBacterialWellnessGain = Mathf.Max(0f, moderateBacterialWellnessGain);
        severeBacterialWellnessGain = Mathf.Max(0f, severeBacterialWellnessGain);
        wellnessGainOnResolve = Mathf.Max(0f, wellnessGainOnResolve);
        mildDailyInfectionWellnessCost = Mathf.Max(0f, mildDailyInfectionWellnessCost);
        moderateDailyInfectionWellnessCost = Mathf.Max(0f, moderateDailyInfectionWellnessCost);
        severeDailyInfectionWellnessCost = Mathf.Max(0f, severeDailyInfectionWellnessCost);
        additionalUnresolvedDailyWellnessCost = Mathf.Max(0f, additionalUnresolvedDailyWellnessCost);
        wellnessLossOnSquadWipe = Mathf.Max(0f, wellnessLossOnSquadWipe);
        debugWellnessLossAmount = Mathf.Max(0f, debugWellnessLossAmount);
        containedSeverityDropSteps = Mathf.Max(1, containedSeverityDropSteps);
        containedSelfResolveDays = Mathf.Max(1, containedSelfResolveDays);
        containedDailyWellnessCost = Mathf.Max(0f, containedDailyWellnessCost);
        threatVisualSpawnCount = Mathf.Max(0, threatVisualSpawnCount);
        threatVisualClusterRadius = Mathf.Max(0f, threatVisualClusterRadius);
        maximumMarkerRowWidth = Mathf.Max(severeMarkerSize.x, maximumMarkerRowWidth);
        promptAutoFadeSeconds = Mathf.Max(0f, promptAutoFadeSeconds);
        if (!Application.isPlaying)
            ApplyBodyPartButtonVisibility();
    }

    private static void ClampMarkerSize(ref Vector2 markerSize)
    {
        markerSize.x = Mathf.Max(1f, markerSize.x);
        markerSize.y = Mathf.Max(1f, markerSize.y);
    }

    private void ApplyBodyPartButtonVisibility()
    {
        if (bodyPartButtonsCanvasGroup == null)
            return;

        bodyPartButtonsCanvasGroup.alpha = showBodyPartButtons ? 1f : 0f;
        bodyPartButtonsCanvasGroup.interactable = showBodyPartButtons;
        bodyPartButtonsCanvasGroup.blocksRaycasts = showBodyPartButtons;
    }

    private void CreateWbcSquadHud()
    {
        if (hudCanvas == null || !(hudCanvas.transform is RectTransform canvasRect))
            return;

        GameObject squadHudObject = new GameObject("WBC Squad HUD", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        squadHudObject.transform.SetParent(canvasRect, false);
        squadHudObject.transform.SetAsLastSibling();
        RectTransform squadHudRect = squadHudObject.GetComponent<RectTransform>();
        squadHudRect.anchorMin = new Vector2(0.85f, 0.02f);
        squadHudRect.anchorMax = new Vector2(1f, 0.28f);
        squadHudRect.pivot = new Vector2(1f, 0f);
        squadHudRect.anchoredPosition = new Vector2(-12f, 12f);
        squadHudRect.sizeDelta = Vector2.zero;
        Image squadHudBackground = squadHudObject.GetComponent<Image>();
        squadHudBackground.color = squadHudPanelColor;
        squadHudBackground.raycastTarget = false;

        GameObject headshotObject = new GameObject("WBC Squad Headshot Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage), typeof(Button));
        headshotObject.transform.SetParent(squadHudObject.transform, false);
        RectTransform headshotRect = headshotObject.GetComponent<RectTransform>();
        headshotRect.anchorMin = Vector2.zero;
        headshotRect.anchorMax = Vector2.one;
        headshotRect.offsetMin = Vector2.zero;
        headshotRect.offsetMax = Vector2.zero;

        RawImage headshotImage = headshotObject.GetComponent<RawImage>();
        headshotImage.texture = ResolveHeadshotTexture();
        headshotImage.raycastTarget = true;
        headshotButton = headshotObject.GetComponent<Button>();
        headshotButton.targetGraphic = headshotImage;
        headshotButton.transition = Selectable.Transition.None;
        headshotButton.onClick.AddListener(SpawnWbcSquad);

        GameObject cooldownFillObject = new GameObject("WBC Headshot Cooldown Radial Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        cooldownFillObject.transform.SetParent(headshotObject.transform, false);
        RectTransform cooldownFillRect = cooldownFillObject.GetComponent<RectTransform>();
        cooldownFillRect.anchorMin = Vector2.zero;
        cooldownFillRect.anchorMax = Vector2.one;
        cooldownFillRect.offsetMin = Vector2.zero;
        cooldownFillRect.offsetMax = Vector2.zero;
        headshotCooldownFill = cooldownFillObject.GetComponent<Image>();
        headshotCooldownFill.sprite = ResolveMarkerSprite();
        headshotCooldownFill.type = Image.Type.Filled;
        headshotCooldownFill.fillMethod = Image.FillMethod.Radial360;
        headshotCooldownFill.fillOrigin = (int)Image.Origin360.Top;
        headshotCooldownFill.fillClockwise = false;
        headshotCooldownFill.color = dispatchUnavailableColor;
        headshotCooldownFill.raycastTarget = false;
        headshotCooldownFill.enabled = false;

        headshotCooldownLabel = CreateSquadHudText("WBC Headshot Cooldown Countdown", headshotObject.transform, string.Empty, 20f, FontStyles.Bold, Color.white);
        headshotCooldownLabel.rectTransform.anchorMin = Vector2.zero;
        headshotCooldownLabel.rectTransform.anchorMax = Vector2.one;
        headshotCooldownLabel.rectTransform.offsetMin = Vector2.zero;
        headshotCooldownLabel.rectTransform.offsetMax = Vector2.zero;
        headshotCooldownLabel.raycastTarget = false;
        headshotCooldownLabel.enabled = false;

        squadCountLabel = CreateSquadHudText("WBC Squad Counter", squadHudObject.transform,
            $"Squads {CountActiveSquads()}/{Mathf.Max(1, DifficultySettings.CurrentStats.maxActiveSquads)}", 14f, FontStyles.Bold, squadCountColor);
        squadCountLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
        squadCountLabel.rectTransform.anchorMax = new Vector2(1f, 0f);
        squadCountLabel.rectTransform.pivot = new Vector2(0.5f, 0f);
        squadCountLabel.rectTransform.anchoredPosition = new Vector2(0f, 4f);
        squadCountLabel.rectTransform.sizeDelta = new Vector2(-8f, 28f);
        squadCountLabel.raycastTarget = false;

        GameObject rowObject = new GameObject("Active Infection Diamond Row", typeof(RectTransform));
        rowObject.transform.SetParent(squadHudObject.transform, false);
        rowObject.transform.SetAsLastSibling();
        infectionRowRect = rowObject.GetComponent<RectTransform>();
        infectionRowRect.anchorMin = new Vector2(0f, 1f);
        infectionRowRect.anchorMax = new Vector2(1f, 1f);
        infectionRowRect.pivot = new Vector2(0.5f, 1f);
        infectionRowRect.anchoredPosition = Vector2.zero;
        infectionRowRect.sizeDelta = new Vector2(0f, Mathf.Max(36f, severeMarkerSize.y + markerSpacing));
        excessInfectionLabel = CreateSquadHudText("Additional Infection Count", infectionRowRect, string.Empty, 14f, FontStyles.Bold, Color.white);
        excessInfectionLabel.rectTransform.anchorMin = new Vector2(1f, 0.5f);
        excessInfectionLabel.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        excessInfectionLabel.rectTransform.pivot = new Vector2(1f, 0.5f);
        excessInfectionLabel.rectTransform.sizeDelta = new Vector2(42f, 30f);
        excessInfectionLabel.enabled = false;
        rowObject.SetActive(false);
    }


    private void CreateSquadPromptBanner()
    {
        if (hudCanvas == null || !(hudCanvas.transform is RectTransform canvasRect))
            return;

        promptBannerObject = new GameObject("WBC Squad Destination Prompt Banner", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        promptBannerObject.transform.SetParent(canvasRect, false);
        promptBannerObject.transform.SetAsLastSibling();
        RectTransform bannerRect = promptBannerObject.GetComponent<RectTransform>();
        bannerRect.anchorMin = new Vector2(1f, 1f);
        bannerRect.anchorMax = new Vector2(1f, 1f);
        bannerRect.pivot = new Vector2(1f, 1f);
        bannerRect.anchoredPosition = promptBannerTopOffset;
        bannerRect.sizeDelta = promptBannerSize;

        Image bannerBackground = promptBannerObject.GetComponent<Image>();
        bannerBackground.color = promptBannerColor;
        bannerBackground.raycastTarget = false;
        promptBannerCanvasGroup = promptBannerObject.GetComponent<CanvasGroup>();
        promptBannerCanvasGroup.alpha = 0f;
        promptBannerCanvasGroup.interactable = false;
        promptBannerCanvasGroup.blocksRaycasts = false;

        promptBannerLabel = CreateSquadHudText("WBC Squad Destination Prompt Text", bannerRect,
            "Select a destination on the map or an infection to send your WBC squad.", 18f, FontStyles.Bold, Color.white);
        promptBannerLabel.alignment = TextAlignmentOptions.Center;
        promptBannerLabel.textWrappingMode = TextWrappingModes.Normal;
        promptBannerLabel.rectTransform.anchorMin = Vector2.zero;
        promptBannerLabel.rectTransform.anchorMax = Vector2.one;
        promptBannerLabel.rectTransform.offsetMin = new Vector2(12f, 8f);
        promptBannerLabel.rectTransform.offsetMax = new Vector2(-12f, -8f);
        promptBannerLabel.raycastTarget = false;
        promptBannerObject.SetActive(false);
    }

    private void ShowSquadPrompt()
    {
        if (promptBannerObject == null || promptBannerCanvasGroup == null)
            return;

        promptBannerObject.SetActive(true);
        promptBannerCanvasGroup.alpha = 0f;
        promptAutoFadeRemaining = promptAutoFadeSeconds;
        promptFadeElapsed = 0f;
        promptFadeStartAlpha = 0f;
        promptFadingOut = false;
    }

    private void FadeOutSquadPrompt()
    {
        if (promptBannerObject == null || promptBannerCanvasGroup == null || !promptBannerObject.activeSelf)
            return;

        promptAutoFadeRemaining = 0f;
        promptFadeElapsed = 0f;
        promptFadeStartAlpha = promptBannerCanvasGroup.alpha;
        promptFadingOut = true;
    }

    private void UpdateSquadPrompt()
    {
        if (promptBannerObject == null || promptBannerCanvasGroup == null || !promptBannerObject.activeSelf)
            return;

        if (awaitingInfectionTargetSelection && !awaitingSquadDestination)
            awaitingInfectionTargetSelection = false;

        float gameplayDeltaTime = GameplaySpeed.DeltaTime;
        if (awaitingSquadDestination && !awaitingInfectionTargetSelection && !promptFadingOut && promptAutoFadeSeconds > 0f)
        {
            promptAutoFadeRemaining = Mathf.Max(0f, promptAutoFadeRemaining - gameplayDeltaTime);
            if (promptAutoFadeRemaining <= 0f)
            {
                promptFadeStartAlpha = promptBannerCanvasGroup.alpha;
                promptFadeElapsed = 0f;
                promptFadingOut = true;
            }
        }

        promptFadeElapsed = Mathf.Min(SquadPromptFadeDuration, promptFadeElapsed + gameplayDeltaTime);
        float fadeProgress = SquadPromptFadeDuration > 0f ? promptFadeElapsed / SquadPromptFadeDuration : 1f;
        promptBannerCanvasGroup.alpha = Mathf.Lerp(promptFadeStartAlpha, promptFadingOut ? 0f : 1f, fadeProgress);
        if (promptFadingOut && fadeProgress >= 1f)
            promptBannerObject.SetActive(false);
    }

    private void CreateDestinationHoverRing()
    {
        GameObject ringObject = new GameObject("WBC Destination Hover Ring", typeof(LineRenderer));
        destinationHoverRing = ringObject.GetComponent<LineRenderer>();
        destinationHoverRing.useWorldSpace = true;
        destinationHoverRing.loop = true;
        destinationHoverRing.positionCount = 48;
        destinationHoverRing.startWidth = destinationHoverLineWidth;
        destinationHoverRing.endWidth = destinationHoverLineWidth;
        destinationHoverRing.startColor = destinationHoverColor;
        destinationHoverRing.endColor = destinationHoverColor;
        destinationHoverRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        destinationHoverRing.receiveShadows = false;
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            destinationHoverMaterial = new Material(shader)
            {
                name = "WBC Destination Hover Material",
                hideFlags = HideFlags.HideAndDontSave
            };
            destinationHoverRing.sharedMaterial = destinationHoverMaterial;
        }
        ringObject.SetActive(false);
    }

    private void UpdateDestinationHover()
    {
        if (idleSquad == null || destinationHoverRing == null || PointerIsOverClickableUi() ||
            !TryGetMapDestination(Input.mousePosition, out Vector3 targetPosition, out _, out _))
        {
            if (destinationHoverRing != null)
                destinationHoverRing.gameObject.SetActive(false);
            return;
        }

        destinationHoverRing.gameObject.SetActive(true);
        destinationHoverRing.startWidth = destinationHoverLineWidth;
        destinationHoverRing.endWidth = destinationHoverLineWidth;
        destinationHoverRing.startColor = destinationHoverColor;
        destinationHoverRing.endColor = destinationHoverColor;
        for (int index = 0; index < destinationHoverRing.positionCount; index++)
        {
            float angle = index * Mathf.PI * 2f / destinationHoverRing.positionCount;
            Vector3 point = targetPosition + Vector3.up * 0.08f +
                new Vector3(Mathf.Cos(angle) * destinationHoverRadius, 0f, Mathf.Sin(angle) * destinationHoverRadius);
            destinationHoverRing.SetPosition(index, point);
        }
    }

    private void HandleMapClick()
    {
        if (awaitingInfectionTargetSelection && Input.GetMouseButtonDown(0))
        {
            bool clickedActiveInfectionDiamond = false;
            if (EventSystem.current != null)
            {
                PointerEventData pointerData = new PointerEventData(EventSystem.current)
                {
                    position = Input.mousePosition
                };
                List<RaycastResult> raycastResults = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pointerData, raycastResults);
                foreach (RaycastResult result in raycastResults)
                {
                    if (result.gameObject == null)
                        continue;

                    foreach (InfectionMarker marker in activeInfectionMarkers)
                    {
                        if (marker != null && !marker.isRemoving && marker.markerButton != null &&
                            (result.gameObject == marker.markerButton.gameObject ||
                             result.gameObject.transform.IsChildOf(marker.markerButton.transform)))
                        {
                            clickedActiveInfectionDiamond = true;
                            break;
                        }
                    }

                    if (clickedActiveInfectionDiamond)
                        break;
                }
            }

            if (clickedActiveInfectionDiamond)
                return;

            awaitingInfectionTargetSelection = false;
            awaitingSquadDestination = false;
            FadeOutSquadPrompt();
            return;
        }

        if (!enableWbcDispatch || idleSquad == null || !Input.GetMouseButtonDown(0) || PointerIsOverClickableUi())
            return;

        if (!TryGetMapDestination(Input.mousePosition, out Vector3 destination, out string locationName, out Vector3 clickedPoint))
            return;

        InfectionMarker matchingInfection = FindInfectionNearAnchor(clickedPoint);
        if (matchingInfection != null)
        {
            SelectMarker(matchingInfection);
            return;
        }

        if (!DispatchIdleSquadTo(destination, null, locationName))
            LogWbcSquadMessage("WBC squad could not reach that map location.", ConsoleLogUI.LogType.Warning);
    }

    private bool PointerIsOverClickableUi()
    {
        if (EventSystem.current == null)
            return false;

        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };
        List<RaycastResult> raycastResults = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, raycastResults);
        foreach (RaycastResult result in raycastResults)
        {
            if (result.module is GraphicRaycaster && result.gameObject != null &&
                ExecuteEvents.GetEventHandler<IPointerClickHandler>(result.gameObject) != null)
            {
                return true;
            }
        }

        return false;
    }


    private bool TryGetMapDestination(Vector2 screenPosition, out Vector3 destination, out string locationName, out Vector3 clickedPoint)
    {
        destination = Vector3.zero;
        locationName = string.Empty;
        clickedPoint = Vector3.zero;
        if (gameplayCamera == null)
            gameplayCamera = cameraScript != null ? cameraScript.GetComponentInChildren<Camera>() : Camera.main;
        if (gameplayCamera == null)
            return TryGetNearestAnchorDestination(Vector3.zero, out destination, out locationName, out clickedPoint);

        Ray ray = gameplayCamera.ScreenPointToRay(screenPosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, gameplayCamera.farClipPlane, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (first, second) => first.distance.CompareTo(second.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;

            clickedPoint = hit.point;
            if (!TrySampleSquadNavMesh(hit.point, out destination))
                continue;

            locationName = ResolveMoveLocation(hit.point, hit.collider.gameObject.name);
            return true;
        }

        float planeHeight = squadSpawnAnchor != null
            ? squadSpawnAnchor.position.y
            : circulatorySystemController != null ? circulatorySystemController.transform.position.y : 0f;
        Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, planeHeight, 0f));
        if (groundPlane.Raycast(ray, out float planeDistance))
        {
            clickedPoint = ray.GetPoint(planeDistance);
            if (TrySampleSquadNavMesh(clickedPoint, out destination))
            {
                locationName = ResolveMoveLocation(clickedPoint, string.Empty);
                return true;
            }

            if (TryGetNearestAnchorDestination(clickedPoint, out destination, out locationName, out Vector3 nearestAnchor))
            {
                clickedPoint = nearestAnchor;
                return true;
            }
        }

        Vector3 referencePoint = ray.origin + ray.direction * Mathf.Max(0f, Vector3.Dot(GetSquadReferencePosition() - ray.origin, ray.direction));
        return TryGetNearestAnchorDestination(referencePoint, out destination, out locationName, out clickedPoint);
    }

    private bool TryGetNearestAnchorDestination(Vector3 referencePoint, out Vector3 destination, out string locationName, out Vector3 anchorPoint)
    {
        destination = Vector3.zero;
        locationName = string.Empty;
        anchorPoint = Vector3.zero;
        InfectionBodyPartButtonReference nearestMapping = null;
        if (bodyPartButtons == null)
            return false;

        float nearestDistance = float.PositiveInfinity;
        foreach (InfectionBodyPartButtonReference mapping in bodyPartButtons)
        {
            if (mapping == null || mapping.worldAnchor == null)
                continue;

            Vector3 candidate = GetBodyPartAnchorPosition(mapping);
            float distance = (candidate - referencePoint).sqrMagnitude;
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestMapping = mapping;
                anchorPoint = candidate;
            }
        }

        if (nearestMapping == null || !TrySampleSquadNavMesh(anchorPoint, out destination))
            return false;

        locationName = GetBodyPartDisplayName(nearestMapping);
        return true;
    }

    private bool TrySampleSquadNavMesh(Vector3 requestedPoint, out Vector3 sampledPoint)
    {
        foreach (GameObject unit in idleSquad)
        {
            if (unit == null)
                continue;

            NavMeshAgent agent = unit.GetComponent<NavMeshAgent>();
            if (agent == null || !agent.enabled)
                continue;

            NavMeshQueryFilter filter = new NavMeshQueryFilter
            {
                agentTypeID = agent.agentTypeID,
                areaMask = agent.areaMask
            };
            if (NavMesh.SamplePosition(requestedPoint, out NavMeshHit sample, dispatchNavMeshSampleRadius, filter))
            {
                sampledPoint = sample.position;
                return true;
            }
            break;
        }

        sampledPoint = Vector3.zero;
        return false;
    }

    private Vector3 GetBodyPartAnchorPosition(InfectionBodyPartButtonReference mapping)
    {
        if (mapping != null && mapping.worldAnchor != null)
            return mapping.worldAnchor.position;
        return GetSquadReferencePosition();
    }

    private Vector3 GetSquadReferencePosition()
    {
        if (squadSpawnAnchor != null)
            return squadSpawnAnchor.position;
        return circulatorySystemController != null ? circulatorySystemController.transform.position : Vector3.zero;
    }

    private InfectionMarker FindInfectionNearAnchor(Vector3 clickedPoint)
    {
        float maximumDistanceSquared = infectionAnchorMatchRadius * infectionAnchorMatchRadius;
        InfectionMarker nearestInfection = null;
        float nearestDistanceSquared = maximumDistanceSquared;
        foreach (InfectionMarker infection in activeInfectionMarkers)
        {
            if (infection == null || infection.isRemoving || infection.bodyPartMapping == null || infection.bodyPartMapping.worldAnchor == null)
                continue;

            Vector3 anchor = infection.bodyPartMapping.worldAnchor.position;
            float deltaX = anchor.x - clickedPoint.x;
            float deltaZ = anchor.z - clickedPoint.z;
            float distanceSquared = deltaX * deltaX + deltaZ * deltaZ;
            if (distanceSquared <= nearestDistanceSquared)
            {
                nearestDistanceSquared = distanceSquared;
                nearestInfection = infection;
            }
        }

        return nearestInfection;
    }

    private string ResolveMoveLocation(Vector3 point, string fallbackName)
    {
        if (bodyPartButtons == null)
            return string.IsNullOrWhiteSpace(fallbackName) ? "map" : fallbackName;

        InfectionBodyPartButtonReference nearestMapping = null;
        float nearestDistanceSquared = infectionAnchorMatchRadius * infectionAnchorMatchRadius;
        foreach (InfectionBodyPartButtonReference mapping in bodyPartButtons)
        {
            if (mapping == null || mapping.worldAnchor == null)
                continue;

            Vector3 anchor = mapping.worldAnchor.position;
            float deltaX = anchor.x - point.x;
            float deltaZ = anchor.z - point.z;
            float distanceSquared = deltaX * deltaX + deltaZ * deltaZ;
            if (distanceSquared <= nearestDistanceSquared)
            {
                nearestDistanceSquared = distanceSquared;
                nearestMapping = mapping;
            }
        }

        if (nearestMapping != null)
            return GetBodyPartDisplayName(nearestMapping);
        return string.IsNullOrWhiteSpace(fallbackName) ? "map" : fallbackName;
    }


    private void SpawnWbcSquad()
    {
        if (!enableWbcDispatch || idleSquad != null || headshotCooldownRemaining > 0f)
            return;

        int maxActiveSquads = Mathf.Max(1, DifficultySettings.CurrentStats.maxActiveSquads);
        int activeSquadCount = CountActiveSquads();
        if (activeSquadCount >= maxActiveSquads)
        {
            LogWbcSquadMessage($"All squads deployed ({activeSquadCount}/{maxActiveSquads}).", ConsoleLogUI.LogType.Warning);
            return;
        }

        if (circulatorySystemController == null)
            circulatorySystemController = FindFirstObjectByType<CirculatorySystemController>();
        if (circulatorySystemController == null)
        {
            LogWbcSquadMessage("WBC squad could not be spawned: CirculatorySystemController is missing.", ConsoleLogUI.LogType.Warning);
            return;
        }

        int deploymentIndex = GetNextAvailableSquadDeploymentIndex();
        Transform spawnOffsetBase = squadSpawnAnchor;
        if (spawnOffsetBase == null)
        {
            foreach (Transform child in circulatorySystemController.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "Heart")
                {
                    spawnOffsetBase = child;
                    break;
                }
            }
        }

        GameObject spawnOffsetAnchorObject = null;
        Transform spawnAnchor = null;
        if (spawnOffsetBase != null)
        {
            float spawnAngle = deploymentIndex * SquadOffsetAngleStep;
            float spawnRadius = SquadSpawnOffsetRadius * (1f + Mathf.Sqrt(deploymentIndex));
            Vector3 spawnOffset = new Vector3(Mathf.Cos(spawnAngle) * spawnRadius, 0f, Mathf.Sin(spawnAngle) * spawnRadius);
            spawnOffsetAnchorObject = new GameObject("Temporary WBC Squad Spawn Offset");
            spawnOffsetAnchorObject.transform.SetPositionAndRotation(spawnOffsetBase.position + spawnOffset, spawnOffsetBase.rotation);
            spawnAnchor = spawnOffsetAnchorObject.transform;
        }

        GameObject[] spawnedUnits = circulatorySystemController.SpawnNeutrophilsAt(spawnAnchor, squadMemberCount);
        if (spawnOffsetAnchorObject != null)
            Destroy(spawnOffsetAnchorObject);
        if (spawnedUnits == null || spawnedUnits.Length == 0)
        {
            LogWbcSquadMessage("WBC squad could not be spawned. Check the circulatory WBC prefab and NavMesh.", ConsoleLogUI.LogType.Warning);
            return;
        }

        idleSquad = spawnedUnits;
        idleSquadDeploymentIndex = deploymentIndex;
        float wbcMaxHp = DifficultySettings.CurrentStats.wbcHp;
        foreach (GameObject unit in idleSquad)
        {
            if (unit == null)
                continue;

            Health health = unit.GetComponent<Health>();
            if (health == null)
                health = unit.AddComponent<Health>();
            health.SetMaxHp(wbcMaxHp);

            WbcIdleHighlight highlight = unit.GetComponent<WbcIdleHighlight>();
            if (highlight == null)
                highlight = unit.AddComponent<WbcIdleHighlight>();
            highlight.SetHighlightColor(idleSquadHighlightColor);
            highlight.SetHighlighted(true);
            NavMeshAgent agent = unit.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled && agent.isOnNavMesh)
                agent.isStopped = true;
        }

        if (switchToCirculatoryOnSquadSpawn)
        {
            if (cameraScript == null)
                cameraScript = FindFirstObjectByType<CameraScript>();
            if (cameraScript != null)
                cameraScript.SelectLayer(2);
        }

        if (useLegacySelectFirst && selectedMarker != null && !selectedMarker.isRemoving && activeInfectionMarkers.Contains(selectedMarker))
        {
            awaitingSquadDestination = false;
            awaitingInfectionTargetSelection = false;
            Vector3 infectionWorldPosition = GetInfectionWorldPosition(selectedMarker);
            DispatchIdleSquadTo(infectionWorldPosition, selectedMarker, selectedMarker.bodyPartName);
        }
        else
        {
            if (!useLegacySelectFirst && selectedMarker != null)
            {
                selectedMarker.selectionOutline.enabled = false;
                if (selectedMarker.dispatchButton != null)
                    selectedMarker.dispatchButton.gameObject.SetActive(false);
                selectedMarker = null;
            }

            awaitingSquadDestination = true;
            awaitingInfectionTargetSelection = !useLegacySelectFirst;
            if (promptBannerLabel != null)
                promptBannerLabel.text = awaitingInfectionTargetSelection
                    ? "Select an infection to deploy."
                    : "Select a destination on the map or an infection to send your WBC squad.";
            ShowSquadPrompt();
        }

        RefreshWbcSquadHud();
    }

    /// <summary>Starts moving the idle WBC squad to a NavMesh position and begins its gameplay-time cooldown and lifetime.</summary>
    public bool DispatchIdleSquadTo(Vector3 destination)
    {
        return DispatchIdleSquadTo(destination, null, "map");
    }

    private bool DispatchIdleSquadTo(Vector3 destination, InfectionMarker infectionTarget, string moveLocation)
    {
        if (idleSquad == null || idleSquad.Length == 0)
            return false;

        if (infectionTarget != null)
        {
            float standingAngle = idleSquadDeploymentIndex * SquadOffsetAngleStep + Mathf.PI;
            float standingRadius = SquadStandingOffsetRadius * (1f + Mathf.Sqrt(idleSquadDeploymentIndex));
            destination += new Vector3(Mathf.Cos(standingAngle) * standingRadius, 0f, Mathf.Sin(standingAngle) * standingRadius);
        }

        List<NavMeshAgent> squadAgents = new List<NavMeshAgent>(idleSquad.Length);
        List<Vector3> sampledDestinations = new List<Vector3>(idleSquad.Length);
        foreach (GameObject unit in idleSquad)
        {
            if (unit == null)
                return false;

            NavMeshAgent agent = unit.GetComponent<NavMeshAgent>();
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
                return false;

            NavMeshQueryFilter filter = new NavMeshQueryFilter
            {
                agentTypeID = agent.agentTypeID,
                areaMask = agent.areaMask
            };
            if (!NavMesh.SamplePosition(destination, out NavMeshHit hit, dispatchNavMeshSampleRadius, filter))
            {
                LogWbcDispatchPathFailure(infectionTarget, moveLocation);
                return false;
            }

            squadAgents.Add(agent);
            sampledDestinations.Add(hit.position);
        }

        int destinationCount = 0;
        for (int index = 0; index < squadAgents.Count; index++)
        {
            NavMeshAgent agent = squadAgents[index];
            agent.isStopped = false;
            if (!agent.SetDestination(sampledDestinations[index]))
            {
                for (int rollbackIndex = 0; rollbackIndex <= destinationCount; rollbackIndex++)
                {
                    squadAgents[rollbackIndex].ResetPath();
                    squadAgents[rollbackIndex].isStopped = true;
                }
                LogWbcDispatchPathFailure(infectionTarget, moveLocation);
                return false;
            }
            destinationCount++;
        }

        foreach (GameObject unit in idleSquad)
        {
            WbcIdleHighlight highlight = unit.GetComponent<WbcIdleHighlight>();
            if (highlight != null)
                highlight.SetHighlighted(false);
        }

        dispatchedWbcSquads.Add(new DispatchedWbcSquad
        {
            units = idleSquad,
            deploymentIndex = idleSquadDeploymentIndex,
            lifetimeRemaining = squadLifetimeSeconds,
            attackOnArrival = infectionTarget != null,
            infectionTarget = infectionTarget
        });
        idleSquad = null;
        awaitingSquadDestination = false;
        FadeOutSquadPrompt();
        headshotCooldownRemaining = headshotCooldownSeconds;

        if (infectionTarget != null)
            LogWbcSquadMessage($"WBC squad dispatched to {infectionTarget.infection.displayName} at {infectionTarget.bodyPartName}", ConsoleLogUI.LogType.Success);
        else
            LogWbcSquadMessage($"WBC squad moved to {moveLocation}", ConsoleLogUI.LogType.Success);

        RefreshWbcSquadHud();
        return true;
    }
    private void LogWbcDispatchPathFailure(InfectionMarker infectionTarget, string moveLocation)
    {
        string destinationName = infectionTarget != null && infectionTarget.infection != null
            ? infectionTarget.infection.displayName
            : moveLocation;
        LogWbcSquadMessage($"WBC squad could not path to {destinationName}.", ConsoleLogUI.LogType.Warning);
    }



    private void HandleSquadPromptCancellation()
    {
        if (!awaitingSquadDestination)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            awaitingSquadDestination = false;
            FadeOutSquadPrompt();
        }
    }

    private void UpdateIdleAndDispatchedSquads()
    {
        float gameplayDeltaTime = GameplaySpeed.DeltaTime;
        if (headshotCooldownRemaining > 0f)
            headshotCooldownRemaining = Mathf.Max(0f, headshotCooldownRemaining - gameplayDeltaTime);

        for (int squadIndex = dispatchedWbcSquads.Count - 1; squadIndex >= 0; squadIndex--)
        {
            DispatchedWbcSquad squad = dispatchedWbcSquads[squadIndex];
            if (!squad.combatStarted)
            {
                bool hasLivingUnit = false;
                foreach (GameObject unit in squad.units)
                {
                    if (unit == null || !unit.activeInHierarchy)
                        continue;

                    Health health = unit.GetComponent<Health>();
                    if (health != null && !health.IsDead)
                    {
                        hasLivingUnit = true;
                        break;
                    }
                }

                if (!hasLivingUnit)
                {
                    DespawnWbcSquad(squadIndex);
                    continue;
                }
            }

            if (!keepAliveUntilResolved)
                squad.lifetimeRemaining -= gameplayDeltaTime;
            foreach (GameObject unit in squad.units)
            {
                if (unit == null || !unit.activeInHierarchy || squad.arrivedUnits.Contains(unit))
                    continue;

                NavMeshAgent agent = unit.GetComponent<NavMeshAgent>();
                if (agent == null || !agent.enabled || !agent.isOnNavMesh || agent.pathPending)
                    continue;
                if (!agent.hasPath || agent.pathStatus != NavMeshPathStatus.PathComplete)
                {
                    if (!squad.pathFailureLogged)
                    {
                        squad.pathFailureLogged = true;
                        LogWbcDispatchPathFailure(squad.infectionTarget, squad.infectionTarget != null ? squad.infectionTarget.bodyPartName : "map");
                    }
                    continue;
                }
                if (agent.remainingDistance > Mathf.Max(agent.stoppingDistance, dispatchArrivalRadius))
                    continue;

                agent.isStopped = true;
                PlaySquadArrivalAnimation(unit, squad.attackOnArrival);
                squad.arrivedUnits.Add(unit);
                if (!squad.arrivalMessageLogged && squad.infectionTarget != null && squad.infectionTarget.infection != null)
                {
                    squad.arrivalMessageLogged = true;
                    LogWbcSquadMessage($"WBC squad arrived at {squad.infectionTarget.infection.displayName}.", ConsoleLogUI.LogType.Success);
                    BeginInfectionResolution(squad.infectionTarget);
                }
                if (squad.attackOnArrival)
                    squad.combatStarted = true;
            }

            if (keepAliveUntilResolved && squad.infectionTarget != null && squad.infectionTarget.isRemoving)
            {
                DespawnWbcSquad(squadIndex);
                continue;
            }

            if (!keepAliveUntilResolved && squad.lifetimeRemaining <= 0f)
                DespawnWbcSquad(squadIndex);
        }
    }
    private void UpdateInfectionCombat()
    {
        float gameplayDeltaTime = GameplaySpeed.DeltaTime;
        if (gameplayDeltaTime <= 0f)
            return;

        DifficultyStats stats = DifficultySettings.CurrentStats;
        for (int squadIndex = dispatchedWbcSquads.Count - 1; squadIndex >= 0; squadIndex--)
        {
            DispatchedWbcSquad squad = dispatchedWbcSquads[squadIndex];
            InfectionMarker marker = squad.infectionTarget;
            if (!squad.combatStarted || marker == null || marker.isRemoving)
                continue;
            if (marker.isContained)
            {
                FadeOutAndDespawnWbcSquad(squadIndex);
                continue;
            }
            if (marker.threatVisuals.Count == 0)
                continue;

            List<GameObject> livingWbcs = new List<GameObject>();
            foreach (GameObject unit in squad.units)
            {
                if (unit == null || !unit.activeInHierarchy)
                    continue;

                Health health = unit.GetComponent<Health>();
                if (health != null && !health.IsDead)
                    livingWbcs.Add(unit);
            }

            List<GameObject> livingPathogens = new List<GameObject>();
            foreach (GameObject pathogen in marker.threatVisuals)
            {
                if (pathogen == null || !pathogen.activeInHierarchy)
                    continue;

                Health health = pathogen.GetComponent<Health>();
                if (health != null && !health.IsDead)
                    livingPathogens.Add(pathogen);
            }

            if (livingPathogens.Count == 0)
            {
                if (!squad.resolutionTriggered)
                {
                    squad.resolutionTriggered = true;
                    BeginInfectionResolution(marker);
                    FadeOutAndDespawnWbcSquad(squadIndex);
                }
                continue;
            }

            if (livingWbcs.Count == 0)
            {
                if (!squad.wipeWellnessPenaltyApplied)
                {
                    squad.wipeWellnessPenaltyApplied = true;
                    ApplyWellnessChange($"WBC squad overwhelmed by {marker.infection.displayName}", -wellnessLossOnSquadWipe);
                }
                LogWbcSquadMessage($"WBC squad was overwhelmed by {marker.infection.displayName}.", ConsoleLogUI.LogType.Warning);
                dispatchedWbcSquads.RemoveAt(squadIndex);
                LogSquadSlotFreed();
                continue;
            }

            foreach (GameObject wbc in livingWbcs)
            {
                GameObject nearestPathogen = FindNearestLivingUnit(livingPathogens, wbc.transform.position, out float distance);
                if (nearestPathogen == null || distance > combatDetectionRange)
                    continue;

                Health wbcHealth = wbc.GetComponent<Health>();
                if (distance <= combatMeleeRange)
                {
                    float multiplier = marker.infection.pathogenType == InfectionPathogenType.Viral
                        ? stats.wbcDamageMultiplierVsVirus
                        : stats.wbcDamageMultiplierVsBacteria;
                    TryApplyCombatHit(wbc, nearestPathogen, wbcHealth, stats.wbcAttack * multiplier, stats.attackIntervalSeconds);
                }
                else
                {
                    NavMeshAgent agent = wbc.GetComponent<NavMeshAgent>();
                    if (agent != null && agent.enabled)
                    {
                        if (agent.isOnNavMesh)
                        {
                            agent.ResetPath();
                            agent.isStopped = true;
                        }
                        agent.enabled = false;
                    }
                    wbc.transform.position = Vector3.MoveTowards(
                        wbc.transform.position,
                        nearestPathogen.transform.position,
                        wbcCombatMoveSpeed * gameplayDeltaTime);
                }
            }

            foreach (GameObject pathogen in livingPathogens)
            {
                GameObject nearestWbc = FindNearestLivingUnit(livingWbcs, pathogen.transform.position, out float distance);
                if (nearestWbc == null || distance > combatDetectionRange || distance > combatMeleeRange)
                    continue;

                float pathogenDamage = marker.infection.pathogenType == InfectionPathogenType.Viral
                    ? stats.virusAttack
                    : stats.bacteriaAttack;
                TryApplyCombatHit(pathogen, nearestWbc, pathogen.GetComponent<Health>(), pathogenDamage, stats.attackIntervalSeconds);
            }
        }
    }

    private GameObject FindNearestLivingUnit(List<GameObject> candidates, Vector3 origin, out float nearestDistance)
    {
        GameObject nearest = null;
        nearestDistance = float.PositiveInfinity;
        foreach (GameObject candidate in candidates)
        {
            if (candidate == null)
                continue;

            Health health = candidate.GetComponent<Health>();
            if (health == null || health.IsDead)
                continue;

            float distance = Vector3.Distance(origin, candidate.transform.position);
            if (distance >= nearestDistance)
                continue;

            nearestDistance = distance;
            nearest = candidate;
        }

        return nearest;
    }

    private void TryApplyCombatHit(GameObject attacker, GameObject target, Health attackerHealth, float damage, float attackInterval)
    {
        if (attacker == null || target == null || attackerHealth == null || damage <= 0f || !attackerHealth.TryStartAttack(attackInterval))
            return;

        Health targetHealth = target.GetComponent<Health>();
        if (targetHealth == null || targetHealth.IsDead)
            return;

        targetHealth.TakeDamage(damage);
        if (verboseCombatLogging)
            Debug.Log($"[Combat] {attacker.name} hit {target.name} for {damage:0.##} damage; remaining HP={targetHealth.CurrentHp:0.##}.", this);
    }

    private void FadeOutAndDespawnWbcSquad(int squadIndex)
    {
        if (squadIndex < 0 || squadIndex >= dispatchedWbcSquads.Count)
            return;

        DispatchedWbcSquad squad = dispatchedWbcSquads[squadIndex];
        foreach (GameObject unit in squad.units)
        {
            if (unit == null)
                continue;

            Health health = unit.GetComponent<Health>();
            if (health != null)
                health.FadeOutAndRemove();
            else
                unit.SetActive(false);
        }
        dispatchedWbcSquads.RemoveAt(squadIndex);
        LogSquadSlotFreed();
    }



    private void BeginInfectionResolution(InfectionMarker marker)
    {
        if (marker == null || marker.isRemoving || marker.isContained || marker.resolutionPending || marker.infection == null)
            return;
        if (marker.infection.correctResponder != InfectionCorrectResponder.WBC)
            return;

        if (marker.infection.pathogenType == InfectionPathogenType.Bacterial)
        {
            marker.resolutionPending = true;
            StartCoroutine(ResolveBacterialInfection(marker));
            return;
        }

        if (marker.infection.pathogenType != InfectionPathogenType.Viral)
            return;

        marker.infection = CloneInfectionData(marker.infection);
        int reducedSeverity = Mathf.Max((int)InfectionSeverityStage.Mild,
            (int)marker.infection.severityStage - containedSeverityDropSteps);
        marker.infection.severityStage = (InfectionSeverityStage)reducedSeverity;
        marker.isContained = true;
        marker.containedResolveDay = (dayCounter != null ? dayCounter.CurrentDay : 0) + containedSelfResolveDays;
        marker.markerImage.color = containedMarkerColor;
        marker.markerSize = GetMarkerSize(marker.infection.severityStage);
        marker.markerRect.sizeDelta = marker.markerSize;
        RepositionInfectionDiamonds();
        LogWbcSquadMessage($"WBC contained {marker.infection.displayName}, but the virus is hiding inside cells. Adaptive response is building.", ConsoleLogUI.LogType.Warning);
        ApplyWellnessGainOnResolve(marker);
    }

    private IEnumerator ResolveBacterialInfection(InfectionMarker marker)
    {
        if (useFixedDurationClash)
            yield return GameplaySpeed.WaitForGameplaySeconds(bacterialClashDurationSeconds);
        if (marker == null || marker.isRemoving || !activeInfectionMarkers.Contains(marker))
            yield break;

        float wellnessGain = GetBacterialWellnessGain(marker.infection.severityStage);
        ApplyWellnessChange($"WBC cleared {marker.infection.displayName}", wellnessGain);

        ApplyWellnessGainOnResolve(marker);
        LogWbcSquadMessage($"Phagocytosis: WBC engulfed {marker.infection.displayName}. Infection cleared.", ConsoleLogUI.LogType.Success);
        BeginRemovingInfectionMarker(marker);
    }

    private void ApplyWellnessGainOnResolve(InfectionMarker marker)
    {
        if (marker == null || marker.wellnessGainAwardedOnResolve || marker.infection == null || WellnessManager.Instance == null)
            return;

        marker.wellnessGainAwardedOnResolve = true;
        ApplyWellnessChange($"{marker.infection.displayName} cleared", wellnessGainOnResolve);
    }

    private float ApplyWellnessChange(string reason, float requestedDelta)
    {
        WellnessManager wellnessManager = WellnessManager.Instance;
        if (wellnessManager == null)
            return 0f;

        float before = wellnessManager.CurrentWellness;
        float maximum = Mathf.Min(100f, wellnessManager.MaxWellness);
        float target = Mathf.Clamp(before + requestedDelta, 0f, maximum);
        float clampedDelta = target - before;
        wellnessManager.ApplyInfectionResolutionDelta(reason, clampedDelta);
        float after = wellnessManager.CurrentWellness;
        float actualDelta = after - before;
        string amountText = actualDelta >= 0f
            ? $"+{actualDelta:0.##}"
            : $"{actualDelta:0.##}";
        Debug.Log($"Wellness {amountText} ({reason}): {before:0.##} -> {after:0.##}", this);
        return actualDelta;
    }

    private float GetBacterialWellnessGain(InfectionSeverityStage severityStage)
    {
        switch (severityStage)
        {
            case InfectionSeverityStage.Moderate:
                return moderateBacterialWellnessGain;
            case InfectionSeverityStage.Severe:
                return severeBacterialWellnessGain;
            default:
                return mildBacterialWellnessGain;
        }
    }

    private static InfectionData CloneInfectionData(InfectionData source)
    {
        return new InfectionData
        {
            displayName = source.displayName,
            pathogenType = source.pathogenType,
            severityStage = source.severityStage,
            entryCause = source.entryCause,
            correctResponder = source.correctResponder,
            preferredBodyParts = source.preferredBodyParts != null
                ? new List<InfectionBodyPartGroup>(source.preferredBodyParts)
                : new List<InfectionBodyPartGroup>(),
            daysUntreated = source.daysUntreated
        };
    }

    private void UpdateContainedInfections(int currentDay)
    {
        for (int index = activeInfectionMarkers.Count - 1; index >= 0; index--)
        {
            InfectionMarker marker = activeInfectionMarkers[index];
            if (marker == null || !marker.isContained || marker.isRemoving)
                continue;

            ApplyWellnessChange($"Contained infection {marker.infection.displayName}", -containedDailyWellnessCost);

            if (currentDay < marker.containedResolveDay)
                continue;

            string infectionName = marker.infection.displayName;
            BeginRemovingInfectionMarker(marker);
            LogWbcSquadMessage($"Adaptive response cleared {infectionName}.", ConsoleLogUI.LogType.Success);
        }
    }

    private void ApplyDailyUnresolvedInfectionCosts()
    {
        for (int index = 0; index < activeInfectionMarkers.Count; index++)
        {
            InfectionMarker marker = activeInfectionMarkers[index];
            if (marker == null || marker.isRemoving || marker.infection == null)
                continue;

            marker.unresolvedDays++;
            float dailyCost = GetDailyInfectionWellnessCost(marker.infection.severityStage);
            ApplyWellnessChange($"{marker.infection.displayName} unresolved", -dailyCost);

            if (marker.unresolvedDays >= 2)
                ApplyWellnessChange($"{marker.infection.displayName} unresolved 2+ days", -additionalUnresolvedDailyWellnessCost);
        }
    }

    private float GetDailyInfectionWellnessCost(InfectionSeverityStage severityStage)
    {
        switch (severityStage)
        {
            case InfectionSeverityStage.Moderate:
                return moderateDailyInfectionWellnessCost;
            case InfectionSeverityStage.Severe:
                return severeDailyInfectionWellnessCost;
            default:
                return mildDailyInfectionWellnessCost;
        }
    }

    private static void PlaySquadArrivalAnimation(GameObject unit, bool attackOnArrival)
    {
        Animator animator = unit.GetComponentInChildren<Animator>();
        if (animator == null)
            return;

        animator.SetBool("IsMoving", false);
        if (attackOnArrival)
            animator.SetTrigger("Attack");
    }

    private void DespawnWbcSquad(int squadIndex)
    {
        DispatchedWbcSquad squad = dispatchedWbcSquads[squadIndex];
        foreach (GameObject unit in squad.units)
        {
            if (unit == null)
                continue;

            WbcIdleHighlight highlight = unit.GetComponent<WbcIdleHighlight>();
            if (highlight != null)
                highlight.SetHighlighted(false);
            NavMeshAgent agent = unit.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.isStopped = true;
            }
            unit.SetActive(false);
        }
        dispatchedWbcSquads.RemoveAt(squadIndex);
        LogSquadSlotFreed();
    }

    private void RefreshWbcSquadHud()
    {
        if (squadCountLabel != null)
            squadCountLabel.text = $"Squads {CountActiveSquads()}/{Mathf.Max(1, DifficultySettings.CurrentStats.maxActiveSquads)}";

        if (headshotButton != null)
            headshotButton.interactable = enableWbcDispatch && idleSquad == null && headshotCooldownRemaining <= 0f;

        if (headshotCooldownFill != null)
        {
            headshotCooldownFill.fillAmount = headshotCooldownSeconds > 0f
                ? Mathf.Clamp01(headshotCooldownRemaining / headshotCooldownSeconds)
                : 0f;
            headshotCooldownFill.enabled = headshotCooldownRemaining > 0f;
        }

        if (headshotCooldownLabel != null)
        {
            headshotCooldownLabel.text = headshotCooldownRemaining > 0f
                ? Mathf.CeilToInt(headshotCooldownRemaining).ToString()
                : string.Empty;
            headshotCooldownLabel.enabled = headshotCooldownRemaining > 0f;
        }
    }

    private void LogWbcSquadMessage(string message, ConsoleLogUI.LogType logType)
    {
        Debug.Log(message);
        if (ConsoleLogUI.Instance != null)
            ConsoleLogUI.Instance.Log(message, logType);
    }

    private Texture2D ResolveHeadshotTexture()
    {
        if (wbcHeadshotTexture != null)
            return wbcHeadshotTexture;

        if (resolvedHeadshotTexture == null && !string.IsNullOrWhiteSpace(wbcHeadshotResourcePath))
            resolvedHeadshotTexture = Resources.Load<Texture2D>(wbcHeadshotResourcePath);

        return resolvedHeadshotTexture;
    }

    private static TextMeshProUGUI CreateSquadHudText(string objectName, Transform parent, string value, float fontSize, FontStyles fontStyle, Color color)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.text = value;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    private void RepositionInfectionDiamonds()
    {
        if (infectionRowRect == null)
            return;

        List<InfectionMarker> visibleMarkers = new List<InfectionMarker>();
        foreach (InfectionMarker marker in activeInfectionMarkers)
        {
            if (marker != null && !marker.isRemoving)
                visibleMarkers.Add(marker);
        }

        visibleMarkers.Sort((first, second) =>
        {
            int severityOrder = second.infection.severityStage.CompareTo(first.infection.severityStage);
            return severityOrder != 0 ? severityOrder : first.spawnOrder.CompareTo(second.spawnOrder);
        });

        int visibleCount = Mathf.Min(visibleMarkers.Count, DefaultMaximumActiveInfections);
        int hiddenCount = Mathf.Max(0, visibleMarkers.Count - visibleCount);
        float totalWidth = hiddenCount > 0 ? 42f : 0f;
        for (int index = 0; index < visibleCount; index++)
        {
            if (index > 0 || hiddenCount > 0)
                totalWidth += markerSpacing;
            totalWidth += visibleMarkers[index].markerSize.x;
        }

        float cursor = -totalWidth * 0.5f;
        for (int index = 0; index < activeInfectionMarkers.Count; index++)
        {
            InfectionMarker marker = activeInfectionMarkers[index];
            bool shouldShow = visibleMarkers.IndexOf(marker) < visibleCount;
            if (!shouldShow)
            {
                marker.markerButton.gameObject.SetActive(false);
                continue;
            }

            marker.markerButton.gameObject.SetActive(true);
            marker.markerRect.anchorMin = new Vector2(0.5f, 0.5f);
            marker.markerRect.anchorMax = new Vector2(0.5f, 0.5f);
            marker.markerRect.pivot = new Vector2(0.5f, 0.5f);
            marker.markerRect.anchoredPosition = new Vector2(cursor + marker.markerSize.x * 0.5f, 0f);
            cursor += marker.markerSize.x + markerSpacing;
        }

        if (excessInfectionLabel != null)
        {
            excessInfectionLabel.text = hiddenCount > 0 ? $"+{hiddenCount}" : string.Empty;
            excessInfectionLabel.enabled = hiddenCount > 0;
            if (hiddenCount > 0)
            {
                excessInfectionLabel.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                excessInfectionLabel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                excessInfectionLabel.rectTransform.anchoredPosition = new Vector2(cursor + 21f, 0f);
            }
        }

        infectionRowRect.gameObject.SetActive(visibleCount > 0 || fadingInfectionMarkers.Count > 0);
    }

    private void UpdateInfectionMarkerAnimations()
    {
        float gameplayDeltaTime = GameplaySpeed.DeltaTime;
        foreach (InfectionMarker marker in activeInfectionMarkers)
        {
            if (marker.isRemoving)
                continue;

            if (markerPopDuration <= 0f)
            {
                marker.markerRect.localScale = Vector3.one;
                continue;
            }

            marker.animationElapsed = Mathf.Min(markerPopDuration, marker.animationElapsed + gameplayDeltaTime);
            float popProgress = marker.animationElapsed / markerPopDuration;
            float popScale = Mathf.SmoothStep(0.25f, 1f, popProgress);
            marker.markerRect.localScale = Vector3.one * popScale;
        }

        for (int index = fadingInfectionMarkers.Count - 1; index >= 0; index--)
        {
            InfectionMarker marker = fadingInfectionMarkers[index];
            marker.animationElapsed += gameplayDeltaTime;
            float fadeProgress = markerFadeDuration > 0f ? Mathf.Clamp01(marker.animationElapsed / markerFadeDuration) : 1f;
            marker.canvasGroup.alpha = 1f - fadeProgress;
            if (fadeProgress >= 1f)
            {
                marker.markerButton.gameObject.SetActive(false);
                if (marker.dispatchButton != null)
                    marker.dispatchButton.gameObject.SetActive(false);
                fadingInfectionMarkers.RemoveAt(index);
            }
        }

        if (infectionRowRect != null && activeInfectionMarkers.Count == 0 && fadingInfectionMarkers.Count == 0)
            infectionRowRect.gameObject.SetActive(false);
    }

    /// <summary>Begins fading the first active matching infection diamond without deleting its GameObject.</summary>
    public bool RemoveInfectionAt(string displayName, InfectionBodyPartGroup bodyPartGroup)
    {
        for (int index = 0; index < activeInfectionMarkers.Count; index++)
        {
            InfectionMarker marker = activeInfectionMarkers[index];
            if (marker.infection.displayName != displayName || marker.bodyPartGroup != bodyPartGroup || marker.isRemoving)
                continue;

            BeginRemovingInfectionMarker(marker);
            return true;
        }

        return false;
    }

    private void BeginRemovingInfectionMarker(InfectionMarker marker)
    {
        if (marker == null || marker.isRemoving || !activeInfectionMarkers.Contains(marker))
            return;

        marker.isRemoving = true;
        marker.animationElapsed = 0f;
        marker.markerButton.interactable = false;
        marker.markerImage.raycastTarget = false;
        marker.selectionOutline.enabled = false;
        activeInfectionMarkers.Remove(marker);
        fadingInfectionMarkers.Add(marker);
        if (selectedMarker == marker)
            selectedMarker = null;
        RepositionInfectionDiamonds();
    }



    private void HandleDayAdvanced(int currentDay)
    {
        UpdateContainedInfections(currentDay);
        ApplyDailyUnresolvedInfectionCosts();
        if (!spawnOnDayAdvance)
            return;

        List<InfectionData> validEntries = BuildValidInfectionEntries();
        if (validEntries.Count == 0 || GetValidBodyPartButtonCount() == 0)
        {
            Debug.LogWarning("[InfectionSpawner] No valid infection entries or body-part button mappings are configured.", this);
            return;
        }

        int availableInfectionSlots = Mathf.Max(0, maxActiveInfections - activeInfectionMarkers.Count);
        int spawnCount = Mathf.Min(Mathf.Clamp(eventsPerDay, MinimumEventsPerDay, MaximumEventsPerDay), availableInfectionSlots);
        for (int eventIndex = 0; eventIndex < spawnCount && activeInfectionMarkers.Count < maxActiveInfections; eventIndex++)
        {
            InfectionData infection = validEntries[UnityEngine.Random.Range(0, validEntries.Count)];
            InfectionBodyPartButtonReference bodyPartButton = SelectWeightedBodyPartButton(infection);
            if (bodyPartButton == null)
                continue;

            SpawnMarker(infection, bodyPartButton);
            LogInfection(infection, bodyPartButton);
        }
    }

    private List<InfectionData> BuildValidInfectionEntries()
    {
        List<InfectionData> validEntries = new List<InfectionData>();
        if (infectionEntries == null)
            return validEntries;

        foreach (InfectionData infection in infectionEntries)
        {
            if (infection != null && !string.IsNullOrWhiteSpace(infection.displayName) && !string.IsNullOrWhiteSpace(infection.entryCause))
                validEntries.Add(infection);
        }

        return validEntries;
    }

    private int GetValidBodyPartButtonCount()
    {
        int count = 0;
        if (bodyPartButtons == null)
            return count;

        foreach (InfectionBodyPartButtonReference mapping in bodyPartButtons)
        {
            if (mapping != null && mapping.button != null)
                count++;
        }

        return count;
    }

    private InfectionBodyPartButtonReference SelectWeightedBodyPartButton(InfectionData infection)
    {
        if (bodyPartButtons == null)
            return null;

        float totalWeight = 0f;
        foreach (InfectionBodyPartButtonReference mapping in bodyPartButtons)
        {
            if (mapping != null && mapping.button != null)
                totalWeight += GetBodyPartWeight(infection, mapping);
        }

        if (totalWeight <= 0f)
            return SelectRandomMappedBodyPartButton();

        float roll = UnityEngine.Random.value * totalWeight;
        foreach (InfectionBodyPartButtonReference mapping in bodyPartButtons)
        {
            if (mapping == null || mapping.button == null)
                continue;

            roll -= GetBodyPartWeight(infection, mapping);
            if (roll <= 0f)
                return mapping;
        }

        return SelectRandomMappedBodyPartButton();
    }

    private float GetBodyPartWeight(InfectionData infection, InfectionBodyPartButtonReference mapping)
    {
        bool preferred = infection.preferredBodyParts != null && infection.preferredBodyParts.Contains(mapping.bodyPartGroup);
        float weight = preferred ? preferredBodyPartWeight : otherBodyPartWeight;
        return Mathf.Max(0f, weight);
    }

    private InfectionBodyPartButtonReference SelectRandomMappedBodyPartButton()
    {
        List<InfectionBodyPartButtonReference> validMappings = new List<InfectionBodyPartButtonReference>();
        if (bodyPartButtons != null)
        {
            foreach (InfectionBodyPartButtonReference mapping in bodyPartButtons)
            {
                if (mapping != null && mapping.button != null)
                    validMappings.Add(mapping);
            }
        }

        return validMappings.Count == 0 ? null : validMappings[UnityEngine.Random.Range(0, validMappings.Count)];
    }

    private Vector3 ResolveInitialInfectionWorldPosition(InfectionBodyPartButtonReference bodyPartButton, out int spawnPointIndex)
    {
        spawnPointIndex = -1;
        VesselSpawnPointCache vesselSpawnPointCache = FindFirstObjectByType<VesselSpawnPointCache>();
        if (vesselSpawnPointCache == null || vesselSpawnPointCache.WorldSpawnPoints.Count == 0)
            return GetBodyPartAnchorPosition(bodyPartButton);

        IReadOnlyList<Vector3> spawnPoints = vesselSpawnPointCache.WorldSpawnPoints;
        spawnPointIndex = UnityEngine.Random.Range(0, spawnPoints.Count);
        return spawnPoints[spawnPointIndex];
    }

    private Vector3 GetInfectionWorldPosition(InfectionMarker marker)
    {
        if (marker == null)
            return GetSquadReferencePosition();

        VesselSpawnPointCache vesselSpawnPointCache = FindFirstObjectByType<VesselSpawnPointCache>();
        if (vesselSpawnPointCache != null && marker.vesselSpawnPointIndex >= 0)
        {
            IReadOnlyList<Vector3> spawnPoints = vesselSpawnPointCache.WorldSpawnPoints;
            if (marker.vesselSpawnPointIndex < spawnPoints.Count)
                marker.worldPosition = spawnPoints[marker.vesselSpawnPointIndex];
        }

        return marker.worldPosition;
    }


    private void SpawnMarker(InfectionData infection, InfectionBodyPartButtonReference bodyPartButton)
    {
        if (hudCanvas == null || infectionRowRect == null)
        {
            Debug.LogWarning("[InfectionSpawner] HUD Canvas or infection row is missing; infection diamond could not be created.", this);
            return;
        }

        GameObject markerObject = new GameObject($"Infection Diamond - {infection.displayName}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(Outline), typeof(CanvasGroup));
        markerObject.transform.SetParent(infectionRowRect, false);
        markerObject.transform.SetAsLastSibling();
        Vector3 infectionWorldPosition = ResolveInitialInfectionWorldPosition(bodyPartButton, out int vesselSpawnPointIndex);

        InfectionMarker marker = new InfectionMarker
        {
            infection = infection,
            bodyPartMapping = bodyPartButton,
            bodyPartName = GetBodyPartDisplayName(bodyPartButton.button),
            bodyPartGroup = bodyPartButton.bodyPartGroup,
            worldPosition = infectionWorldPosition,
            vesselSpawnPointIndex = vesselSpawnPointIndex,
            markerButton = markerObject.GetComponent<Button>(),
            markerImage = markerObject.GetComponent<Image>(),
            selectionOutline = markerObject.GetComponent<Outline>(),
            markerRect = markerObject.GetComponent<RectTransform>(),
            canvas = hudCanvas,
            markerSize = GetMarkerSize(infection.severityStage),
            canvasGroup = markerObject.GetComponent<CanvasGroup>(),
            spawnOrder = nextInfectionSpawnOrder++,
            unresolvedDays = Mathf.Max(0, infection.daysUntreated)
        };

        marker.markerRect.anchorMin = new Vector2(1f, 0f);
        marker.markerRect.anchorMax = new Vector2(1f, 0f);
        marker.markerRect.pivot = new Vector2(1f, 0f);
        marker.markerRect.sizeDelta = marker.markerSize;
        marker.markerRect.localRotation = Quaternion.Euler(0f, 0f, 45f);
        marker.markerRect.localScale = Vector3.one * 0.25f;

        marker.markerImage.sprite = ResolveMarkerSprite();
        marker.markerImage.color = GetPathogenColor(infection.pathogenType);
        marker.markerImage.raycastTarget = true;
        marker.markerImage.preserveAspect = true;

        marker.selectionOutline.effectColor = selectionOutlineColor;
        marker.selectionOutline.effectDistance = new Vector2(selectionOutlineThickness, -selectionOutlineThickness);
        marker.selectionOutline.useGraphicAlpha = true;
        marker.selectionOutline.enabled = false;

        marker.markerButton.targetGraphic = marker.markerImage;
        marker.markerButton.transition = Selectable.Transition.None;
        marker.markerButton.onClick.AddListener(() => ToggleSelectMarker(marker));

        activeInfectionMarkers.Add(marker);
        SpawnInfectionThreatVisuals(marker);
        if (bodyPartButton.button != null)
        {
            if (!markersByBodyPartButton.TryGetValue(bodyPartButton.button, out List<InfectionMarker> markers))
            {
                markers = new List<InfectionMarker>();
                markersByBodyPartButton.Add(bodyPartButton.button, markers);
            }
            markers.Add(marker);
        }

        if (enableLegacyAvatarClickDispatch)
            CreateDispatchAvatar(marker);
        RepositionInfectionDiamonds();
        infectionRowRect.gameObject.SetActive(true);
    }
    private void SpawnInfectionThreatVisuals(InfectionMarker marker)
    {
        if (marker == null || threatVisualSpawnCount <= 0)
            return;

        if (bacteriaPrefab == null)
        {
            Debug.LogWarning($"[InfectionSpawner] Bacteria prefab is missing; skipping threat visuals for {marker.infection.displayName}.", this);
            return;
        }

        for (int index = 0; index < threatVisualSpawnCount; index++)
        {
            Vector3 offset = UnityEngine.Random.insideUnitSphere * threatVisualClusterRadius;
            GameObject visual = Instantiate(bacteriaPrefab, marker.worldPosition + offset, Quaternion.identity);

            BacteriaAgent bacteriaAgent = visual.GetComponentInChildren<BacteriaAgent>(true);
            if (bacteriaAgent != null)
                bacteriaAgent.enabled = false;

            NavMeshAgent navMeshAgent = visual.GetComponentInChildren<NavMeshAgent>(true);
            if (navMeshAgent != null)
                navMeshAgent.enabled = false;

            Collider[] colliders = visual.GetComponentsInChildren<Collider>(true);
            foreach (Collider visualCollider in colliders)
            {
                if (visualCollider != null)
                    visualCollider.enabled = false;
            }

            Health health = visual.GetComponent<Health>();
            if (health == null)
                health = visual.AddComponent<Health>();
            DifficultyStats stats = DifficultySettings.CurrentStats;
            float pathogenMaxHp = marker.infection.pathogenType == InfectionPathogenType.Viral
                ? stats.virusHp
                : stats.bacteriaHp;
            health.SetMaxHp(pathogenMaxHp);
            health.SetPathogenType(marker.infection.pathogenType);

            marker.threatVisuals.Add(visual);
        }
    }

    private void UpdateInfectionThreatVisuals()
    {
        foreach (InfectionMarker marker in activeInfectionMarkers)
        {
            if (marker != null && marker.isContained)
                DespawnInfectionThreatVisuals(marker);
        }

        foreach (InfectionMarker marker in fadingInfectionMarkers)
        {
            if (marker != null && marker.isRemoving)
                DespawnInfectionThreatVisuals(marker);
        }
    }

    private void DespawnInfectionThreatVisuals(InfectionMarker marker)
    {
        if (marker == null || marker.threatVisualsDespawned)
            return;

        marker.threatVisualsDespawned = true;
        foreach (GameObject visual in marker.threatVisuals)
        {
            if (visual != null)
                Destroy(visual);
        }
        marker.threatVisuals.Clear();
    }



    private void CreateDispatchAvatar(InfectionMarker marker)
    {
        GameObject avatarObject = new GameObject($"WBC Dispatch - {marker.infection.displayName}", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage), typeof(Button));
        avatarObject.transform.SetParent(marker.canvas.transform, false);
        avatarObject.transform.SetAsLastSibling();

        RectTransform avatarRect = avatarObject.GetComponent<RectTransform>();
        avatarRect.anchorMin = new Vector2(0.5f, 0.5f);
        avatarRect.anchorMax = new Vector2(0.5f, 0.5f);
        avatarRect.pivot = new Vector2(0.5f, 0.5f);
        avatarRect.sizeDelta = dispatchAvatarSize;

        marker.dispatchImage = avatarObject.GetComponent<RawImage>();
        marker.dispatchImage.texture = ResolveWbcAvatarTexture();
        marker.dispatchImage.color = Color.white;
        marker.dispatchImage.raycastTarget = true;

        marker.dispatchButton = avatarObject.GetComponent<Button>();
        marker.dispatchButton.targetGraphic = marker.dispatchImage;
        marker.dispatchButton.transition = Selectable.Transition.ColorTint;
        marker.dispatchButton.onClick.AddListener(() => DispatchSelectedWbc(marker));

        GameObject countLabelObject = new GameObject("WBC Active Count", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        countLabelObject.transform.SetParent(avatarObject.transform, false);
        RectTransform countRect = countLabelObject.GetComponent<RectTransform>();
        countRect.anchorMin = new Vector2(0.5f, 0f);
        countRect.anchorMax = new Vector2(0.5f, 0f);
        countRect.pivot = new Vector2(0.5f, 1f);
        countRect.anchoredPosition = new Vector2(0f, -2f);
        countRect.sizeDelta = new Vector2(92f, 20f);

        marker.dispatchCountLabel = countLabelObject.GetComponent<TextMeshProUGUI>();
        marker.dispatchCountLabel.font = TMP_Settings.defaultFontAsset;
        marker.dispatchCountLabel.fontSize = 12f;
        marker.dispatchCountLabel.fontStyle = FontStyles.Bold;
        marker.dispatchCountLabel.alignment = TextAlignmentOptions.Center;
        marker.dispatchCountLabel.color = Color.white;
        marker.dispatchCountLabel.textWrappingMode = TextWrappingModes.NoWrap;
        marker.dispatchCountLabel.raycastTarget = false;
        avatarObject.SetActive(false);
    }

    private void UpdateMarkerPositions()
    {
        foreach (KeyValuePair<Button, List<InfectionMarker>> entry in markersByBodyPartButton)
        {
            if (entry.Key == null || entry.Value.Count == 0 || entry.Value[0].canvas == null)
                continue;

            RectTransform targetRect = entry.Key.transform as RectTransform;
            RectTransform canvasRect = entry.Value[0].canvas.transform as RectTransform;
            if (targetRect == null || canvasRect == null)
                continue;

            Camera canvasCamera = entry.Value[0].canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : entry.Value[0].canvas.worldCamera;
            Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(canvasCamera, targetRect.position);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, canvasCamera, out Vector2 localPosition))
                continue;

            foreach (InfectionMarker marker in entry.Value)
                marker.canvasLocalBodyPartPosition = localPosition;

            RepositionMarkers(entry.Value);
        }
    }


    private void RepositionMarkers(List<InfectionMarker> markers)
    {
        int rowStart = 0;
        float verticalRowOffset = 0f;
        while (rowStart < markers.Count)
        {
            int rowEnd = rowStart;
            float rowWidth = 0f;
            float rowHeight = 0f;
            while (rowEnd < markers.Count)
            {
                InfectionMarker candidate = markers[rowEnd];
                float candidateWidth = rowWidth + (rowEnd > rowStart ? markerSpacing : 0f) + candidate.markerSize.x;
                if (rowEnd > rowStart && candidateWidth > maximumMarkerRowWidth)
                    break;

                rowWidth = candidateWidth;
                rowHeight = Mathf.Max(rowHeight, candidate.markerSize.y);
                rowEnd++;
            }

            float cursor = -rowWidth * 0.5f;
            for (int markerIndex = rowStart; markerIndex < rowEnd; markerIndex++)
            {
                InfectionMarker marker = markers[markerIndex];
                float markerWidth = marker.markerSize.x;
                marker.markerRect.anchoredPosition = marker.canvasLocalBodyPartPosition + markerOffset +
                    new Vector2(cursor + markerWidth * 0.5f, -verticalRowOffset);
                cursor += markerWidth + markerSpacing;
                if (marker.dispatchButton != null && marker.dispatchButton.transform is RectTransform avatarRect)
                    avatarRect.anchoredPosition = marker.markerRect.anchoredPosition + dispatchAvatarOffset;
            }

            verticalRowOffset += rowHeight + markerSpacing;
            rowStart = rowEnd;
        }
    }

    private Vector2 GetMarkerSize(InfectionSeverityStage severityStage)
    {
        switch (severityStage)
        {
            case InfectionSeverityStage.Moderate:
                return moderateMarkerSize;
            case InfectionSeverityStage.Severe:
                return severeMarkerSize;
            default:
                return mildMarkerSize;
        }
    }

    private Color GetPathogenColor(InfectionPathogenType pathogenType)
    {
        return pathogenType == InfectionPathogenType.Viral ? viralColor : bacterialColor;
    }

    private Sprite ResolveMarkerSprite()
    {
        if (markerSprite != null)
            return markerSprite;
        if (resolvedMarkerSprite == null && !string.IsNullOrWhiteSpace(markerSpriteResourcePath))
            resolvedMarkerSprite = Resources.Load<Sprite>(markerSpriteResourcePath);
        return resolvedMarkerSprite;
    }

    private Texture2D ResolveWbcAvatarTexture()
    {
        if (wbcAvatarTexture != null)
            return wbcAvatarTexture;
        if (resolvedWbcAvatarTexture == null && !string.IsNullOrWhiteSpace(wbcAvatarResourcePath))
            resolvedWbcAvatarTexture = Resources.Load<Texture2D>(wbcAvatarResourcePath);
        return resolvedWbcAvatarTexture != null ? resolvedWbcAvatarTexture : ResolveMarkerSprite()?.texture;
    }

    private void CreateSelectedInfectionPopup()
    {
        selectedInfectionPopupObject = new GameObject("Selected Infection Popup", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        RectTransform popupRect = selectedInfectionPopupObject.GetComponent<RectTransform>();
        popupRect.sizeDelta = InfectionPopupSize;
        selectedInfectionPopupObject.transform.localScale = Vector3.one * 0.0035f;

        Canvas popupCanvas = selectedInfectionPopupObject.GetComponent<Canvas>();
        popupCanvas.renderMode = RenderMode.WorldSpace;
        popupCanvas.worldCamera = gameplayCamera != null ? gameplayCamera : Camera.main;
        popupCanvas.overrideSorting = true;
        popupCanvas.sortingOrder = 30;

        selectedInfectionPopupCanvasGroup = selectedInfectionPopupObject.GetComponent<CanvasGroup>();
        selectedInfectionPopupCanvasGroup.alpha = 0f;
        selectedInfectionPopupCanvasGroup.interactable = false;
        selectedInfectionPopupCanvasGroup.blocksRaycasts = false;

        GameObject backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        backgroundObject.transform.SetParent(selectedInfectionPopupObject.transform, false);
        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        Image backgroundImage = backgroundObject.GetComponent<Image>();
        backgroundImage.color = squadHudPanelColor;
        backgroundImage.raycastTarget = false;

        selectedInfectionCategoryLabel = CreateInfectionPopupText(
            "Category", new Vector2(0f, 0.55f), Vector2.one, new Vector2(12f, 0f), new Vector2(-12f, -7f),
            19f, FontStyles.Bold, Color.white, TextAlignmentOptions.Left);
        selectedInfectionCategoryLabel.transform.SetParent(selectedInfectionPopupObject.transform, false);

        selectedInfectionDetailsLabel = CreateInfectionPopupText(
            "Pathogen Details", Vector2.zero, new Vector2(1f, 0.62f), new Vector2(12f, 5f), new Vector2(-12f, 0f),
            15f, FontStyles.Normal, Color.white, TextAlignmentOptions.Left);
        selectedInfectionDetailsLabel.transform.SetParent(selectedInfectionPopupObject.transform, false);

        selectedInfectionPopupObject.SetActive(false);
    }

    private static TextMeshProUGUI CreateInfectionPopupText(string objectName, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax, float fontSize, FontStyles fontStyle, Color color, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = anchorMin;
        textRect.anchorMax = anchorMax;
        textRect.offsetMin = offsetMin;
        textRect.offsetMax = offsetMax;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    private void UpdateSelectedInfectionPopup()
    {
        InfectionMarker requestedMarker = selectedMarker != null && !selectedMarker.isRemoving &&
            activeInfectionMarkers.Contains(selectedMarker) ? selectedMarker : null;
        if (requestedMarker != null && selectedInfectionPopupObject == null)
            CreateSelectedInfectionPopup();
        if (selectedInfectionPopupObject == null || selectedInfectionPopupCanvasGroup == null)
            return;

        float fadeStep = GameplaySpeed.DeltaTime / InfectionPopupFadeDuration;
        if (selectedInfectionPopupMarker != requestedMarker)
        {
            if (selectedInfectionPopupMarker != null && selectedInfectionPopupCanvasGroup.alpha > 0f)
            {
                selectedInfectionPopupCanvasGroup.alpha = Mathf.MoveTowards(selectedInfectionPopupCanvasGroup.alpha, 0f, fadeStep);
                if (selectedInfectionPopupCanvasGroup.alpha > 0f)
                    return;
            }

            selectedInfectionPopupMarker = requestedMarker;
            if (selectedInfectionPopupMarker != null)
            {
                selectedInfectionPopupObject.SetActive(true);
                selectedInfectionCategoryLabel.text = selectedInfectionPopupMarker.infection.pathogenType == InfectionPathogenType.Viral
                    ? "Viral infection"
                    : "Bacterial infection";
                selectedInfectionCategoryLabel.color = GetPathogenColor(selectedInfectionPopupMarker.infection.pathogenType);
            }
        }

        if (selectedInfectionPopupMarker == null)
        {
            selectedInfectionPopupCanvasGroup.alpha = Mathf.MoveTowards(selectedInfectionPopupCanvasGroup.alpha, 0f, fadeStep);
            if (selectedInfectionPopupCanvasGroup.alpha <= 0f)
                selectedInfectionPopupObject.SetActive(false);
            return;
        }

        int remainingPathogens = CountRemainingPathogens(selectedInfectionPopupMarker);
        selectedInfectionDetailsLabel.text = $"{selectedInfectionPopupMarker.infection.displayName}\nPathogens remaining: {remainingPathogens}";
        selectedInfectionPopupCanvasGroup.alpha = Mathf.MoveTowards(selectedInfectionPopupCanvasGroup.alpha, 1f, fadeStep);
    }

    private int CountRemainingPathogens(InfectionMarker marker)
    {
        if (marker == null || marker.threatVisuals == null)
            return 0;

        int count = 0;
        foreach (GameObject visual in marker.threatVisuals)
        {
            if (IsPathogenVisualAlive(visual))
                count++;
        }
        return count;
    }

    private static bool IsPathogenVisualAlive(GameObject visual)
    {
        if (visual == null || !visual.activeInHierarchy)
            return false;

        Health health = visual.GetComponent<Health>();
        return health == null || !health.IsDead;
    }

    private void PositionSelectedInfectionPopup(InfectionMarker marker, Camera popupCamera)
    {
        if (marker == null || selectedInfectionPopupObject == null || popupCamera == null)
            return;

        Vector3 pathogenCenter = Vector3.zero;
        int pathogenCount = 0;
        foreach (GameObject visual in marker.threatVisuals)
        {
            if (!IsPathogenVisualAlive(visual))
                continue;

            pathogenCenter += visual.transform.position;
            pathogenCount++;
        }

        Vector3 anchor = pathogenCount > 0
            ? pathogenCenter / pathogenCount
            : GetInfectionWorldPosition(marker);
        float clusterRadius = 0f;
        if (pathogenCount > 0)
        {
            foreach (GameObject visual in marker.threatVisuals)
            {
                if (IsPathogenVisualAlive(visual))
                    clusterRadius = Mathf.Max(clusterRadius, Vector3.Distance(anchor, visual.transform.position));
            }
        }

        Vector3 offset = popupCamera.transform.right * (0.55f + clusterRadius) +
                         popupCamera.transform.up * (0.2f + clusterRadius * 0.25f);
        Transform popupTransform = selectedInfectionPopupObject.transform;
        popupTransform.position = anchor + offset;
        popupTransform.rotation = popupCamera.transform.rotation;

        float popupScale = 0.0035f;
        if (!popupCamera.orthographic)
        {
            float distance = Vector3.Distance(popupCamera.transform.position, popupTransform.position);
            popupScale *= Mathf.Clamp(distance / InfectionPopupReferenceDistance, 0.25f, 20f);
        }
        popupScale = Mathf.Abs(popupScale);
        popupTransform.localScale = new Vector3(popupScale, popupScale, popupScale);
    }

    private void ToggleSelectMarker(InfectionMarker marker)
    {
        if (marker == null || marker.isRemoving)
            return;

        if (selectedMarker == marker)
        {
            marker.selectionOutline.enabled = false;
            if (marker.dispatchButton != null)
                marker.dispatchButton.gameObject.SetActive(false);
            selectedMarker = null;
            return;
        }

        SelectMarker(marker);
    }

    private void SelectMarker(InfectionMarker marker)
    {
        if (marker == null || marker.isRemoving || !activeInfectionMarkers.Contains(marker))
            return;

        if (selectedMarker != null && selectedMarker != marker)
        {
            selectedMarker.selectionOutline.enabled = false;
            if (selectedMarker.dispatchButton != null)
                selectedMarker.dispatchButton.gameObject.SetActive(false);
        }

        selectedMarker = marker;
        selectedMarker.selectionOutline.enabled = true;
        selectedMarker.selectionOutline.effectColor = selectionOutlineColor;
        selectedMarker.selectionOutline.effectDistance = new Vector2(selectionOutlineThickness, -selectionOutlineThickness);
        if (enableLegacyAvatarClickDispatch && selectedMarker.dispatchButton != null)
            selectedMarker.dispatchButton.gameObject.SetActive(true);
        LogSelectedInfection(marker);

        if (!enableWbcDispatch)
            return;

        if (awaitingInfectionTargetSelection)
        {
            if (idleSquad == null)
                return;

            Vector3 targetPosition = GetInfectionWorldPosition(marker);
            if (DispatchIdleSquadTo(targetPosition, marker, marker.bodyPartName))
                awaitingInfectionTargetSelection = false;
            return;
        }

        if (!useLegacySelectFirst)
            return;
        if (idleSquad == null)
        {
            LogWbcSquadMessage("Spawn WBCs first.", ConsoleLogUI.LogType.Warning);
            return;
        }

        Vector3 infectionWorldPosition = GetInfectionWorldPosition(marker);
        DispatchIdleSquadTo(infectionWorldPosition, marker, marker.bodyPartName);
    }

    public void RequestDispatchToInfection(InfectionMarker marker)
    {
        if (marker == null || marker.isRemoving || !activeInfectionMarkers.Contains(marker))
            return;
        if (!awaitingInfectionTargetSelection || idleSquad == null)
            return;

        SelectMarker(marker);
    }

    private void LogSelectedInfection(InfectionMarker marker)
    {
        string message = $"Selected infection: {marker.infection.displayName} | Cause: {marker.infection.entryCause} | " +
                         $"Stage: {marker.infection.severityStage} | Body part: {marker.bodyPartName} | Days untreated: {marker.infection.daysUntreated}";
        Debug.Log(message);
        if (ConsoleLogUI.Instance != null)
            ConsoleLogUI.Instance.Log(message, ConsoleLogUI.LogType.Info);
    }

    private void DispatchSelectedWbc(InfectionMarker marker)
    {
        if (!enableWbcDispatch || !enableLegacyAvatarClickDispatch || marker == null || marker != selectedMarker)
            return;

        EnsureDispatchSlots();
        int availableSlotIndex = FindAvailableDispatchSlot();
        if (CountActiveWbcs() + CountActiveSquadUnits() >= maxActiveWbcs || availableSlotIndex < 0)
        {
            string capacityMessage = $"WBC dispatch unavailable: {CountActiveWbcs()}/{maxActiveWbcs} active.";
            Debug.Log(capacityMessage);
            if (ConsoleLogUI.Instance != null)
                ConsoleLogUI.Instance.Log(capacityMessage, ConsoleLogUI.LogType.Warning);
            RefreshDispatchAvatar();
            return;
        }

        float cooldownDuration = Mathf.Max(perWbcCooldownSeconds, dispatchArrivalDelaySeconds);
        wbcDispatchSlots[availableSlotIndex].cooldownRemaining = cooldownDuration;
        RefreshDispatchAvatar();
        StartCoroutine(CompleteWbcArrival(marker.infection.displayName, marker.bodyPartName));
    }

    private IEnumerator CompleteWbcArrival(string infectionName, string bodyPartName)
    {
        yield return GameplaySpeed.WaitForGameplaySeconds(dispatchArrivalDelaySeconds);
        string message = $"WBC deployed to {infectionName} at {bodyPartName}";
        Debug.Log(message);
        if (ConsoleLogUI.Instance != null)
            ConsoleLogUI.Instance.Log(message, ConsoleLogUI.LogType.Success);
    }

    private void UpdateWbcCooldowns()
    {
        EnsureDispatchSlots();
        float gameplayDeltaTime = GameplaySpeed.DeltaTime;
        if (gameplayDeltaTime <= 0f)
            return;

        int activeCountBeforeUpdate = CountActiveWbcs();
        foreach (WbcDispatchSlot slot in wbcDispatchSlots)
            slot.cooldownRemaining = Mathf.Max(0f, slot.cooldownRemaining - gameplayDeltaTime);

        if (CountActiveWbcs() != activeCountBeforeUpdate)
            RefreshDispatchAvatar();
    }

    private void EnsureDispatchSlots()
    {
        int desiredSlotCount = Mathf.Max(1, maxActiveWbcs);
        while (wbcDispatchSlots.Count < desiredSlotCount)
            wbcDispatchSlots.Add(new WbcDispatchSlot());
        while (wbcDispatchSlots.Count > desiredSlotCount && wbcDispatchSlots[wbcDispatchSlots.Count - 1].cooldownRemaining <= 0f)
            wbcDispatchSlots.RemoveAt(wbcDispatchSlots.Count - 1);
    }

    private int FindAvailableDispatchSlot()
    {
        for (int index = 0; index < maxActiveWbcs && index < wbcDispatchSlots.Count; index++)
        {
            if (wbcDispatchSlots[index].cooldownRemaining <= 0f)
                return index;
        }

        return -1;
    }

    private int CountActiveWbcs()
    {
        int activeCount = 0;
        foreach (WbcDispatchSlot slot in wbcDispatchSlots)
        {
            if (slot.cooldownRemaining > 0f)
                activeCount++;
        }

        return activeCount;
    }
    private int CountActiveSquads()
    {
        return dispatchedWbcSquads.Count + (idleSquad != null ? 1 : 0);
    }

    private int GetNextAvailableSquadDeploymentIndex()
    {
        int maxActiveSquads = Mathf.Max(1, DifficultySettings.CurrentStats.maxActiveSquads);
        bool[] occupiedSlots = new bool[maxActiveSquads];
        if (idleSquad != null && idleSquadDeploymentIndex >= 0 && idleSquadDeploymentIndex < occupiedSlots.Length)
            occupiedSlots[idleSquadDeploymentIndex] = true;

        foreach (DispatchedWbcSquad squad in dispatchedWbcSquads)
        {
            if (squad != null && squad.deploymentIndex >= 0 && squad.deploymentIndex < occupiedSlots.Length)
                occupiedSlots[squad.deploymentIndex] = true;
        }

        for (int index = 0; index < occupiedSlots.Length; index++)
        {
            if (!occupiedSlots[index])
                return index;
        }

        return 0;
    }

    private void LogSquadSlotFreed()
    {
        int activeSquadCount = CountActiveSquads();
        int maxActiveSquads = Mathf.Max(1, DifficultySettings.CurrentStats.maxActiveSquads);
        LogWbcSquadMessage($"Squad slot freed ({activeSquadCount}/{maxActiveSquads}).", ConsoleLogUI.LogType.Success);
        RefreshWbcSquadHud();
    }



    private int CountActiveSquadUnits()
    {
        HashSet<GameObject> activeUnits = new HashSet<GameObject>();
        if (idleSquad != null)
        {
            foreach (GameObject unit in idleSquad)
            {
                if (unit != null && unit.activeInHierarchy)
                    activeUnits.Add(unit);
            }
        }

        foreach (DispatchedWbcSquad squad in dispatchedWbcSquads)
        {
            if (squad == null || squad.units == null)
                continue;

            foreach (GameObject unit in squad.units)
            {
                if (unit != null && unit.activeInHierarchy)
                    activeUnits.Add(unit);
            }
        }

        return activeUnits.Count;
    }

    private void RefreshDispatchAvatar()
    {
        int activeCount = CountActiveWbcs();
        foreach (List<InfectionMarker> markers in markersByBodyPartButton.Values)
        {
            foreach (InfectionMarker marker in markers)
            {
                if (marker.dispatchCountLabel != null)
                    marker.dispatchCountLabel.text = $"WBC {activeCount}/{maxActiveWbcs}";

                if (marker.dispatchImage != null)
                    marker.dispatchImage.color = enableWbcDispatch && activeCount < maxActiveWbcs ? Color.white : dispatchUnavailableColor;

                if (marker.dispatchButton != null)
                    marker.dispatchButton.interactable = enableWbcDispatch && activeCount < maxActiveWbcs;
            }
        }
    }

    private void LogInfection(InfectionData infection, InfectionBodyPartButtonReference bodyPartButton)
    {
        string message = $"{infection.displayName} at {GetBodyPartDisplayName(bodyPartButton.button)}: {infection.entryCause}";
        Debug.Log(message);
        if (ConsoleLogUI.Instance != null)
            ConsoleLogUI.Instance.Log(message, ConsoleLogUI.LogType.Warning);
    }

    private string GetBodyPartDisplayName(InfectionBodyPartButtonReference mapping)
    {
        if (mapping == null || mapping.button == null)
            return mapping != null ? mapping.bodyPartGroup.ToString() : "unknown location";
        return GetBodyPartDisplayName(mapping.button);
    }

    private static string GetBodyPartDisplayName(Button button)
    {
        string buttonName = button.name;
        return buttonName.EndsWith(OrderButtonSuffix, StringComparison.Ordinal)
            ? buttonName.Substring(0, buttonName.Length - OrderButtonSuffix.Length)
            : buttonName;
    }

    private static List<InfectionData> CreateDefaultInfectionEntries()
    {
        return new List<InfectionData>
        {
            new InfectionData
            {
                displayName = "Staph",
                pathogenType = InfectionPathogenType.Bacterial,
                severityStage = InfectionSeverityStage.Mild,
                entryCause = "scraped knee",
                correctResponder = InfectionCorrectResponder.WBC,
                preferredBodyParts = new List<InfectionBodyPartGroup> { InfectionBodyPartGroup.Feet, InfectionBodyPartGroup.Hands },
                daysUntreated = 0
            },
            new InfectionData
            {
                displayName = "E. coli/Salmonella",
                pathogenType = InfectionPathogenType.Bacterial,
                severityStage = InfectionSeverityStage.Moderate,
                entryCause = "spoiled food",
                correctResponder = InfectionCorrectResponder.WBC,
                preferredBodyParts = new List<InfectionBodyPartGroup> { InfectionBodyPartGroup.Head },
                daysUntreated = 0
            },
            new InfectionData
            {
                displayName = "Strep",
                pathogenType = InfectionPathogenType.Bacterial,
                severityStage = InfectionSeverityStage.Mild,
                entryCause = "sore throat",
                correctResponder = InfectionCorrectResponder.WBC,
                preferredBodyParts = new List<InfectionBodyPartGroup> { InfectionBodyPartGroup.Head },
                daysUntreated = 0
            },
            new InfectionData
            {
                displayName = "Common cold",
                pathogenType = InfectionPathogenType.Viral,
                severityStage = InfectionSeverityStage.Mild,
                entryCause = "crowded commute",
                correctResponder = InfectionCorrectResponder.KillerT,
                preferredBodyParts = new List<InfectionBodyPartGroup> { InfectionBodyPartGroup.Head },
                daysUntreated = 0
            },
            new InfectionData
            {
                displayName = "Flu",
                pathogenType = InfectionPathogenType.Viral,
                severityStage = InfectionSeverityStage.Moderate,
                entryCause = "crowded commute",
                correctResponder = InfectionCorrectResponder.KillerT,
                preferredBodyParts = new List<InfectionBodyPartGroup> { InfectionBodyPartGroup.Head },
                daysUntreated = 0
            },
            new InfectionData
            {
                displayName = "Stomach flu",
                pathogenType = InfectionPathogenType.Viral,
                severityStage = InfectionSeverityStage.Mild,
                entryCause = "unwashed hands",
                correctResponder = InfectionCorrectResponder.KillerT,
                preferredBodyParts = new List<InfectionBodyPartGroup> { InfectionBodyPartGroup.Hands },
                daysUntreated = 0
            }
        };
    }

    private static List<InfectionBodyPartButtonReference> CreateDefaultBodyPartButtons()
    {
        return new List<InfectionBodyPartButtonReference>
        {
            new InfectionBodyPartButtonReference { bodyPartGroup = InfectionBodyPartGroup.Feet },
            new InfectionBodyPartButtonReference { bodyPartGroup = InfectionBodyPartGroup.Feet },
            new InfectionBodyPartButtonReference { bodyPartGroup = InfectionBodyPartGroup.Hands },
            new InfectionBodyPartButtonReference { bodyPartGroup = InfectionBodyPartGroup.Hands },
            new InfectionBodyPartButtonReference { bodyPartGroup = InfectionBodyPartGroup.Head }
        };
    }
}
