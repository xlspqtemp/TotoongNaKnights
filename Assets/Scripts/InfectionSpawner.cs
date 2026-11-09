using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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
    private const int MinimumHoursBetweenInfectionSpawns = 2;
    private const int MaximumHoursBetweenInfectionSpawns = 3;
    private const int PathogenPoolDifficultyCount = 4;
    private const float FungalHealthMultiplier = 1.25f;
    private const float BreachedBarrierPathogenMultiplier = 1.5f;
    private const int DefaultMaximumActiveWbcs = 5;
    private const int DefaultMaximumActiveInfections = 5;
    private const float SquadSpawnOffsetRadius = 1.75f;
    private const float SquadStandingOffsetRadius = 2.8f;
    private const float SquadOffsetAngleStep = 2.39996323f;
    private const string DendriticDestinationObjectName = "Dendritic Infection Destination";
    private const float MinimumPromptWellnessGap = 30f;
    private const float DefaultPromptWellnessGap = 36f;
    private const float SquadPromptFadeDuration = 0.4f;
    private const float InfectionPopupFadeDuration = 0.3f;
    private const string AnimatorMovingParameter = "IsMoving";
    private const string AnimatorIdleStateName = "Idle";
    private const string AnimatorAttackParameter = "Attack";
    private const string AnimatorTagParameter = "Tag";
    private const string AnimatorVictoryParameter = "Victory";
    private const float AnimatorMovementThreshold = 0.1f;
    private const float BCellTagAnimationTimeoutSeconds = 10f;
    private const float KillerTCellAttackDurationSeconds = 1.1f;
    private const float KillerTCellWarCryDurationSeconds = 2.8333335f;
    private const int MinimumViralContainmentDays = 2;
    private const int MaximumViralContainmentDays = 3;
    private const string TCellUnitName = "T Cell";
    private const string BCellUnitName = "B Cell";
    private const string NeutrophilUnitName = "Neutrophil";
    private const string NeutrophilDisplayName = "Neutrophil";
    private const string EosinophilDisplayName = "Eosinophil";
    private const string BasophilDisplayName = "Basophil";
    private const string NeutrophilVariantName = "NEUTROPHIL";
    private const string EosinophilVariantName = "EOSINOPHIL";
    private const string BasophilVariantName = "BASOPHIL";
    private static readonly Color AntibodyGoldColor = new Color(1f, 0.72f, 0.12f, 1f);
    private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
    private const float ContainedDendriticIdleBobHeight = 0.04f;
    private const float ContainedDendriticIdleBobSpeed = 2f;
    private const string AntigenIconResourcePath = "UI/Dentritic message pop up";
    private const string AntigenScaleReferenceIconResourcePath = "UI/Blue virus icon";
    private const string AntigenCollectedTitle = "ANTIGEN COLLECTED";
    private const string AntigenCollectedSubtitleFormat = "{0} antigen acquired. T-Cell training unlocked.";
    private const string AntigenAlreadyAcquiredTitle = "ANTIGEN ALREADY ACQUIRED";
    private const string AntigenAlreadyAcquiredSubtitleFormat = "{0} T-Cell is already unlocked in the Lymphatic Factory.";
    private const string AdaptiveResponsePopupEnqueueMessageMethodName = "EnqueueMessage";
    private const BindingFlags AdaptiveResponsePopupMessageMethodFlags = BindingFlags.Instance | BindingFlags.NonPublic;
    private const int NotificationSpritePixelsPerUnit = 1024;
    private const float AntigenNotificationIconScale = 0.65f;
    private const float AntigenIconHeightOffset = 2f;
    private const float AntigenIconBobHeight = 0.12f;
    private const float AntigenIconBobSpeed = 2f;
    private const float AntigenIconPulseSpeed = 3f;
    private const float AntigenIconMinimumAlpha = 0.45f;
    private const float AntigenIconMaximumAlpha = 1f;
    private const float AntigenIconColliderScale = 1.5f;
    private const float MacrophageDefaultHealth = 300f;
    private const float MacrophageDefaultAttackDamage = 20f;
    private const float NormalDifficultyWbcHp = 100f;
    private const float NormalDifficultyWbcAttack = 10f;
    private const float MacrophageAttackRange = 2f;
    private const float NeutrophilBaseDamage = 10f;
    private const float EosinophilBaseDamage = 15f;
    private const float BasophilBaseDamage = 12f;
    private const float InnateVariantSpecialistDamageMultiplier = 3f;
    private static readonly Color NeutrophilBaseColor = new Color(0.906f, 0.877f, 0.857f, 1f);
    private static readonly Color EosinophilTint = new Color(1f, 0.28f, 0.14f, 1f);
    private static readonly Color BasophilTint = new Color(0.48f, 0.32f, 1f, 1f);
    private static readonly string[] ParasiteCategoryKeywords = { "parasite", "parasitic", "malaria", "protozoa", "helminth", "worm" };
    private static readonly string[] AllergenCategoryKeywords = { "allergen", "allergy", "pollen", "dust mite", "pet dander", "ragweed" };
    private const float OxygenBuffMovementMultiplier = 2f;
    private const float OxygenBuffAgentRefreshIntervalSeconds = 0.2f;
    private static readonly InfectionRarity[] GenericPathogenRarities =
    {
        InfectionRarity.Common,
        InfectionRarity.Uncommon
    };
    private static readonly InfectionRarity[] NamedPathogenRarities =
    {
        InfectionRarity.Common,
        InfectionRarity.Uncommon,
        InfectionRarity.Rare,
        InfectionRarity.VeryRare
    };


    private const float InfectionPopupReferenceDistance = 5f;
    [Header("Selected Infection Popup")]
    [SerializeField, Min(1f)] private float popupFontSize = 26f;
    [SerializeField] private Vector2 popupSize = new Vector2(440f, 140f);
    [SerializeField, Min(0.001f)] private float popupScale = 0.005f;
    [SerializeField, Min(0f)] private float popupMinVerticalOffset = 1.5f;
    [SerializeField, Min(0f)] private float popupClusterRadiusMultiplier = 0.5f;
    private const string DefaultMarkerSpriteResourcePath = "HUDWhiteSwatch";
    private const string DefaultWbcAvatarResourcePath = "Manual/WBC";
    private const string OrderButtonSuffix = " Order";
    private const float LeukocyteSupplyBarFlashDurationSeconds = 0.35f;
    private const float LeukocyteSupplyBarPulseScale = 0.08f;

    private static readonly Vector2 DefaultDispatchAvatarSize = new Vector2(38f, 38f);
    private static readonly Color DefaultBacterialColor = new Color(0.95f, 0.2f, 0.18f, 1f);
    private static readonly Color DefaultViralColor = new Color(0.2f, 0.55f, 1f, 1f);
    private static readonly Color DefaultFungalColor = new Color(0.2f, 0.8f, 0.28f, 1f);
    private static readonly Color DefaultDispatchUnavailableColor = new Color(0.45f, 0.45f, 0.45f, 0.65f);

    [Header("Day Progression")]
    [SerializeField] private DayCounterUI dayCounter;
    [SerializeField, Min(1)] private int maxActiveInfections = DefaultMaximumActiveInfections;

    [Header("Pathogen Database")]
    private List<InfectionData> infectionEntries;
    private int hoursUntilNextInfectionSpawn;
    private bool infectionSpawnTimerInitialized;

    [Header("Body-Part Button Mapping")]
    [SerializeField] private List<InfectionBodyPartButtonReference> bodyPartButtons = CreateDefaultBodyPartButtons();

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
    [SerializeField] private Color squadHudPanelColor = new Color(0.025f, 0.045f, 0.065f, 0.9f);
    [SerializeField] private Color squadCountColor = Color.white;
    [SerializeField, Min(0f)] private float markerFadeDuration = 0.22f;

    [Header("Random Body-Part Placement")]
    [SerializeField, Min(0f)] private float preferredBodyPartWeight = 4f;
    [SerializeField, Min(0f)] private float otherBodyPartWeight = 1f;

    [Header("Marker Appearance")]
    [SerializeField] private Sprite markerSprite;
    [SerializeField] private string markerSpriteResourcePath = DefaultMarkerSpriteResourcePath;
    [SerializeField] private Color bacterialColor = DefaultBacterialColor;
    [SerializeField] private Color viralColor = DefaultViralColor;
    [SerializeField] private Color fungalColor = DefaultFungalColor;

    [Header("WBC Dispatch")]
    [SerializeField] private bool enableWbcDispatch = true;
    [SerializeField] private bool useLegacySelectFirst;
    [SerializeField, Min(1)] private int maxActiveWbcs = DefaultMaximumActiveWbcs;
    [SerializeField, Min(0.1f)] private float perWbcCooldownSeconds = 20f;
    [SerializeField, Min(0f)] private float dispatchArrivalDelaySeconds = 2f;
    [SerializeField] private Texture2D wbcAvatarTexture;
    [SerializeField] private string wbcAvatarResourcePath = DefaultWbcAvatarResourcePath;
    [SerializeField] private Vector2 dispatchAvatarSize = DefaultDispatchAvatarSize;
    [SerializeField] private Color dispatchUnavailableColor = DefaultDispatchUnavailableColor;

    [Header("Infection Resolution")]
    [SerializeField] private AudioClip infectionClearedSfx;
    [SerializeField] private bool useFixedDurationClash = false;
    [SerializeField, Min(0f)] private float bacterialClashDurationSeconds = 4f;
    [SerializeField] private float mildBacterialWellnessGain = 2f;
    [SerializeField] private float moderateBacterialWellnessGain = 4f;
    [SerializeField] private float severeBacterialWellnessGain = 6f;
    [SerializeField, Min(0f)] private float wellnessGainOnResolve = 5f;
    [Header("Leukocyte Supply")]
    [SerializeField, Min(0)] private int clearBonus = 10;
    [SerializeField, Min(0)] private int containmentBonus = 5;
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
    // WBC deployment does not change the selected body-system layer.
    [SerializeField] private Color idleSquadHighlightColor = new Color(0f, 1f, 1f, 1f);
    [SerializeField, Min(0f)] private float headshotCooldownSeconds = 5f;
    [SerializeField] private bool keepAliveUntilResolved = true;
    [SerializeField, Min(0.1f)] private float squadLifetimeSeconds = 20f;
    [SerializeField, Min(0.1f)] private float dispatchNavMeshSampleRadius = 25f;
    [SerializeField, Min(0.1f)] private float dispatchArrivalRadius = 0.5f;
    [SerializeField] private GameObject dendriticPrefab;
    [SerializeField] private Vector3 holdOffset = new Vector3(1.5f, 0f, 0f);
    [SerializeField, Min(0f)] private float combatDetectionRange = 8f;
    [SerializeField, Min(0f)] private float combatMeleeRange = 1.5f;
    [SerializeField, Min(0f)] private float wbcCombatMoveSpeed = 3.5f;
    [SerializeField] private bool verboseCombatLogging;
    [SerializeField, Min(0f)] private float infectionAnchorMatchRadius = 2f;
    [SerializeField] private Vector2 promptBannerSize = new Vector2(700f, 58f);
    [SerializeField, Min(MinimumPromptWellnessGap)] private float promptBannerWellnessGap = DefaultPromptWellnessGap;
    [SerializeField] private Color promptBannerColor = new Color(0.025f, 0.045f, 0.065f, 0.92f);
    [SerializeField, Min(0f)] private float promptAutoFadeSeconds = 8f;
    private readonly HashSet<LymphaticBatchGroup> lymphaticBatchAttackAnimationsTriggered = new HashSet<LymphaticBatchGroup>();
    private readonly HashSet<GameObject> bCellTintedUnits = new HashSet<GameObject>();




    private readonly Dictionary<Button, List<InfectionMarker>> markersByBodyPartButton = new Dictionary<Button, List<InfectionMarker>>();
    private AudioSource infectionAudioSource;
    private readonly List<GameObject> activeBatchPathogens = new List<GameObject>();
    private readonly List<PendingAntigenCollection> pendingAntigenCollections = new List<PendingAntigenCollection>();
    private Sprite resolvedAntigenIconSprite;
    private Texture2D antigenScaleReferenceTexture;
    private readonly List<LymphaticBatchGroup> activeLymphaticBatches = new List<LymphaticBatchGroup>();
    private LymphaticBatchSpawner lymphaticBatchSpawner;

    private readonly List<WbcDispatchSlot> wbcDispatchSlots = new List<WbcDispatchSlot>();
    private readonly List<InfectionMarker> activeInfectionMarkers = new List<InfectionMarker>();
    private readonly List<InfectionMarker> fadingInfectionMarkers = new List<InfectionMarker>();
    private InfectionMarker selectedMarker;
    private Sprite resolvedMarkerSprite;
    private Texture2D resolvedWbcAvatarTexture;
    private Texture2D resolvedHeadshotTexture;
    private GameObject wbcHudRoot;
    private Button headshotButton;
    private Image wbcHeadshotImage;
    private TextMeshProUGUI squadCountLabel;
    private Image headshotCooldownFill;
    private TextMeshProUGUI headshotCooldownLabel;
    private RectTransform leukocyteSupplyBarRect;
    private Image leukocyteSupplyFill;
    private TextMeshProUGUI leukocyteSupplyValueLabel;
    private float leukocyteSupply;
    private float leukocyteSupplyBarFlashRemaining;
    private PendingInfectionsPanel pendingInfectionsPanel;
    private GameObject promptBannerObject;
    private TextMeshProUGUI promptBannerLabel;
    private CanvasGroup promptBannerCanvasGroup;
    private AdaptiveResponsePopup adaptiveResponsePopup;
    private GameObject selectedInfectionPopupObject;
    private CanvasGroup selectedInfectionPopupCanvasGroup;
    private TextMeshProUGUI selectedInfectionCategoryLabel;
    private TextMeshProUGUI selectedInfectionDetailsLabel;
    private InfectionMarker selectedInfectionPopupMarker;
    private GameObject[] idleSquad;
    private readonly List<DispatchedWbcSquad> dispatchedWbcSquads = new List<DispatchedWbcSquad>();
    private readonly List<DispatchedDendriticCell> dispatchedDendriticCells = new List<DispatchedDendriticCell>();
    private float headshotCooldownRemaining;
    private float promptAutoFadeRemaining;
    private float promptFadeElapsed;
    private float promptFadeStartAlpha;
    private int idleSquadDeploymentIndex;
    private bool awaitingSquadDestination;
    private bool awaitingInfectionTargetSelection;
    private bool promptFadingOut;
    private Camera gameplayCamera;

    public IReadOnlyList<InfectionMarker> ActiveInfections => activeInfectionMarkers;

    /// <summary>Indicates whether the timed Respiratory Oxygen Boost perk is active.</summary>
    public static bool isOxygenBuffActive;

    private static InfectionSpawner activeInstance;
    private readonly Dictionary<NavMeshAgent, float> oxygenBuffOriginalAgentSpeeds = new Dictionary<NavMeshAgent, float>();
    private float oxygenBuffAgentRefreshTimer;

    /// <summary>Activates or ends the Respiratory Oxygen Boost and updates deployed immune-unit speeds.</summary>
    public static void SetOxygenBuffActive(bool isActive)
    {
        if (isOxygenBuffActive == isActive)
        {
            if (isActive && activeInstance != null)
                activeInstance.RefreshOxygenBuffAgentSpeeds();
            return;
        }

        isOxygenBuffActive = isActive;
        if (activeInstance == null)
            return;

        if (isActive)
        {
            activeInstance.oxygenBuffAgentRefreshTimer = 0f;
            activeInstance.RefreshOxygenBuffAgentSpeeds();
        }
        else
        {
            activeInstance.RestoreOxygenBuffAgentSpeeds();
        }
    }

    /// <summary>Returns whether an agent represents a tactical WBC or a Macrophage unit.</summary>
    private static bool IsOxygenBuffTarget(NavMeshAgent agent)
    {
        if (agent == null)
            return false;

        if (agent.GetComponentInParent<WbcIdleHighlight>() != null)
            return true;

        MacrophageScript macrophage = agent.GetComponentInParent<MacrophageScript>();
        LymphaticBatchGroup batch = agent.GetComponentInParent<LymphaticBatchGroup>();
        return macrophage != null && batch != null &&
            string.Equals(batch.UnitType, "Macrophage", StringComparison.OrdinalIgnoreCase) &&
            batch.Status != LymphaticBatchGroup.BatchStatus.Ready;
    }

    private void ApplyOxygenBuffSpeed(NavMeshAgent agent)
    {
        if (!isOxygenBuffActive || agent == null || !agent.isActiveAndEnabled || !IsOxygenBuffTarget(agent))
            return;

        if (!oxygenBuffOriginalAgentSpeeds.TryGetValue(agent, out float originalSpeed))
        {
            originalSpeed = agent.speed;
            oxygenBuffOriginalAgentSpeeds.Add(agent, originalSpeed);
        }

        agent.speed = originalSpeed * OxygenBuffMovementMultiplier;
    }

    private void RefreshOxygenBuffAgentSpeeds()
    {
        foreach (NavMeshAgent agent in FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None))
            ApplyOxygenBuffSpeed(agent);
    }

    private void RestoreOxygenBuffAgentSpeeds()
    {
        foreach (KeyValuePair<NavMeshAgent, float> entry in oxygenBuffOriginalAgentSpeeds)
        {
            if (entry.Key != null)
                entry.Key.speed = entry.Value;
        }

        oxygenBuffOriginalAgentSpeeds.Clear();
        oxygenBuffAgentRefreshTimer = 0f;
    }

    private void UpdateOxygenBuff()
    {
        if (!isOxygenBuffActive)
        {
            if (oxygenBuffOriginalAgentSpeeds.Count > 0)
                RestoreOxygenBuffAgentSpeeds();
            return;
        }

        oxygenBuffAgentRefreshTimer -= Time.unscaledDeltaTime;
        if (oxygenBuffAgentRefreshTimer > 0f)
            return;

        oxygenBuffAgentRefreshTimer = OxygenBuffAgentRefreshIntervalSeconds;
        RefreshOxygenBuffAgentSpeeds();
    }

    /// <summary>Returns whether the supplied infection marker is still awaiting resolution.</summary>
    public bool IsInfectionActive(InfectionMarker marker)
    {
        return marker != null && !marker.isRemoving && activeInfectionMarkers.Contains(marker);
    }

    /// <summary>Raised when the player clicks a live infection row or map icon while assigning a batch target.</summary>
    public event Action<InfectionMarker> OnInfectionTargetClicked;

    /// <summary>Forwards a clicked live infection marker to batch targeting and any active legacy target picker.</summary>
    public void NotifyInfectionTargetClicked(InfectionMarker marker)
    {
        if (marker == null || marker.isRemoving || !activeInfectionMarkers.Contains(marker))
            return;

        OnInfectionTargetClicked?.Invoke(marker);
        if (awaitingInfectionTargetSelection)
            RequestDispatchToInfection(marker);
    }

    /// <summary>Returns the world-space average position of each deployed squad with at least one living unit.</summary>
    public IReadOnlyList<Vector3> GetDeployedSquadPositions()
    {
        List<Vector3> positions = new List<Vector3>(dispatchedWbcSquads.Count);
        foreach (DispatchedWbcSquad squad in dispatchedWbcSquads)
        {
            if (squad == null || squad.units == null)
                continue;

            Vector3 positionSum = Vector3.zero;
            int liveUnitCount = 0;
            foreach (GameObject unit in squad.units)
            {
                if (unit == null || !unit.activeInHierarchy)
                    continue;

                Health health = unit.GetComponent<Health>();
                if (health != null && health.IsDead)
                    continue;

                positionSum += unit.transform.position;
                liveUnitCount++;
            }

            if (liveUnitCount > 0)
                positions.Add(positionSum / liveUnitCount);
        }

        return positions;
    }

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

        if (lymphaticBatchSpawner == null)
            lymphaticBatchSpawner = FindFirstObjectByType<LymphaticBatchSpawner>();
        if (lymphaticBatchSpawner != null)
        {
            lymphaticBatchSpawner.GetBatches(activeLymphaticBatches);
            foreach (LymphaticBatchGroup batch in activeLymphaticBatches)
            {
                if (batch != null && batch.TargetInfection == marker && batch.Status != LymphaticBatchGroup.BatchStatus.Ready)
                    return true;
            }
        }

        return false;
    }

    public bool IsAwaitingInfectionTargetSelection => awaitingInfectionTargetSelection;

    /// <summary>Force-spawns one infection using the active difficulty's generic/named and rarity weights.</summary>
    public void ForceSpawnOneInfection()
    {
        if (activeInfectionMarkers.Count >= maxActiveInfections)
        {
            Debug.LogWarning("[InfectionSpawner] Cannot force-spawn an infection because the active infection cap has been reached.", this);
            return;
        }

        if (GetValidBodyPartButtonCount() == 0)
        {
            Debug.LogWarning("[InfectionSpawner] Cannot force-spawn an infection because no body-part mappings are configured.", this);
            return;
        }

        if (!TryCreateWeightedInfection(out InfectionData infection, out bool isGeneric, out int namedRoll))
        {
            Debug.LogWarning("[InfectionSpawner] Cannot force-spawn an infection because the current difficulty has no positive pathogen weights.", this);
            return;
        }

        InfectionBodyPartButtonReference bodyPartButton = SelectWeightedBodyPartButton(infection);
        if (bodyPartButton == null)
            return;

        bool wasBarrierMutated = ApplyBarrierMutation(infection);
        int infectionCountBeforeSpawn = activeInfectionMarkers.Count;
        SpawnMarker(infection, bodyPartButton, wasBarrierMutated);
        if (activeInfectionMarkers.Count == infectionCountBeforeSpawn + 1)
        {
            LogInfection(infection, bodyPartButton);
            DifficultyStats stats = DifficultySettings.CurrentStats;
            Debug.Log($"[InfectionSpawner] Forced weighted roll {namedRoll}/100; NamedChance={stats.namedInfectionChance:0.##}%; Result={(isGeneric ? "Generic" : "Named")}; Difficulty={stats.difficultyName}.", this);
        }
    }

    /// <summary>Spawns one bacterial infection through the configured marker creation path, preferring its configured body parts.</summary>
    public void ForceSpawnOneBacterialInfection()
    {
        if (activeInfectionMarkers.Count >= maxActiveInfections)
        {
            Debug.LogWarning("[InfectionSpawner] Cannot force-spawn a bacterial infection because the active infection cap has been reached.", this);
            return;
        }

        List<InfectionData> validBacterialEntries = new List<InfectionData>();
        List<List<InfectionBodyPartButtonReference>> preferredMappingsByEntry = new List<List<InfectionBodyPartButtonReference>>();
        foreach (InfectionData infection in BuildValidInfectionEntries())
        {
            if (infection.pathogenType != InfectionPathogenType.Bacterial || infection.preferredBodyParts == null)
                continue;

            List<InfectionBodyPartButtonReference> preferredMappings = new List<InfectionBodyPartButtonReference>();
            if (bodyPartButtons != null)
            {
                foreach (InfectionBodyPartButtonReference mapping in bodyPartButtons)
                {
                    if (mapping != null && mapping.button != null && infection.preferredBodyParts.Contains(mapping.bodyPartGroup))
                        preferredMappings.Add(mapping);
                }
            }

            if (preferredMappings.Count == 0)
                continue;

            validBacterialEntries.Add(infection);
            preferredMappingsByEntry.Add(preferredMappings);
        }

        if (validBacterialEntries.Count == 0)
        {
            Debug.LogWarning("[InfectionSpawner] Cannot force-spawn a bacterial infection because no valid bacterial entries have mapped preferred body parts.", this);
            return;
        }

        int entryIndex = UnityEngine.Random.Range(0, validBacterialEntries.Count);
        InfectionData selectedInfection = validBacterialEntries[entryIndex];
        List<InfectionBodyPartButtonReference> preferredBodyPartMappings = preferredMappingsByEntry[entryIndex];
        InfectionBodyPartButtonReference selectedBodyPart = preferredBodyPartMappings[UnityEngine.Random.Range(0, preferredBodyPartMappings.Count)];
        InfectionData infectionToSpawn = CloneInfectionData(selectedInfection);
        bool wasBarrierMutated = ApplyBarrierMutation(infectionToSpawn);
        int infectionCountBeforeSpawn = activeInfectionMarkers.Count;
        SpawnMarker(infectionToSpawn, selectedBodyPart, wasBarrierMutated);
        if (activeInfectionMarkers.Count == infectionCountBeforeSpawn + 1)
            LogInfection(infectionToSpawn, selectedBodyPart);
    }

    /// <summary>Spawns a Staph infection, falling back to Strep, at the supplied world position.</summary>
    public InfectionMarker ForceSpawnWoundBacterialInfection(Vector3 worldPosition)
    {
        if (activeInfectionMarkers.Count >= maxActiveInfections)
        {
            Debug.LogWarning("[InfectionSpawner] Cannot spawn a wound infection because the active infection cap has been reached.", this);
            return null;
        }

        InfectionData selectedInfection = null;
        foreach (InfectionData infection in BuildValidInfectionEntries())
        {
            if (infection.pathogenType != InfectionPathogenType.Bacterial)
                continue;

            string pathogenName = string.IsNullOrWhiteSpace(infection.pathogenDatabaseName)
                ? infection.displayName
                : infection.pathogenDatabaseName;
            if (string.Equals(pathogenName, "Staph", StringComparison.OrdinalIgnoreCase))
            {
                selectedInfection = infection;
                break;
            }

            if (selectedInfection == null && string.Equals(pathogenName, "Strep", StringComparison.OrdinalIgnoreCase))
                selectedInfection = infection;
        }

        if (selectedInfection == null)
        {
            Debug.LogWarning("[InfectionSpawner] Cannot spawn a wound infection because no Staph or Strep bacterial entry is available.", this);
            return null;
        }

        InfectionData infectionToSpawn = CloneInfectionData(selectedInfection);
        InfectionBodyPartButtonReference bodyPartButton = SelectWeightedBodyPartButton(infectionToSpawn);
        if (bodyPartButton == null)
        {
            Debug.LogWarning("[InfectionSpawner] Cannot spawn a wound infection because no body-part dispatch mapping is available.", this);
            return null;
        }

        InfectionMarker marker = SpawnMarker(infectionToSpawn, bodyPartButton, false, worldPosition);
        if (marker != null)
            LogInfection(infectionToSpawn, bodyPartButton);
        return marker;
    }

    public sealed class InfectionMarker
    {
        public InfectionData infection;
        public InfectionBodyPartButtonReference bodyPartMapping;
        public string bodyPartName;
        public InfectionBodyPartGroup bodyPartGroup;
        public Button dispatchButton;
        public RawImage dispatchImage;
        public TextMeshProUGUI dispatchCountLabel;
        public Canvas canvas;
        public CanvasGroup canvasGroup;
        public float animationElapsed;
        public bool isRemoving;
        public bool isContained;
        public bool isTagged;
        public bool bCellTagInProgress;
        public bool resolutionPending;
        public int containedResolveDay;
        public GameObject containedDendriticCell;
        public Vector3 containedDendriticBasePosition;
        public bool containedDendriticUsesProceduralIdle;
        public int unresolvedDays;
        public bool wasBarrierMutated;
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
        public bool holdingForContainment;
        public bool wipeWellnessPenaltyApplied;
        public readonly HashSet<GameObject> arrivedUnits = new HashSet<GameObject>();
    }

    private sealed class DispatchedDendriticCell
    {
        public InfectionMarker infectionTarget;
        public GameObject cell;
        public GameObject destination;
    }

    private sealed class PendingAntigenCollection
    {
        public string antigenName;
        public GameObject dendriticCell;
        public GameObject icon;
        public SpriteRenderer iconRenderer;
        public BoxCollider iconCollider;
        public Vector3 iconBasePosition;
        public Vector3 scaleAnchor;
    }

    private void Awake()
    {
        activeInstance = this;
        isOxygenBuffActive = false;
        infectionEntries = CreateDefaultInfectionEntries();
        infectionAudioSource = GetComponent<AudioSource>();
        if (infectionAudioSource == null)
            infectionAudioSource = gameObject.AddComponent<AudioSource>();
        infectionAudioSource.playOnAwake = false;
        infectionAudioSource.spatialBlend = 0f;
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

        DifficultyStats difficultyStats = DifficultySettings.CurrentStats;
        leukocyteSupply = Mathf.Clamp(difficultyStats.startingSupply, 0, Mathf.Max(0, difficultyStats.maxSupply));
        maxActiveInfections = Mathf.Max(1, maxActiveInfections);
        maxActiveWbcs = Mathf.Max(1, maxActiveWbcs);
        squadMemberCount = Mathf.Max(1, squadMemberCount);
        headshotCooldownSeconds = Mathf.Max(0f, headshotCooldownSeconds);
        squadLifetimeSeconds = Mathf.Max(0.1f, squadLifetimeSeconds);
        promptBannerWellnessGap = Mathf.Max(MinimumPromptWellnessGap, promptBannerWellnessGap);
        EnsureDispatchSlots();
        ApplyBodyPartButtonVisibility();
    }

    private void OnEnable()
    {
        if (dayCounter != null)
        {
            dayCounter.OnDayAdvanced += HandleDayAdvanced;
            dayCounter.OnHourAdvanced += HandleHourAdvanced;
        }

        if (!infectionSpawnTimerInitialized)
            ResetInfectionSpawnTimer();

        EnsurePendingInfectionsPanelSubscription();
    }

    private void Start()
    {
        DifficultyStats stats = DifficultySettings.CurrentStats;
        Debug.Log($"Difficulty: {stats.difficultyName} | wbcHp={stats.wbcHp:0.##} | wbcAttack={stats.wbcAttack:0.##} | bacteriaHp={stats.bacteriaHp:0.##} | virusHp={stats.virusHp:0.##} | bacteriaAttack={stats.bacteriaAttack:0.##} | virusAttack={stats.virusAttack:0.##} | attackIntervalSeconds={stats.attackIntervalSeconds:0.##} | wbcDamageMultiplierVsBacteria={stats.wbcDamageMultiplierVsBacteria:0.##} | wbcDamageMultiplierVsVirus={stats.wbcDamageMultiplierVsVirus:0.##}", this);
        CreateAdaptiveResponsePopup();
        EnsurePendingInfectionsPanelSubscription();
        if (dayCounter == null)
            Debug.LogWarning("[InfectionSpawner] DayCounterUI is missing; timed infection spawning is disabled.", this);
        else
            Debug.Log($"[InfectionSpawner] Timed infection spawning initialized; next spawn in {hoursUntilNextInfectionSpawn} in-game hours.", this);
        if (hudCanvas == null)
            Debug.LogWarning("[InfectionSpawner] HUD Canvas is missing; infection row UI is disabled.", this);
    }

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Time.timeScale > 0f && Input.GetKeyDown(debugWellnessLossKey))
            ApplyWellnessChange("Debug test", -debugWellnessLossAmount);
#endif

        UpdateOxygenBuff();
        bool antigenIconClicked = HandleAntigenIconClick();
        UpdateWbcCooldowns();
        UpdateLeukocyteSupply();
        UpdateDendriticCells();
        UpdateContainedDendriticCells();
        UpdatePendingAntigenCollections();
        UpdateIdleAndDispatchedSquads();
        UpdateInfectionCombat();
        UpdateLymphaticBatchAnimations();

        UpdateLymphaticBatchInfectionResolutions();
        UpdateTaggedInfectionResolutions();
        UpdateInfectionMarkerAnimations();
        UpdateInfectionThreatVisuals();
        UpdateSelectedInfectionPopup();
        UpdateSquadPrompt();
        HandleSquadPromptCancellation();
        HandleMapClick(antigenIconClicked);
        EnsurePendingInfectionsPanelSubscription();
    }

    private void UpdateLeukocyteSupply()
    {
        DifficultyStats stats = DifficultySettings.CurrentStats;
        float maxSupply = Mathf.Max(0, stats.maxSupply);
        float gameplayDeltaTime = GameplaySpeed.DeltaTime;
        if (gameplayDeltaTime > 0f)
        {
            float regenSecondsPerPoint = Mathf.Max(0.01f, stats.regenSecondsPerPoint);
            leukocyteSupply = Mathf.Min(maxSupply, leukocyteSupply + gameplayDeltaTime / regenSecondsPerPoint);
        }
        else
        {
            leukocyteSupply = Mathf.Min(maxSupply, leukocyteSupply);
        }

        if (leukocyteSupplyBarFlashRemaining > 0f)
            leukocyteSupplyBarFlashRemaining = Mathf.Max(0f, leukocyteSupplyBarFlashRemaining - Time.unscaledDeltaTime);
        UpdateLeukocyteSupplyBarPulse();
    }

    private void AddLeukocyteSupply(float amount)
    {
        if (amount <= 0f)
            return;

        float maxSupply = Mathf.Max(0, DifficultySettings.CurrentStats.maxSupply);
        float previousSupply = leukocyteSupply;
        leukocyteSupply = Mathf.Clamp(leukocyteSupply + amount, 0f, maxSupply);
        if (leukocyteSupply > previousSupply)
            leukocyteSupplyBarFlashRemaining = LeukocyteSupplyBarFlashDurationSeconds;
        RefreshLeukocyteSupplyBar();
    }

    private void SpendLeukocyteSupply(int amount)
    {
        float previousSupply = leukocyteSupply;
        leukocyteSupply = Mathf.Max(0f, leukocyteSupply - Mathf.Max(0, amount));
        if (leukocyteSupply < previousSupply)
            leukocyteSupplyBarFlashRemaining = LeukocyteSupplyBarFlashDurationSeconds;
        RefreshLeukocyteSupplyBar();
    }

    private void RefreshLeukocyteSupplyBar()
    {
        if (leukocyteSupplyFill == null || leukocyteSupplyValueLabel == null)
            return;

        DifficultyStats stats = DifficultySettings.CurrentStats;
        int maxSupply = Mathf.Max(0, stats.maxSupply);
        leukocyteSupply = Mathf.Clamp(leukocyteSupply, 0f, maxSupply);
        leukocyteSupplyFill.fillAmount = maxSupply > 0 ? Mathf.Clamp01(leukocyteSupply / maxSupply) : 0f;
        leukocyteSupplyValueLabel.text = $"{Mathf.FloorToInt(leukocyteSupply + 0.0001f)} / {maxSupply}";
    }

    private void UpdateLeukocyteSupplyBarPulse()
    {
        if (leukocyteSupplyBarRect == null || leukocyteSupplyFill == null)
            return;

        float pulse = 0f;
        if (leukocyteSupplyBarFlashRemaining > 0f)
        {
            float progress = 1f - leukocyteSupplyBarFlashRemaining / LeukocyteSupplyBarFlashDurationSeconds;
            pulse = Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI);
        }

        leukocyteSupplyBarRect.localScale = Vector3.one * (1f + pulse * LeukocyteSupplyBarPulseScale);
        leukocyteSupplyFill.color = Color.Lerp(new Color(0.15f, 0.78f, 0.8f, 1f), Color.white, pulse * 0.55f);
    }

    private void EnsurePendingInfectionsPanelSubscription()
    {
        PendingInfectionsPanel panel = FindFirstObjectByType<PendingInfectionsPanel>();
        if (panel == pendingInfectionsPanel)
            return;

        if (pendingInfectionsPanel != null)
            pendingInfectionsPanel.OnRowClickedPublic -= HandlePendingInfectionRowClicked;
        pendingInfectionsPanel = panel;
        if (pendingInfectionsPanel != null)
            pendingInfectionsPanel.OnRowClickedPublic += HandlePendingInfectionRowClicked;
    }

    private void HandlePendingInfectionRowClicked(InfectionMarker marker)
    {
        NotifyInfectionTargetClicked(marker);
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
        {
            dayCounter.OnDayAdvanced -= HandleDayAdvanced;
            dayCounter.OnHourAdvanced -= HandleHourAdvanced;
        }
        if (pendingInfectionsPanel != null)
            pendingInfectionsPanel.OnRowClickedPublic -= HandlePendingInfectionRowClicked;
        pendingInfectionsPanel = null;
        if (activeInstance == this)
        {
            isOxygenBuffActive = false;
            RestoreOxygenBuffAgentSpeeds();
            activeInstance = null;
        }
    }

    private void OnValidate()
    {
        maxActiveInfections = Mathf.Max(1, maxActiveInfections);
        maxActiveWbcs = Mathf.Max(1, maxActiveWbcs);
        squadMemberCount = Mathf.Max(1, squadMemberCount);
        headshotCooldownSeconds = Mathf.Max(0f, headshotCooldownSeconds);
        squadLifetimeSeconds = Mathf.Max(0.1f, squadLifetimeSeconds);
        infectionAnchorMatchRadius = Mathf.Max(0f, infectionAnchorMatchRadius);

        dispatchNavMeshSampleRadius = Mathf.Max(0.1f, dispatchNavMeshSampleRadius);
        dispatchArrivalRadius = Mathf.Max(0.1f, dispatchArrivalRadius);
        promptBannerWellnessGap = Mathf.Max(MinimumPromptWellnessGap, promptBannerWellnessGap);
        combatDetectionRange = Mathf.Max(0f, combatDetectionRange);
        combatMeleeRange = Mathf.Max(0f, combatMeleeRange);
        wbcCombatMoveSpeed = Mathf.Max(0f, wbcCombatMoveSpeed);
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
        promptAutoFadeSeconds = Mathf.Max(0f, promptAutoFadeSeconds);
        if (!Application.isPlaying)
            ApplyBodyPartButtonVisibility();
    }

    private void ApplyBodyPartButtonVisibility()
    {
        if (bodyPartButtonsCanvasGroup == null)
            return;

        bodyPartButtonsCanvasGroup.alpha = showBodyPartButtons ? 1f : 0f;
        bodyPartButtonsCanvasGroup.interactable = showBodyPartButtons;
        bodyPartButtonsCanvasGroup.blocksRaycasts = showBodyPartButtons;
    }

    public void SetWbcHudVisibility(bool isVisible)
    {
        if (wbcHudRoot != null)
            wbcHudRoot.SetActive(isVisible);
    }

    private void CreateWbcSquadHud()
    {
        if (hudCanvas == null || !(hudCanvas.transform is RectTransform canvasRect))
            return;

        GameObject squadHudObject = new GameObject("WBC Squad HUD", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        wbcHudRoot = squadHudObject;
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

        GameObject headshotObject = new GameObject("WBC Squad Headshot Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        headshotObject.transform.SetParent(squadHudObject.transform, false);
        RectTransform headshotRect = headshotObject.GetComponent<RectTransform>();
        headshotRect.anchorMin = Vector2.zero;
        headshotRect.anchorMax = Vector2.one;
        headshotRect.offsetMin = Vector2.zero;
        headshotRect.offsetMax = Vector2.zero;

        wbcHeadshotImage = headshotObject.GetComponent<Image>();
        wbcHeadshotImage.sprite = Resources.Load<Sprite>("UI/WBC HEADSHOT ICON");
        wbcHeadshotImage.preserveAspect = true;
        wbcHeadshotImage.raycastTarget = true;
        headshotButton = headshotObject.GetComponent<Button>();
        headshotButton.targetGraphic = wbcHeadshotImage;
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
        CreateLeukocyteSupplyBar(squadHudObject.transform);
    }

    private void CreateLeukocyteSupplyBar(Transform parent)
    {
        GameObject barObject = new GameObject("Leukocyte Supply Bar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        barObject.transform.SetParent(parent, false);
        barObject.transform.SetAsLastSibling();
        leukocyteSupplyBarRect = barObject.GetComponent<RectTransform>();
        leukocyteSupplyBarRect.anchorMin = new Vector2(0f, 1f);
        leukocyteSupplyBarRect.anchorMax = new Vector2(1f, 1f);
        leukocyteSupplyBarRect.pivot = new Vector2(0.5f, 0f);
        leukocyteSupplyBarRect.anchoredPosition = new Vector2(0f, 8f);
        leukocyteSupplyBarRect.sizeDelta = new Vector2(-8f, 48f);

        Image barBackground = barObject.GetComponent<Image>();
        barBackground.sprite = ResolveMarkerSprite();
        barBackground.color = new Color(0.025f, 0.045f, 0.065f, 0.96f);
        barBackground.raycastTarget = false;

        TextMeshProUGUI titleLabel = CreateSquadHudText("Leukocyte Supply Label", barObject.transform,
            "LEUKOCYTE SUPPLY", 11f, FontStyles.Bold, Color.white);
        titleLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        titleLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
        titleLabel.rectTransform.pivot = new Vector2(0f, 1f);
        titleLabel.rectTransform.anchoredPosition = new Vector2(7f, -4f);
        titleLabel.rectTransform.sizeDelta = new Vector2(135f, 17f);

        leukocyteSupplyValueLabel = CreateSquadHudText("Leukocyte Supply Value", barObject.transform,
            string.Empty, 11f, FontStyles.Bold, Color.white);
        leukocyteSupplyValueLabel.rectTransform.anchorMin = new Vector2(1f, 1f);
        leukocyteSupplyValueLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
        leukocyteSupplyValueLabel.rectTransform.pivot = new Vector2(1f, 1f);
        leukocyteSupplyValueLabel.rectTransform.anchoredPosition = new Vector2(-7f, -4f);
        leukocyteSupplyValueLabel.rectTransform.sizeDelta = new Vector2(72f, 17f);

        GameObject trackObject = new GameObject("Leukocyte Supply Track", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        trackObject.transform.SetParent(barObject.transform, false);
        RectTransform trackRect = trackObject.GetComponent<RectTransform>();
        trackRect.anchorMin = new Vector2(0f, 0f);
        trackRect.anchorMax = new Vector2(1f, 0f);
        trackRect.pivot = new Vector2(0.5f, 0f);
        trackRect.anchoredPosition = new Vector2(0f, 4f);
        trackRect.sizeDelta = new Vector2(-12f, 17f);
        Image trackImage = trackObject.GetComponent<Image>();
        trackImage.sprite = ResolveMarkerSprite();
        trackImage.color = new Color(0.01f, 0.025f, 0.04f, 1f);
        trackImage.raycastTarget = false;

        GameObject fillObject = new GameObject("Leukocyte Supply Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fillObject.transform.SetParent(trackObject.transform, false);
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        leukocyteSupplyFill = fillObject.GetComponent<Image>();
        leukocyteSupplyFill.sprite = ResolveMarkerSprite();
        leukocyteSupplyFill.type = Image.Type.Filled;
        leukocyteSupplyFill.fillMethod = Image.FillMethod.Horizontal;
        leukocyteSupplyFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        leukocyteSupplyFill.color = new Color(0.15f, 0.78f, 0.8f, 1f);
        leukocyteSupplyFill.raycastTarget = false;
        RefreshLeukocyteSupplyBar();
    }


    private void CreateSquadPromptBanner()
    {
        if (hudCanvas == null || !(hudCanvas.transform is RectTransform canvasRect))
            return;

        promptBannerObject = new GameObject("WBC Squad Destination Prompt Banner", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        promptBannerObject.transform.SetParent(canvasRect, false);
        promptBannerObject.transform.SetAsLastSibling();
        RectTransform bannerRect = promptBannerObject.GetComponent<RectTransform>();
        bannerRect.anchorMin = new Vector2(0.5f, 1f);
        bannerRect.anchorMax = new Vector2(0.5f, 1f);
        bannerRect.pivot = new Vector2(0.5f, 1f);
        bannerRect.sizeDelta = promptBannerSize;
        PositionPromptBelowWellnessBar(canvasRect);

        Image bannerBackground = promptBannerObject.GetComponent<Image>();
        bannerBackground.color = promptBannerColor;
        bannerBackground.raycastTarget = false;
        promptBannerCanvasGroup = promptBannerObject.GetComponent<CanvasGroup>();
        promptBannerCanvasGroup.alpha = 0f;
        promptBannerCanvasGroup.interactable = false;
        promptBannerCanvasGroup.blocksRaycasts = false;

        promptBannerLabel = CreateSquadHudText("WBC Squad Destination Prompt Text", bannerRect,
            "Click an infection to send your squad.", 18f, FontStyles.Bold, Color.white);
        promptBannerLabel.alignment = TextAlignmentOptions.Center;
        promptBannerLabel.textWrappingMode = TextWrappingModes.Normal;
        promptBannerLabel.rectTransform.anchorMin = Vector2.zero;
        promptBannerLabel.rectTransform.anchorMax = Vector2.one;
        promptBannerLabel.rectTransform.offsetMin = new Vector2(12f, 8f);
        promptBannerLabel.rectTransform.offsetMax = new Vector2(-12f, -8f);
        promptBannerLabel.raycastTarget = false;
        promptBannerObject.SetActive(false);
    }

    private void CreateAdaptiveResponsePopup()
    {
        adaptiveResponsePopup = FindFirstObjectByType<AdaptiveResponsePopup>();
        if (adaptiveResponsePopup != null && hudCanvas != null)
            adaptiveResponsePopup.Initialize(hudCanvas, promptBannerWellnessGap);
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

    private void PositionPromptBelowWellnessBar(RectTransform canvasRect)
    {
        if (promptBannerObject == null || canvasRect == null)
            return;

        float wellnessBottomLocalY = float.PositiveInfinity;
        RectTransform wellnessBarRect = hudCanvas != null
            ? hudCanvas.transform.Find("GameProgressBar") as RectTransform
            : null;
        RectTransform wellnessValueRect = hudCanvas != null
            ? hudCanvas.transform.Find("ProgressValueLabel") as RectTransform
            : null;

        if (wellnessBarRect != null)
            wellnessBottomLocalY = GetBottomEdgeCanvasLocalY(wellnessBarRect, canvasRect);
        if (wellnessValueRect != null)
            wellnessBottomLocalY = Mathf.Min(wellnessBottomLocalY, GetBottomEdgeCanvasLocalY(wellnessValueRect, canvasRect));

        float verticalGap = Mathf.Max(MinimumPromptWellnessGap, promptBannerWellnessGap);
        float yFromCanvasTop = verticalGap;
        if (!float.IsPositiveInfinity(wellnessBottomLocalY))
            yFromCanvasTop = wellnessBottomLocalY - canvasRect.rect.yMax - verticalGap;

        RectTransform bannerRect = promptBannerObject.transform as RectTransform;
        if (bannerRect != null)
            bannerRect.anchoredPosition = new Vector2(0f, yFromCanvasTop);
    }

    private static float GetBottomEdgeCanvasLocalY(RectTransform targetRect, RectTransform canvasRect)
    {
        Vector3[] corners = new Vector3[4];
        targetRect.GetWorldCorners(corners);
        float bottomLeftY = canvasRect.InverseTransformPoint(corners[0]).y;
        float bottomRightY = canvasRect.InverseTransformPoint(corners[3]).y;
        return Mathf.Min(bottomLeftY, bottomRightY);
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

    private void HandleMapClick(bool antigenIconClicked)
    {
        if (antigenIconClicked || !Input.GetMouseButtonDown(0))
            return;

        if (!TryGetInfectionTargetAtScreenPosition(Input.mousePosition, out InfectionMarker infectionTarget))
            return;

        NotifyInfectionTargetClicked(infectionTarget);
    }

    private bool TryGetInfectionTargetAtScreenPosition(Vector2 screenPosition, out InfectionMarker infectionTarget)
    {
        infectionTarget = null;
        if (EventSystem.current != null)
        {
            PointerEventData pointerData = new PointerEventData(EventSystem.current)
            {
                position = screenPosition
            };
            List<RaycastResult> raycastResults = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, raycastResults);
            foreach (RaycastResult result in raycastResults)
            {
                if (result.module is GraphicRaycaster)
                    return false;
            }
        }

        if (gameplayCamera == null)
            gameplayCamera = cameraScript != null ? cameraScript.GetComponentInChildren<Camera>() : Camera.main;
        if (gameplayCamera == null)
            return false;

        Ray ray = gameplayCamera.ScreenPointToRay(screenPosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, gameplayCamera.farClipPlane, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (first, second) => first.distance.CompareTo(second.distance));
        const string NotificationObjectPrefix = "Infection Notification - ";
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;

            Transform candidate = hit.collider.transform;
            while (candidate != null)
            {
                if (candidate.name.StartsWith(NotificationObjectPrefix, StringComparison.Ordinal))
                {
                    string infectionDisplayName = candidate.name.Substring(NotificationObjectPrefix.Length);
                    foreach (InfectionMarker marker in activeInfectionMarkers)
                    {
                        if (marker != null && !marker.isRemoving && marker.infection != null &&
                            string.Equals(marker.infection.displayName, infectionDisplayName, StringComparison.Ordinal))
                        {
                            infectionTarget = marker;
                            return true;
                        }
                    }
                }

                candidate = candidate.parent;
            }
        }

        return false;
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
            ApplyOxygenBuffSpeed(agent);
            if (agent != null && agent.enabled && agent.isOnNavMesh)
                agent.isStopped = true;
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
                if (selectedMarker.dispatchButton != null)
                    selectedMarker.dispatchButton.gameObject.SetActive(false);
                selectedMarker = null;
            }

            awaitingSquadDestination = true;
            awaitingInfectionTargetSelection = !useLegacySelectFirst;
            if (promptBannerLabel != null)
                promptBannerLabel.text = awaitingInfectionTargetSelection
                    ? "Click an infection to send your squad."
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
        Debug.Log("[Dispatch] Dispatch method started.");
        if (idleSquad == null || idleSquad.Length == 0)
            return false;

        DifficultyStats supplyStats = DifficultySettings.CurrentStats;
        int deployCost = Mathf.Max(0, supplyStats.deployCost);
        if (leukocyteSupply < deployCost)
        {
            LogWbcSquadMessage("Not enough leukocyte supply.", ConsoleLogUI.LogType.Warning);
            return false;
        }

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

        if (infectionTarget == null || infectionTarget.infection == null ||
            infectionTarget.infection.pathogenType != InfectionPathogenType.Viral)
        {
            GameObject dendriticCell = SpawnDendriticCell(infectionTarget, idleSquad[0].transform.position, out GameObject dendriticDestination);
            if (dendriticCell != null)
            {
                dispatchedDendriticCells.Add(new DispatchedDendriticCell
                {
                    infectionTarget = infectionTarget,
                    cell = dendriticCell,
                    destination = dendriticDestination
                });
            }
        }

        dispatchedWbcSquads.Add(new DispatchedWbcSquad
        {
            units = idleSquad,
            deploymentIndex = idleSquadDeploymentIndex,
            lifetimeRemaining = squadLifetimeSeconds,
            attackOnArrival = infectionTarget != null && infectionTarget.infection != null
                && infectionTarget.infection.pathogenType != InfectionPathogenType.Viral,
            infectionTarget = infectionTarget
        });
        SpendLeukocyteSupply(deployCost);
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

    private static Quaternion GetDendriticSpawnRotation(GameObject prefab, Vector3 lookDirection)
    {
        lookDirection.y = 0f;
        Quaternion prefabRotation = prefab.transform.localRotation;
        return lookDirection.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(lookDirection) * prefabRotation
            : prefabRotation;
    }

    private GameObject SpawnDendriticCell(InfectionMarker infectionTarget, Vector3 spawnPosition, out GameObject destinationObject)
    {
        destinationObject = null;
        if (dendriticPrefab == null || infectionTarget == null)
            return null;

        Vector3 lookDirection = infectionTarget.worldPosition - spawnPosition;
        Quaternion spawnRotation = GetDendriticSpawnRotation(dendriticPrefab, lookDirection);

        GameObject dendriticCell = Instantiate(dendriticPrefab, spawnPosition, spawnRotation);
        NavMeshAgent agent = dendriticCell.GetComponent<NavMeshAgent>();
        CirculatoryCellRoute route = dendriticCell.GetComponent<CirculatoryCellRoute>();
        if (agent == null || route == null || !agent.enabled)
        {
            Debug.LogWarning("[InfectionSpawner] The Dendritic Cell prefab requires an enabled NavMeshAgent and CirculatoryCellRoute.", this);
            Destroy(dendriticCell);
            return null;
        }

        agent.updateRotation = false;

        NavMeshQueryFilter filter = new NavMeshQueryFilter
        {
            agentTypeID = agent.agentTypeID,
            areaMask = agent.areaMask
        };
        Vector3 requestedDestination = infectionTarget.worldPosition + holdOffset;
        if (!NavMesh.SamplePosition(requestedDestination, out NavMeshHit destinationHit, dispatchNavMeshSampleRadius, filter))
        {
            Debug.LogWarning("[InfectionSpawner] The Dendritic Cell could not find a NavMesh position near the infection.", this);
            Destroy(dendriticCell);
            return null;
        }

        if (circulatorySystemController == null)
            circulatorySystemController = FindFirstObjectByType<CirculatorySystemController>();

        Transform heart = null;
        List<Transform> lungPoints = new List<Transform>();
        if (circulatorySystemController != null)
        {
            foreach (Transform waypoint in circulatorySystemController.GetComponentsInChildren<Transform>(true))
            {
                if (waypoint.name == "Heart")
                    heart = waypoint;
                else if (waypoint.name == "Point - Lung")
                    lungPoints.Add(waypoint);
            }
        }

        destinationObject = new GameObject(DendriticDestinationObjectName);
        destinationObject.transform.position = destinationHit.position;
        if (heart != null && lungPoints.Count > 0)
            route.Configure(agent, heart, destinationObject.transform, lungPoints.ToArray());

        route.PauseForThreat();
        agent.isStopped = false;
        if (!agent.SetDestination(destinationHit.position))
        {
            Destroy(destinationObject);
            destinationObject = null;
            Destroy(dendriticCell);
            return null;
        }

        return dendriticCell;
    }

    private void UpdateDendriticCells()
    {
        for (int index = dispatchedDendriticCells.Count - 1; index >= 0; index--)
        {
            DispatchedDendriticCell dispatchedCell = dispatchedDendriticCells[index];
            GameObject dendriticCell = dispatchedCell.cell;
            if (dendriticCell == null)
            {
                if (dispatchedCell.destination != null)
                    Destroy(dispatchedCell.destination);
                dispatchedDendriticCells.RemoveAt(index);
                continue;
            }

            NavMeshAgent agent = dendriticCell.GetComponent<NavMeshAgent>();
            if (agent == null || !agent.enabled || !agent.isOnNavMesh || agent.pathPending || !agent.hasPath
                || agent.pathStatus != NavMeshPathStatus.PathComplete)
            {
                continue;
            }

            if (agent.remainingDistance > Mathf.Max(agent.stoppingDistance, dispatchArrivalRadius))
                continue;

            agent.isStopped = true;
            if (dispatchedCell.destination != null)
            {
                Destroy(dispatchedCell.destination);
                dispatchedCell.destination = null;
            }
        }
    }

    private void UpdateContainedDendriticCells()
    {
        foreach (InfectionMarker marker in activeInfectionMarkers)
        {
            if (marker == null || !marker.isContained || marker.containedDendriticCell == null ||
                !marker.containedDendriticUsesProceduralIdle)
                continue;

            float bobOffset = Mathf.Sin(Time.time * ContainedDendriticIdleBobSpeed) * ContainedDendriticIdleBobHeight;
            marker.containedDendriticCell.transform.position = marker.containedDendriticBasePosition + Vector3.up * bobOffset;
        }
    }

    private void SpawnContainedDendriticCell(InfectionMarker marker)
    {
        if (marker == null || dendriticPrefab == null || marker.containedDendriticCell != null)
            return;

        Vector3 spawnPosition = marker.worldPosition + Vector3.right * 1.5f;
        Vector3 lookDirection = marker.worldPosition - spawnPosition;
        Quaternion spawnRotation = GetDendriticSpawnRotation(dendriticPrefab, lookDirection);

        GameObject dendriticCell = Instantiate(dendriticPrefab, spawnPosition, spawnRotation);
        marker.containedDendriticCell = dendriticCell;
        marker.containedDendriticBasePosition = dendriticCell.transform.position;

        NavMeshAgent agent = dendriticCell.GetComponent<NavMeshAgent>();
        if (agent != null && agent.enabled)
        {
            agent.updateRotation = false;
            if (agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.isStopped = true;
            }
            else
            {
                agent.enabled = false;
            }
        }

        Animator animator = dendriticCell.GetComponentInChildren<Animator>(true);
        bool hasIdleAnimation = animator != null && animator.runtimeAnimatorController != null &&
            animator.HasState(0, Animator.StringToHash("Idle"));
        marker.containedDendriticUsesProceduralIdle = !hasIdleAnimation;
        if (hasIdleAnimation)
            animator.Play("Idle", 0, 0f);
    }

    private bool CreatePendingAntigenCollection(InfectionMarker marker)
    {
        if (marker == null || marker.infection == null || marker.infection.pathogenType != InfectionPathogenType.Viral)
            return false;

        Sprite iconSprite = ResolveAntigenIconSprite();
        if (iconSprite == null)
        {
            Debug.LogWarning("[InfectionSpawner] The Dendritic antigen icon could not be loaded from Resources/UI.", this);
            return false;
        }

        GameObject icon = new GameObject("Antigen Collection Icon", typeof(SpriteRenderer), typeof(BoxCollider));
        icon.layer = LayerMask.NameToLayer("Default");
        SpriteRenderer iconRenderer = icon.GetComponent<SpriteRenderer>();
        iconRenderer.sprite = iconSprite;
        iconRenderer.sortingOrder = 30;
        iconRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        iconRenderer.receiveShadows = false;

        Vector3 iconPosition = marker.containedDendriticCell != null
            ? marker.containedDendriticCell.transform.position + Vector3.up * AntigenIconHeightOffset
            : marker.worldPosition + Vector3.right * 1.5f + Vector3.up * AntigenIconHeightOffset;
        icon.transform.position = iconPosition;
        icon.transform.localScale = Vector3.one * GetAntigenNotificationScale(iconSprite, marker.worldPosition);

        BoxCollider iconCollider = icon.GetComponent<BoxCollider>();
        iconCollider.enabled = true;
        iconCollider.isTrigger = false;
        Vector3 spriteSize = iconSprite.bounds.size;
        iconCollider.size = new Vector3(Mathf.Max(0.1f, spriteSize.x * AntigenIconColliderScale),
            Mathf.Max(0.1f, spriteSize.y * AntigenIconColliderScale), 0.4f);

        pendingAntigenCollections.Add(new PendingAntigenCollection
        {
            antigenName = marker.infection.displayName,
            dendriticCell = marker.containedDendriticCell,
            icon = icon,
            iconRenderer = iconRenderer,
            iconCollider = iconCollider,
            iconBasePosition = iconPosition,
            scaleAnchor = marker.worldPosition
        });
        marker.containedDendriticCell = null;
        marker.containedDendriticUsesProceduralIdle = false;
        return true;
    }

    private Sprite ResolveAntigenIconSprite()
    {
        if (resolvedAntigenIconSprite != null)
            return resolvedAntigenIconSprite;

        resolvedAntigenIconSprite = Resources.Load<Sprite>(AntigenIconResourcePath);
        if (resolvedAntigenIconSprite != null)
            return resolvedAntigenIconSprite;

        Texture2D iconTexture = Resources.Load<Texture2D>(AntigenIconResourcePath);
        if (iconTexture == null)
            return null;

        resolvedAntigenIconSprite = Sprite.Create(iconTexture,
            new Rect(0f, 0f, iconTexture.width, iconTexture.height), new Vector2(0.5f, 0.5f), NotificationSpritePixelsPerUnit);
        return resolvedAntigenIconSprite;
    }

    private void UpdatePendingAntigenCollections()
    {
        for (int index = pendingAntigenCollections.Count - 1; index >= 0; index--)
        {
            PendingAntigenCollection collection = pendingAntigenCollections[index];
            if (collection == null || collection.icon == null)
            {
                pendingAntigenCollections.RemoveAt(index);
                continue;
            }

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * AntigenIconPulseSpeed);
            Vector3 bobbedPosition = collection.iconBasePosition + Vector3.up *
                (Mathf.Sin(Time.unscaledTime * AntigenIconBobSpeed) * AntigenIconBobHeight);
            collection.icon.transform.position = bobbedPosition;
            collection.icon.transform.localScale = Vector3.one *
                GetAntigenNotificationScale(collection.iconRenderer != null ? collection.iconRenderer.sprite : null, collection.scaleAnchor);
            if (gameplayCamera != null)
            {
                Vector3 cameraDirection = gameplayCamera.transform.position - collection.icon.transform.position;
                if (cameraDirection.sqrMagnitude > 0.001f)
                    collection.icon.transform.rotation = Quaternion.LookRotation(cameraDirection);
            }

            if (collection.iconRenderer != null)
            {
                Color iconColor = collection.iconRenderer.color;
                iconColor.a = Mathf.Lerp(AntigenIconMinimumAlpha, AntigenIconMaximumAlpha, pulse);
                collection.iconRenderer.color = iconColor;
            }
        }
    }

    private float GetAntigenNotificationScale(Sprite iconSprite, Vector3 anchor)
    {
        float cameraScale = AntigenNotificationIconScale;
        if (gameplayCamera != null)
        {
            if (gameplayCamera.orthographic)
                cameraScale = Mathf.Max(0.01f, gameplayCamera.orthographicSize * AntigenNotificationIconScale);
            else
            {
                float distance = Vector3.Distance(gameplayCamera.transform.position, anchor);
                cameraScale = Mathf.Max(0.01f, distance * AntigenNotificationIconScale * 0.35f);
            }
        }

        if (iconSprite == null)
            return cameraScale;

        if (antigenScaleReferenceTexture == null)
            antigenScaleReferenceTexture = Resources.Load<Texture2D>(AntigenScaleReferenceIconResourcePath);
        if (antigenScaleReferenceTexture == null)
            return cameraScale;

        float referenceIconPixels = Mathf.Max(antigenScaleReferenceTexture.width, antigenScaleReferenceTexture.height);
        float antigenIconWorldSize = Mathf.Max(iconSprite.bounds.size.x, iconSprite.bounds.size.y);
        if (referenceIconPixels <= 0f || antigenIconWorldSize <= 0f)
            return cameraScale;

        float referenceIconWorldSize = referenceIconPixels / NotificationSpritePixelsPerUnit;
        return cameraScale * (referenceIconWorldSize / antigenIconWorldSize);
    }

    private bool HandleAntigenIconClick()
    {
        if (!Input.GetMouseButtonDown(0) || pendingAntigenCollections.Count == 0)
            return false;

        if (gameplayCamera == null)
            gameplayCamera = cameraScript != null ? cameraScript.GetComponentInChildren<Camera>() : Camera.main;
        if (gameplayCamera == null)
            return false;

        Ray ray = gameplayCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, gameplayCamera.farClipPlane,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);

        for (int index = pendingAntigenCollections.Count - 1; index >= 0; index--)
        {
            PendingAntigenCollection collection = pendingAntigenCollections[index];
            if (collection == null || collection.iconCollider == null || !collection.iconCollider.enabled)
                continue;

            bool colliderHit = false;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider != collection.iconCollider)
                    continue;

                colliderHit = true;
                break;
            }

            if (!colliderHit)
                colliderHit = collection.iconCollider.Raycast(ray, out RaycastHit iconHit, gameplayCamera.farClipPlane);
            if (!colliderHit && !IsScreenPointInsideColliderBounds(gameplayCamera, Input.mousePosition, collection.iconCollider))
                continue;

            CollectAntigen(collection);
            return true;
        }

        return false;
    }

    private static bool IsScreenPointInsideColliderBounds(Camera camera, Vector2 screenPosition, Collider targetCollider)
    {
        Bounds bounds = targetCollider.bounds;
        Vector3 minimum = bounds.min;
        Vector3 maximum = bounds.max;
        float minimumScreenX = float.PositiveInfinity;
        float maximumScreenX = float.NegativeInfinity;
        float minimumScreenY = float.PositiveInfinity;
        float maximumScreenY = float.NegativeInfinity;

        for (int corner = 0; corner < 8; corner++)
        {
            Vector3 worldCorner = new Vector3(
                (corner & 1) == 0 ? minimum.x : maximum.x,
                (corner & 2) == 0 ? minimum.y : maximum.y,
                (corner & 4) == 0 ? minimum.z : maximum.z);
            Vector3 screenCorner = camera.WorldToScreenPoint(worldCorner);
            if (screenCorner.z <= 0f)
                continue;

            minimumScreenX = Mathf.Min(minimumScreenX, screenCorner.x);
            maximumScreenX = Mathf.Max(maximumScreenX, screenCorner.x);
            minimumScreenY = Mathf.Min(minimumScreenY, screenCorner.y);
            maximumScreenY = Mathf.Max(maximumScreenY, screenCorner.y);
        }

        return screenPosition.x >= minimumScreenX && screenPosition.x <= maximumScreenX &&
            screenPosition.y >= minimumScreenY && screenPosition.y <= maximumScreenY;
    }

    private void CollectAntigen(PendingAntigenCollection collection)
    {
        if (collection == null)
            return;

        bool antigenAlreadyUnlocked = IsAntigenUnlocked(collection.antigenName);
        if (!antigenAlreadyUnlocked)
            Databank.AddAntigen(collection.antigenName);
        ShowAntigenPopup(collection.antigenName, antigenAlreadyUnlocked);
        if (collection.dendriticCell != null)
            Destroy(collection.dendriticCell);
        if (collection.icon != null)
            Destroy(collection.icon);
        pendingAntigenCollections.Remove(collection);
    }

    private static bool IsAntigenUnlocked(string antigenName)
    {
        if (string.IsNullOrWhiteSpace(antigenName) || Databank.UnlockedAntigens == null)
            return false;

        foreach (string unlockedAntigen in Databank.UnlockedAntigens)
        {
            if (string.Equals(unlockedAntigen, antigenName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private void DespawnDendriticCell(DispatchedDendriticCell dispatchedCell)
    {
        if (dispatchedCell == null)
            return;

        if (dispatchedCell.cell != null)
        {
            NavMeshAgent agent = dispatchedCell.cell.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.isStopped = true;
            }
            Destroy(dispatchedCell.cell);
            dispatchedCell.cell = null;
        }

        if (dispatchedCell.destination != null)
        {
            Destroy(dispatchedCell.destination);
            dispatchedCell.destination = null;
        }
    }

    private void DespawnDendriticCellsForInfection(InfectionMarker infectionTarget)
    {
        if (infectionTarget == null)
            return;

        if (infectionTarget.containedDendriticCell != null)
        {
            Destroy(infectionTarget.containedDendriticCell);
            infectionTarget.containedDendriticCell = null;
            infectionTarget.containedDendriticUsesProceduralIdle = false;
        }

        for (int index = dispatchedDendriticCells.Count - 1; index >= 0; index--)
        {
            DispatchedDendriticCell dispatchedCell = dispatchedDendriticCells[index];
            if (dispatchedCell.infectionTarget != infectionTarget)
                continue;

            DespawnDendriticCell(dispatchedCell);
            dispatchedDendriticCells.RemoveAt(index);
        }
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

            if (!keepAliveUntilResolved && !squad.holdingForContainment)
                squad.lifetimeRemaining -= gameplayDeltaTime;
            foreach (GameObject unit in squad.units)
            {
                if (unit == null || !unit.activeInHierarchy)
                    continue;

                NavMeshAgent agent = unit.GetComponent<NavMeshAgent>();
                bool hasArrived = squad.arrivedUnits.Contains(unit);
                UpdateUnitMovementAnimation(unit, agent, !hasArrived);
                if (hasArrived)
                    continue;

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
                    if (!squad.infectionTarget.isTagged)
                        BeginInfectionResolution(squad.infectionTarget);
                    if (squad.infectionTarget.isContained
                        && squad.infectionTarget.infection.pathogenType == InfectionPathogenType.Viral)
                    {
                        squad.holdingForContainment = true;
                    }
                }
                if (squad.attackOnArrival)
                    squad.combatStarted = true;
            }

            if ((keepAliveUntilResolved || squad.holdingForContainment)
                && squad.infectionTarget != null && squad.infectionTarget.isRemoving)
            {
                DespawnWbcSquad(squadIndex);
                continue;
            }

            if (!keepAliveUntilResolved && !squad.holdingForContainment && squad.lifetimeRemaining <= 0f)
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
            if (!squad.combatStarted || marker == null || marker.isRemoving || marker.isTagged)
                continue;
            if (marker.infection != null && marker.infection.pathogenType == InfectionPathogenType.Viral)
            {
                if (marker.isContained)
                    squad.holdingForContainment = true;
                continue;
            }
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
                    MacrophageScript macrophage = wbc.GetComponent<MacrophageScript>();
                    if (TryApplyCombatHit(wbc, nearestPathogen, wbcHealth, stats.wbcAttack * multiplier, stats.attackIntervalSeconds)
                        && macrophage != null)
                    {
                        TriggerMacrophageCombatAnimation(macrophage, nearestPathogen);
                    }
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
                        wbcCombatMoveSpeed * (isOxygenBuffActive ? OxygenBuffMovementMultiplier : 1f) * gameplayDeltaTime);
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

    private void UpdateLymphaticBatchInfectionResolutions()
    {
        if (lymphaticBatchSpawner == null)
            lymphaticBatchSpawner = FindFirstObjectByType<LymphaticBatchSpawner>();
        if (lymphaticBatchSpawner == null)
            return;

        DifficultyStats stats = DifficultySettings.CurrentStats;
        lymphaticBatchSpawner.GetBatches(activeLymphaticBatches);
        foreach (LymphaticBatchGroup batch in activeLymphaticBatches)
        {
            if (batch == null || batch.Status != LymphaticBatchGroup.BatchStatus.Working)
                continue;

            InfectionMarker marker = batch.TargetInfection;
            if (marker == null || marker.isRemoving || marker.isContained || marker.infection == null)
                continue;

            bool isBCellBatch = string.Equals(batch.UnitType, BCellUnitName, StringComparison.OrdinalIgnoreCase);
            if (isBCellBatch)
            {
                if (marker.isTagged)
                    ReturnLymphaticBatchToReady(batch.BatchId);
                else if (!marker.bCellTagInProgress)
                    BeginBCellTagging(batch, marker);
                continue;
            }

            if (marker.infection.pathogenType == InfectionPathogenType.Viral)
            {
                BeginInfectionResolution(marker, batch);
                continue;
            }

            if (marker.isTagged)
                continue;

            BeginInfectionResolution(marker);
            activeBatchPathogens.Clear();
            foreach (GameObject pathogen in marker.threatVisuals)
            {
                Health pathogenHealth = pathogen != null ? pathogen.GetComponent<Health>() : null;
                if (pathogen != null && pathogen.activeInHierarchy && pathogenHealth != null && !pathogenHealth.IsDead)
                    activeBatchPathogens.Add(pathogen);
            }

            bool isMacrophageBatch = string.Equals(batch.UnitType, "Macrophage", StringComparison.OrdinalIgnoreCase);
            float attackRange = isMacrophageBatch
                ? Mathf.Max(combatMeleeRange, MacrophageAttackRange)
                : combatMeleeRange;
            foreach (GameObject member in batch.Members)
            {
                if (member == null || !member.activeInHierarchy)
                    continue;

                MacrophageScript macrophage = isMacrophageBatch ? member.GetComponent<MacrophageScript>() : null;
                Health memberHealth = member.GetComponent<Health>();
                if (memberHealth == null && isMacrophageBatch)
                    memberHealth = member.AddComponent<Health>();

                if (isMacrophageBatch && memberHealth != null && !memberHealth.IsDead)
                {
                    float baseMacrophageHealth = macrophage != null ? Mathf.Max(1, macrophage.health) : MacrophageDefaultHealth;
                    float macrophageHealthScale = Mathf.Max(0f, stats.wbcHp) / NormalDifficultyWbcHp;
                    float targetMacrophageHealth = Mathf.Max(1f, baseMacrophageHealth * macrophageHealthScale);
                    if (!Mathf.Approximately(memberHealth.MaxHp, targetMacrophageHealth))
                        memberHealth.SetMaxHp(targetMacrophageHealth);
                }

                if (memberHealth == null || memberHealth.IsDead)
                    continue;

                float attackDamage = stats.wbcAttack;
                if (isMacrophageBatch)
                {
                    float baseMacrophageDamage = macrophage != null ? macrophage.damage : MacrophageDefaultAttackDamage;
                    float macrophageDamageScale = Mathf.Max(0f, stats.wbcAttack) / NormalDifficultyWbcAttack;
                    attackDamage = baseMacrophageDamage * macrophageDamageScale;
                }
                else
                {
                    NeutrophilScript neutrophil = member.GetComponent<NeutrophilScript>();
                    if (neutrophil != null)
                    {
                        string neutrophilVariant = GetNeutrophilVariantName(member);
                        attackDamage = Mathf.Max(0f, neutrophil.damage);
                        if (string.Equals(neutrophilVariant, EosinophilVariantName, StringComparison.OrdinalIgnoreCase) && IsParasiteInfection(marker.infection))
                            attackDamage *= InnateVariantSpecialistDamageMultiplier;
                        else if (string.Equals(neutrophilVariant, BasophilVariantName, StringComparison.OrdinalIgnoreCase) && IsAllergenInfection(marker.infection))
                            attackDamage *= InnateVariantSpecialistDamageMultiplier;
                    }
                }

                GameObject nearestPathogen = FindNearestLivingUnit(activeBatchPathogens, member.transform.position, out float distance);
                if (nearestPathogen == null || distance > combatDetectionRange)
                    continue;

                NavMeshAgent agent = member.GetComponent<NavMeshAgent>();
                if (distance > attackRange)
                {
                    if (agent != null && agent.enabled && agent.isOnNavMesh && !agent.pathPending &&
                        (!agent.hasPath || (agent.destination - nearestPathogen.transform.position).sqrMagnitude > 0.25f))
                    {
                        agent.isStopped = false;
                        agent.SetDestination(nearestPathogen.transform.position);
                    }
                    continue;
                }

                if (agent != null && agent.enabled && agent.isOnNavMesh && agent.hasPath)
                {
                    agent.ResetPath();
                    agent.isStopped = true;
                }

                if (TryApplyCombatHit(member, nearestPathogen, memberHealth, attackDamage, stats.attackIntervalSeconds)
                    && macrophage != null)
                {
                    TriggerMacrophageCombatAnimation(macrophage, nearestPathogen);
                }
            }
        }
    }
    private void BeginBCellTagging(LymphaticBatchGroup batch, InfectionMarker marker)
    {
        if (batch == null || marker == null || marker.isTagged || marker.bCellTagInProgress)
            return;

        GameObject tagger = null;
        foreach (GameObject member in batch.Members)
        {
            if (member == null || !member.activeInHierarchy)
                continue;

            tagger = member;
            break;
        }

        if (tagger == null)
            return;

        Animator animator = tagger.GetComponentInChildren<Animator>(true);
        if (animator == null || !HasAnimatorParameter(animator, AnimatorTagParameter, AnimatorControllerParameterType.Trigger))
        {
            Debug.LogWarning($"[Resolution] B-Cell '{tagger.name}' cannot tag {marker.infection.displayName}: its Animator has no '{AnimatorTagParameter}' trigger.", this);
            return;
        }

        marker.bCellTagInProgress = true;
        StartCoroutine(PlayBCellTagAnimation(tagger, animator, marker, batch.BatchId));
    }

    private IEnumerator PlayBCellTagAnimation(GameObject tagger, Animator animator, InfectionMarker marker, int batchId)
    {
        if (tagger == null || animator == null || marker == null || marker.isRemoving)
        {
            if (marker != null)
                marker.bCellTagInProgress = false;
            yield break;
        }

        if (!animator.enabled)
            animator.enabled = true;
        if (HasAnimatorParameter(animator, AnimatorMovingParameter, AnimatorControllerParameterType.Bool))
            animator.SetBool(AnimatorMovingParameter, false);
        animator.ResetTrigger(AnimatorTagParameter);
        animator.SetTrigger(AnimatorTagParameter);

        int tagStateHash = Animator.StringToHash("Tag");
        float elapsed = 0f;
        bool enteredTagState = false;
        while (tagger != null && animator != null && marker != null && !marker.isRemoving)
        {
            if (animator.GetCurrentAnimatorStateInfo(0).shortNameHash == tagStateHash)
            {
                enteredTagState = true;
                break;
            }

            elapsed += Time.deltaTime;
            if (elapsed >= BCellTagAnimationTimeoutSeconds)
                break;
            yield return null;
        }

        if (!enteredTagState)
        {
            if (marker != null)
            {
                marker.bCellTagInProgress = false;
                Debug.LogWarning("[Resolution] B-Cell Tag animation did not enter its Tag state.", this);
            }
            yield break;
        }

        elapsed = 0f;
        while (tagger != null && animator != null && marker != null && !marker.isRemoving)
        {
            AnimatorStateInfo tagState = animator.GetCurrentAnimatorStateInfo(0);
            if (tagState.shortNameHash != tagStateHash || tagState.normalizedTime >= 1f)
                break;

            elapsed += Time.deltaTime;
            if (elapsed >= BCellTagAnimationTimeoutSeconds)
                break;
            yield return null;
        }

        if (tagger != null && animator != null && marker != null && !marker.isRemoving && marker.infection != null)
        {
            AnimatorStateInfo finalState = animator.GetCurrentAnimatorStateInfo(0);
            if (finalState.shortNameHash == tagStateHash && finalState.normalizedTime < 1f)
            {
                marker.bCellTagInProgress = false;
                Debug.LogWarning("[Resolution] B-Cell Tag animation timed out before completion.", this);
                yield break;
            }

            marker.bCellTagInProgress = false;
            marker.isTagged = true;
            ApplyTaggedPathogenVisuals(marker);
            Debug.Log($"[Resolution] B-Cell antibodies tagged {marker.infection.displayName}.", this);
            animator.Play(AnimatorIdleStateName, 0, 0f);
            ReturnLymphaticBatchToReady(batchId);
        }
        else if (marker != null)
        {
            marker.bCellTagInProgress = false;
        }
    }

    private void ApplyTaggedPathogenVisuals(InfectionMarker marker)
    {
        if (marker == null)
            return;

        foreach (GameObject pathogen in marker.threatVisuals)
        {
            if (pathogen == null)
                continue;

            Renderer[] renderers = pathogen.GetComponentsInChildren<Renderer>();
            foreach (Renderer pathogenRenderer in renderers)
            {
                if (pathogenRenderer == null)
                    continue;

                MaterialPropertyBlock properties = new MaterialPropertyBlock();
                pathogenRenderer.GetPropertyBlock(properties);
                bool hasColorProperty = false;
                foreach (Material sharedMaterial in pathogenRenderer.sharedMaterials)
                {
                    if (sharedMaterial == null)
                        continue;

                    if (sharedMaterial.HasProperty(BaseColorPropertyId))
                    {
                        properties.SetColor(BaseColorPropertyId, AntibodyGoldColor);
                        hasColorProperty = true;
                    }
                    if (sharedMaterial.HasProperty(ColorPropertyId))
                    {
                        properties.SetColor(ColorPropertyId, AntibodyGoldColor);
                        hasColorProperty = true;
                    }
                }

                if (hasColorProperty)
                    pathogenRenderer.SetPropertyBlock(properties);
            }
        }
    }

    private void ReturnLymphaticBatchToReady(int batchId)
    {
        if (lymphaticBatchSpawner == null)
            lymphaticBatchSpawner = FindFirstObjectByType<LymphaticBatchSpawner>();
        if (lymphaticBatchSpawner != null)
            lymphaticBatchSpawner.AbortBatch(batchId);
    }

    private void UpdateTaggedInfectionResolutions()
    {
        HashSet<InfectionMarker> taggedMarkersWithResponders = new HashSet<InfectionMarker>();
        foreach (DispatchedWbcSquad squad in dispatchedWbcSquads)
        {
            InfectionMarker marker = squad != null ? squad.infectionTarget : null;
            if (squad != null && squad.arrivalMessageLogged && marker != null && marker.isTagged && !marker.isRemoving)
                taggedMarkersWithResponders.Add(marker);
        }

        if (lymphaticBatchSpawner == null)
            lymphaticBatchSpawner = FindFirstObjectByType<LymphaticBatchSpawner>();
        if (lymphaticBatchSpawner != null)
        {
            lymphaticBatchSpawner.GetBatches(activeLymphaticBatches);
            foreach (LymphaticBatchGroup batch in activeLymphaticBatches)
            {
                InfectionMarker marker = batch != null ? batch.TargetInfection : null;
                if (batch == null || batch.Status != LymphaticBatchGroup.BatchStatus.Working
                    || marker == null || !marker.isTagged || marker.isRemoving)
                    continue;

                bool isInnateResponder = string.Equals(batch.UnitType, "Neutrophil", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(batch.UnitType, "WBC", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(batch.UnitType, "Macrophage", StringComparison.OrdinalIgnoreCase);
                if (isInnateResponder)
                    taggedMarkersWithResponders.Add(marker);
            }
        }

        foreach (InfectionMarker marker in taggedMarkersWithResponders)
            ResolveTaggedInfectionImmediately(marker);
    }

    private void ResolveTaggedInfectionImmediately(InfectionMarker marker)
    {
        if (marker == null || !marker.isTagged || marker.isRemoving || marker.resolutionPending || marker.infection == null)
            return;

        marker.resolutionPending = true;
        AddLeukocyteSupply(clearBonus);
        ApplyWellnessGainOnResolve(marker);
        PlayInfectionClearedSfx();
        Debug.Log($"[Resolution] Opsonization cleared {marker.infection.displayName} upon immune-cell arrival.", this);
        LogWbcSquadMessage($"Opsonization: immune cells cleared {marker.infection.displayName}.", ConsoleLogUI.LogType.Success);
        BeginRemovingInfectionMarker(marker);
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

    private void UpdateLymphaticBatchAnimations()
    {
        if (lymphaticBatchSpawner == null)
            lymphaticBatchSpawner = FindFirstObjectByType<LymphaticBatchSpawner>();
        if (lymphaticBatchSpawner == null)
            return;

        lymphaticBatchSpawner.GetBatches(activeLymphaticBatches);
        bCellTintedUnits.RemoveWhere(unit => unit == null);
        lymphaticBatchAttackAnimationsTriggered.RemoveWhere(batch =>
            batch == null || !activeLymphaticBatches.Contains(batch) || batch.Status != LymphaticBatchGroup.BatchStatus.Working);

        foreach (LymphaticBatchGroup batch in activeLymphaticBatches)
        {
            if (batch == null)
                continue;

            InfectionMarker target = batch.TargetInfection;
            bool isBCellBatch = string.Equals(batch.UnitType, BCellUnitName, StringComparison.OrdinalIgnoreCase);
            bool isKillerTCellViralClash = target != null && target.infection != null &&
                target.infection.pathogenType == InfectionPathogenType.Viral &&
                string.Equals(batch.UnitType, TCellUnitName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(batch.PathogenVariant, target.infection.displayName, StringComparison.OrdinalIgnoreCase);
            bool isMovingBatch = batch.Status == LymphaticBatchGroup.BatchStatus.Mobilizing ||
                (batch.Status == LymphaticBatchGroup.BatchStatus.Working && !isKillerTCellViralClash);
            foreach (GameObject member in batch.Members)
            {
                if (member == null || !member.activeInHierarchy)
                    continue;

                if (isBCellBatch)
                {
                    BacteriaCellResponder bacteriaResponder = member.GetComponent<BacteriaCellResponder>();
                    if (bacteriaResponder != null && bacteriaResponder.enabled)
                        bacteriaResponder.enabled = false;
                    ApplyBCellTint(member);
                }
                UpdateUnitMovementAnimation(member, member.GetComponent<NavMeshAgent>(), isMovingBatch);
            }

            bool isBacterialClash = target != null && target.infection != null &&
                target.infection.pathogenType != InfectionPathogenType.Viral;
            bool shouldPlayCombatAttack = (isBacterialClash && !isBCellBatch) || isKillerTCellViralClash;
            if (batch.Status == LymphaticBatchGroup.BatchStatus.Working && shouldPlayCombatAttack &&
                lymphaticBatchAttackAnimationsTriggered.Add(batch))
            {
                foreach (GameObject member in batch.Members)
                {
                    if (member != null && member.activeInHierarchy)
                        TriggerUnitAttackAnimation(member);
                }
            }
        }
    }

    private void ApplyBCellTint(GameObject unit)
    {
        if (unit == null || !bCellTintedUnits.Add(unit))
            return;

        Renderer[] renderers = unit.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer cellRenderer in renderers)
        {
            if (cellRenderer == null)
                continue;

            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            cellRenderer.GetPropertyBlock(properties);
            bool hasTintProperty = false;
            foreach (Material sharedMaterial in cellRenderer.sharedMaterials)
            {
                if (sharedMaterial == null)
                    continue;

                if (sharedMaterial.HasProperty(BaseColorPropertyId))
                {
                    properties.SetColor(BaseColorPropertyId, AntibodyGoldColor);
                    hasTintProperty = true;
                }
                if (sharedMaterial.HasProperty(ColorPropertyId))
                {
                    properties.SetColor(ColorPropertyId, AntibodyGoldColor);
                    hasTintProperty = true;
                }
            }

            if (hasTintProperty)
                cellRenderer.SetPropertyBlock(properties);
        }
    }

    private static void UpdateUnitMovementAnimation(GameObject unit, NavMeshAgent agent, bool movementAllowed)
    {
        if (unit == null)
            return;

        MacrophageScript macrophage = unit.GetComponent<MacrophageScript>();
        if (macrophage != null && macrophage.enabled)
            return;

        Animator animator = unit.GetComponentInChildren<Animator>(true);
        if (animator == null)
            return;

        if (!animator.enabled)
            animator.enabled = true;

        if (!HasAnimatorParameter(animator, AnimatorMovingParameter, AnimatorControllerParameterType.Bool))
            return;

        bool isMoving = movementAllowed && agent != null && agent.enabled && agent.isOnNavMesh &&
            !agent.isStopped && !agent.pathPending && agent.hasPath &&
            (agent.velocity.sqrMagnitude > AnimatorMovementThreshold * AnimatorMovementThreshold ||
             agent.remainingDistance > Mathf.Max(agent.stoppingDistance, 0.01f));
        animator.SetBool(AnimatorMovingParameter, isMoving);
    }

    private static void TriggerUnitAttackAnimation(GameObject unit)
    {
        if (unit == null)
            return;

        if (unit.GetComponent<MacrophageScript>() != null)
            return;

        Animator animator = unit.GetComponentInChildren<Animator>(true);
        if (animator == null)
            return;

        if (!animator.enabled)
            animator.enabled = true;
        if (HasAnimatorParameter(animator, AnimatorMovingParameter, AnimatorControllerParameterType.Bool))
            animator.SetBool(AnimatorMovingParameter, false);
        if (HasAnimatorParameter(animator, AnimatorAttackParameter, AnimatorControllerParameterType.Trigger))
            animator.SetTrigger(AnimatorAttackParameter);
    }

    private static void TriggerUnitVictoryAnimation(GameObject unit)
    {
        if (unit == null)
            return;

        Animator animator = unit.GetComponentInChildren<Animator>(true);
        if (animator == null)
            return;

        if (!animator.enabled)
            animator.enabled = true;
        if (HasAnimatorParameter(animator, AnimatorMovingParameter, AnimatorControllerParameterType.Bool))
            animator.SetBool(AnimatorMovingParameter, false);
        if (HasAnimatorParameter(animator, AnimatorAttackParameter, AnimatorControllerParameterType.Trigger))
            animator.ResetTrigger(AnimatorAttackParameter);
        if (HasAnimatorParameter(animator, AnimatorVictoryParameter, AnimatorControllerParameterType.Trigger))
            animator.SetTrigger(AnimatorVictoryParameter);
    }

    private static bool HasAnimatorParameter(Animator animator, string parameterName, AnimatorControllerParameterType parameterType)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return false;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == parameterName && parameter.type == parameterType)
                return true;
        }

        return false;
    }


    private bool TryApplyCombatHit(GameObject attacker, GameObject target, Health attackerHealth, float damage, float attackInterval)
    {
        if (attacker == null || target == null || attackerHealth == null || damage <= 0f || !attackerHealth.TryStartAttack(attackInterval))
            return false;

        Health targetHealth = target.GetComponent<Health>();
        if (targetHealth == null || targetHealth.IsDead)
            return false;

        targetHealth.TakeDamage(damage);
        if (verboseCombatLogging)
            Debug.Log($"[Combat] {attacker.name} hit {target.name} for {damage:0.##} damage; remaining HP={targetHealth.CurrentHp:0.##}.", this);
        return true;
    }

    private static void TriggerMacrophageCombatAnimation(MacrophageScript macrophage, GameObject pathogen)
    {
        if (macrophage == null)
            return;

        Health pathogenHealth = pathogen != null ? pathogen.GetComponent<Health>() : null;
        if (pathogenHealth != null && pathogenHealth.IsDead)
            macrophage.TriggerTauntAnimation();
        else
            macrophage.TriggerAttackAnimation();
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



    private void BeginInfectionResolution(InfectionMarker marker, LymphaticBatchGroup sourceBatch = null)
    {
        if (marker == null || marker.isRemoving || marker.isContained || marker.resolutionPending || marker.infection == null)
            return;

        bool isViral = marker.infection.pathogenType == InfectionPathogenType.Viral;
        bool isKillerTCell = sourceBatch != null &&
            string.Equals(sourceBatch.UnitType, TCellUnitName, StringComparison.OrdinalIgnoreCase);
        if (isViral && isKillerTCell)
        {
            if (!string.Equals(sourceBatch.PathogenVariant, marker.infection.displayName, StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning($"[Resolution] {sourceBatch.DisplayName} cannot clear {marker.infection.displayName}; the T-Cell specialization does not match.", this);
                return;
            }

            marker.resolutionPending = true;
            StartCoroutine(ResolveViralInfectionByKillerTCell(marker, sourceBatch));
            return;
        }

        if (!isViral)
        {
            marker.resolutionPending = true;
            StartCoroutine(ResolveBacterialInfection(marker));
            return;
        }

        marker.infection = CloneInfectionData(marker.infection);
        int reducedSeverity = Mathf.Max((int)InfectionSeverityStage.Mild,
            (int)marker.infection.severityStage - containedSeverityDropSteps);
        marker.infection.severityStage = (InfectionSeverityStage)reducedSeverity;
        marker.isContained = true;
        int minimumContainmentDays = Mathf.Clamp(containedSelfResolveDays,
            MinimumViralContainmentDays, MaximumViralContainmentDays);
        int containmentDays = UnityEngine.Random.Range(minimumContainmentDays, MaximumViralContainmentDays + 1);
        marker.containedResolveDay = (dayCounter != null ? dayCounter.CurrentDay : 0) + containmentDays;
        SpawnContainedDendriticCell(marker);
        AddLeukocyteSupply(containmentBonus);
        LogWbcSquadMessage($"WBC contained {marker.infection.displayName}, but the virus is hiding inside cells. Adaptive response is building.", ConsoleLogUI.LogType.Warning);
        ApplyWellnessGainOnResolve(marker);
    }

    private IEnumerator ResolveViralInfectionByKillerTCell(InfectionMarker marker, LymphaticBatchGroup sourceBatch)
    {
        yield return GameplaySpeed.WaitForGameplaySeconds(KillerTCellAttackDurationSeconds);
        if (marker == null || marker.isRemoving || !activeInfectionMarkers.Contains(marker))
            yield break;

        foreach (GameObject member in sourceBatch.Members)
        {
            if (member != null && member.activeInHierarchy)
                TriggerUnitVictoryAnimation(member);
        }

        yield return GameplaySpeed.WaitForGameplaySeconds(KillerTCellWarCryDurationSeconds);
        if (marker == null || marker.isRemoving || !activeInfectionMarkers.Contains(marker))
            yield break;

        Debug.Log($"[Resolution] Killer T-Cell induced apoptosis in {marker.infection.displayName}. Infection cleared.", this);
        ApplyWellnessGainOnResolve(marker);
        BeginRemovingInfectionMarker(marker);
    }


    private IEnumerator ResolveBacterialInfection(InfectionMarker marker)
    {
        if (useFixedDurationClash)
            yield return GameplaySpeed.WaitForGameplaySeconds(bacterialClashDurationSeconds);
        if (marker == null || marker.isRemoving || !activeInfectionMarkers.Contains(marker))
            yield break;

        AddLeukocyteSupply(clearBonus);

        ApplyWellnessGainOnResolve(marker);
        PlayInfectionClearedSfx();
        LogWbcSquadMessage($"Phagocytosis: WBC engulfed {marker.infection.displayName}. Infection cleared.", ConsoleLogUI.LogType.Success);
        BeginRemovingInfectionMarker(marker);
    }

    private void PlayInfectionClearedSfx()
    {
        if (infectionClearedSfx == null || infectionAudioSource == null)
            return;

        infectionAudioSource.PlayOneShot(infectionClearedSfx);
    }

    private void ApplyWellnessGainOnResolve(InfectionMarker marker)
    {
        if (marker == null || marker.wellnessGainAwardedOnResolve || marker.infection == null || WellnessManager.Instance == null)
            return;

        marker.wellnessGainAwardedOnResolve = true;
        float resolveGain = marker.infection.resolveGain > 0
            ? marker.infection.resolveGain
            : GetBacterialWellnessGain(marker.infection.severityStage);
        ApplyWellnessChange($"{marker.infection.displayName} resolved", resolveGain);
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
            pathogenDatabaseName = source.pathogenDatabaseName,
            pathogenType = source.pathogenType,
            rarity = source.rarity,
            severityStage = source.severityStage,
            resolveGain = source.resolveGain,
            neglectLoss = source.neglectLoss,
            easySpawnWeight = source.easySpawnWeight,
            normalSpawnWeight = source.normalSpawnWeight,
            mediumSpawnWeight = source.mediumSpawnWeight,
            hardSpawnWeight = source.hardSpawnWeight,
            isGeneric = source.isGeneric,
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

            float containedWellnessCost = containedDailyWellnessCost;
            if (marker.wasBarrierMutated)
                containedWellnessCost *= BreachedBarrierPathogenMultiplier;
            ApplyWellnessChange($"Contained infection {marker.infection.displayName}", -containedWellnessCost);

            if (currentDay < marker.containedResolveDay)
                continue;

            string infectionName = marker.infection.displayName;
            if (marker.infection.pathogenType == InfectionPathogenType.Viral && !CreatePendingAntigenCollection(marker))
                continue;

            PlayInfectionClearedSfx();
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
            float dailyCost = marker.infection.neglectLoss > 0
                ? marker.infection.neglectLoss
                : GetDailyInfectionWellnessCost(marker.infection.severityStage);
            if (marker.wasBarrierMutated)
                dailyCost *= BreachedBarrierPathogenMultiplier;
            ApplyWellnessChange($"{marker.infection.displayName} unresolved", -dailyCost);

            if (marker.unresolvedDays >= 2 && marker.infection.neglectLoss <= 0)
            {
                float additionalCost = additionalUnresolvedDailyWellnessCost;
                if (marker.wasBarrierMutated)
                    additionalCost *= BreachedBarrierPathogenMultiplier;
                ApplyWellnessChange($"{marker.infection.displayName} unresolved 2+ days", -additionalCost);
            }
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
        if (unit == null)
            return;

        MacrophageScript macrophage = unit.GetComponent<MacrophageScript>();
        if (macrophage != null)
        {
            macrophage.TriggerArrivalAnimation();
            return;
        }

        Animator animator = unit.GetComponentInChildren<Animator>(true);
        if (animator == null)
            return;

        if (!animator.enabled)
            animator.enabled = true;
        if (HasAnimatorParameter(animator, AnimatorMovingParameter, AnimatorControllerParameterType.Bool))
            animator.SetBool(AnimatorMovingParameter, false);
        if (attackOnArrival && HasAnimatorParameter(animator, AnimatorAttackParameter, AnimatorControllerParameterType.Trigger))
            animator.SetTrigger(AnimatorAttackParameter);
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
        int currentSquads = CountActiveSquads();
        int maxSquads = Mathf.Max(1, DifficultySettings.CurrentStats.maxActiveSquads);
        if (squadCountLabel != null)
            squadCountLabel.text = $"Squads {currentSquads}/{maxSquads}";

        if (wbcHeadshotImage != null)
            wbcHeadshotImage.color = currentSquads >= maxSquads
                ? new Color(1f, 0.4f, 0.4f, 1f)
                : Color.white;

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

        RefreshLeukocyteSupplyBar();
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

    private void UpdateInfectionMarkerAnimations()
    {
        float gameplayDeltaTime = GameplaySpeed.DeltaTime;
        for (int index = fadingInfectionMarkers.Count - 1; index >= 0; index--)
        {
            InfectionMarker marker = fadingInfectionMarkers[index];
            marker.animationElapsed += gameplayDeltaTime;
            float fadeProgress = markerFadeDuration > 0f ? Mathf.Clamp01(marker.animationElapsed / markerFadeDuration) : 1f;
            if (marker.canvasGroup != null)
                marker.canvasGroup.alpha = 1f - fadeProgress;
            if (fadeProgress >= 1f)
            {
                if (marker.dispatchButton != null)
                    marker.dispatchButton.gameObject.SetActive(false);
                fadingInfectionMarkers.RemoveAt(index);
            }
        }
    }

    /// <summary>Removes the first active matching infection marker.</summary>
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

        bool shouldDespawnResolvedSquads = marker.infection == null
            || marker.infection.pathogenType != InfectionPathogenType.Viral
            || marker.isContained
            || marker.resolutionPending;
        if (shouldDespawnResolvedSquads)
        {
            for (int squadIndex = dispatchedWbcSquads.Count - 1; squadIndex >= 0; squadIndex--)
            {
                DispatchedWbcSquad squad = dispatchedWbcSquads[squadIndex];
                if (squad != null && squad.infectionTarget == marker)
                    DespawnWbcSquad(squadIndex);
            }

            if (lymphaticBatchSpawner == null)
                lymphaticBatchSpawner = FindFirstObjectByType<LymphaticBatchSpawner>();
            if (lymphaticBatchSpawner != null)
            {
                lymphaticBatchSpawner.GetBatches(activeLymphaticBatches);
                for (int batchIndex = activeLymphaticBatches.Count - 1; batchIndex >= 0; batchIndex--)
                {
                    LymphaticBatchGroup batch = activeLymphaticBatches[batchIndex];
                    if (batch != null && batch.TargetInfection == marker)
                        lymphaticBatchSpawner.DestroyBatch(batch.BatchId);
                }
            }
        }

        DespawnDendriticCellsForInfection(marker);
        marker.isRemoving = true;
        marker.animationElapsed = 0f;
        if (marker.dispatchButton != null)
            marker.dispatchButton.interactable = false;
        activeInfectionMarkers.Remove(marker);
        fadingInfectionMarkers.Add(marker);
        if (selectedMarker == marker)
            selectedMarker = null;
    }



    private void HandleDayAdvanced(int currentDay)
    {
        UpdateContainedInfections(currentDay);
        ApplyDailyUnresolvedInfectionCosts();
    }

    private void HandleHourAdvanced(int currentDay, int currentHour)
    {
        if (!infectionSpawnTimerInitialized)
            ResetInfectionSpawnTimer();

        hoursUntilNextInfectionSpawn--;
        if (hoursUntilNextInfectionSpawn > 0)
            return;

        SpawnTimedInfection(currentDay, currentHour);
        ResetInfectionSpawnTimer();
        Debug.Log($"[InfectionSpawner] Next infection spawn scheduled in {hoursUntilNextInfectionSpawn} in-game hours.", this);
    }

    private void ResetInfectionSpawnTimer()
    {
        hoursUntilNextInfectionSpawn = UnityEngine.Random.Range(
            MinimumHoursBetweenInfectionSpawns,
            MaximumHoursBetweenInfectionSpawns + 1);
        infectionSpawnTimerInitialized = true;
    }

    private bool ApplyBarrierMutation(InfectionData infection)
    {
        if (infection == null || !IsAnyActiveBarrierBreached())
            return false;

        switch (infection.severityStage)
        {
            case InfectionSeverityStage.Mild:
                infection.severityStage = InfectionSeverityStage.Moderate;
                break;
            case InfectionSeverityStage.Moderate:
                infection.severityStage = InfectionSeverityStage.Severe;
                break;
        }

        Debug.Log("[InfectionSpawner] Pathogen mutated by breached barrier!", this);
        return true;
    }

    private static bool IsAnyActiveBarrierBreached()
    {
        BarrierHealth[] barriers = FindObjectsByType<BarrierHealth>(FindObjectsSortMode.None);
        foreach (BarrierHealth barrier in barriers)
        {
            if (barrier != null && barrier.isActiveAndEnabled && barrier.IsBreached)
                return true;
        }

        return false;
    }

    private void SpawnTimedInfection(int currentDay, int currentHour)
    {
        if (activeInfectionMarkers.Count >= maxActiveInfections)
        {
            Debug.LogWarning($"[InfectionSpawner] Skipped scheduled infection at Day {currentDay}, Hour {currentHour:00}: active infection cap reached ({maxActiveInfections}).", this);
            return;
        }

        if (GetValidBodyPartButtonCount() == 0)
        {
            Debug.LogWarning("[InfectionSpawner] Cannot spawn a timed infection because no body-part button mappings are configured.", this);
            return;
        }

        if (!TryCreateWeightedInfection(out InfectionData infection, out bool isGeneric, out int namedRoll))
        {
            DifficultyStats stats = DifficultySettings.CurrentStats;
            Debug.LogWarning($"[InfectionSpawner] Cannot spawn a timed infection because the {(isGeneric ? "generic" : "named")} pathogen pool has no positive weights for {stats.difficultyName} difficulty.", this);
            return;
        }

        InfectionBodyPartButtonReference bodyPartButton = SelectWeightedBodyPartButton(infection);
        if (bodyPartButton == null)
        {
            Debug.LogWarning($"[InfectionSpawner] Cannot place {infection.displayName}; no valid body-part mapping was selected.", this);
            return;
        }

        bool wasBarrierMutated = ApplyBarrierMutation(infection);
        SpawnMarker(infection, bodyPartButton, wasBarrierMutated);
        LogInfection(infection, bodyPartButton);
        DifficultyStats difficultyStats = DifficultySettings.CurrentStats;
        Debug.Log($"[InfectionSpawner] Spawn roll {namedRoll}/100 with NamedChance={difficultyStats.namedInfectionChance:0.##}% => {(isGeneric ? "Generic" : "Named")}. Profile={infection.pathogenDatabaseName}; Type={infection.pathogenType}; Rarity={infection.rarity}; Severity={infection.severityStage}; Difficulty={difficultyStats.difficultyName}; TierWeights=Common:{difficultyStats.commonPathogenWeightPercent:0.##}%/Uncommon:{difficultyStats.uncommonPathogenWeightPercent:0.##}%/Rare:{difficultyStats.rarePathogenWeightPercent:0.##}%/VeryRare:{difficultyStats.veryRarePathogenWeightPercent:0.##}%; Day={currentDay}; Hour={currentHour:00}.", this);
    }

    private bool TryCreateWeightedInfection(out InfectionData infection, out bool isGeneric, out int namedRoll)
    {
        infection = null;
        isGeneric = true;
        namedRoll = 0;

        DifficultyStats difficultyStats = DifficultySettings.CurrentStats;
        if (difficultyStats == null)
            return false;

        namedRoll = UnityEngine.Random.Range(0, 100);
        float namedChance = Mathf.Clamp(difficultyStats.namedInfectionChance, 0f, 100f);
        isGeneric = namedRoll >= namedChance;
        InfectionData selectedProfile = SelectWeightedPathogenProfile(isGeneric);
        if (selectedProfile == null)
            return false;

        infection = CloneInfectionData(selectedProfile);
        infection.isGeneric = isGeneric;
        if (isGeneric)
            infection.displayName = GetGenericInfectionDisplayName(infection.pathogenType);
        return true;
    }

    private InfectionData SelectWeightedPathogenProfile(bool selectGenericPool)
    {
        List<InfectionData> validEntries = BuildValidInfectionEntries();
        int difficultyIndex = Mathf.Clamp(DifficultySettings.SelectedDifficultyIndex, 0, PathogenPoolDifficultyCount - 1);
        DifficultyStats difficultyStats = DifficultySettings.CurrentStats;
        if (difficultyStats == null)
            return null;

        InfectionRarity[] selectableRarities = selectGenericPool
            ? GenericPathogenRarities
            : NamedPathogenRarities;
        List<InfectionRarity> availableRarities = new List<InfectionRarity>();
        List<float> rarityWeights = new List<float>();
        float totalRarityWeight = 0f;
        foreach (InfectionRarity rarity in selectableRarities)
        {
            float rarityWeight = GetAvailableRarityWeight(validEntries, difficultyStats, rarity, difficultyIndex);
            if (rarityWeight <= 0f)
                continue;

            availableRarities.Add(rarity);
            rarityWeights.Add(rarityWeight);
            totalRarityWeight += rarityWeight;
        }

        if (totalRarityWeight <= 0f)
            return null;

        float rarityRoll = UnityEngine.Random.value * totalRarityWeight;
        InfectionRarity selectedRarity = availableRarities[availableRarities.Count - 1];
        for (int index = 0; index < availableRarities.Count; index++)
        {
            rarityRoll -= rarityWeights[index];
            if (rarityRoll < 0f)
            {
                selectedRarity = availableRarities[index];
                break;
            }
        }

        List<InfectionData> candidates = new List<InfectionData>();
        List<float> candidateWeights = new List<float>();
        float totalProfileWeight = 0f;
        foreach (InfectionData entry in validEntries)
        {
            if (entry.rarity != selectedRarity)
                continue;

            float profileWeight = GetPathogenSpawnWeight(entry, difficultyIndex);
            if (profileWeight <= 0f)
                continue;

            candidates.Add(entry);
            candidateWeights.Add(profileWeight);
            totalProfileWeight += profileWeight;
        }

        if (candidates.Count == 0 || totalProfileWeight <= 0f)
            return null;

        float profileRoll = UnityEngine.Random.value * totalProfileWeight;
        for (int index = 0; index < candidates.Count; index++)
        {
            profileRoll -= candidateWeights[index];
            if (profileRoll < 0f)
                return candidates[index];
        }

        return candidates[candidates.Count - 1];
    }

    private static float GetAvailableRarityWeight(List<InfectionData> validEntries, DifficultyStats difficultyStats, InfectionRarity rarity, int difficultyIndex)
    {
        float tierWeight = difficultyStats.GetPathogenTierWeightPercent(rarity);
        if (tierWeight <= 0f)
            return 0f;

        foreach (InfectionData entry in validEntries)
        {
            if (entry.rarity == rarity && GetPathogenSpawnWeight(entry, difficultyIndex) > 0f)
                return tierWeight;
        }

        return 0f;
    }

    private static float GetPathogenSpawnWeight(InfectionData infection, int difficultyIndex)
    {
        switch (difficultyIndex)
        {
            case 0:
                return Mathf.Max(0f, infection.easySpawnWeight);
            case 1:
                return Mathf.Max(0f, infection.normalSpawnWeight);
            case 2:
                return Mathf.Max(0f, infection.mediumSpawnWeight);
            default:
                return Mathf.Max(0f, infection.hardSpawnWeight);
        }
    }

    private static string GetGenericInfectionDisplayName(InfectionPathogenType pathogenType)
    {
        switch (pathogenType)
        {
            case InfectionPathogenType.Bacterial:
                return "Unidentified Bacteria";
            case InfectionPathogenType.Viral:
                return "Unidentified Virus";
            default:
                return "Unidentified Fungus";
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


    private InfectionMarker SpawnMarker(
        InfectionData infection,
        InfectionBodyPartButtonReference bodyPartButton,
        bool wasBarrierMutated = false,
        Vector3? worldPositionOverride = null)
    {
        int vesselSpawnPointIndex = -1;
        Vector3 infectionWorldPosition = worldPositionOverride.HasValue
            ? worldPositionOverride.Value
            : ResolveInitialInfectionWorldPosition(bodyPartButton, out vesselSpawnPointIndex);
        InfectionMarker marker = new InfectionMarker
        {
            infection = infection,
            bodyPartMapping = bodyPartButton,
            bodyPartName = GetBodyPartDisplayName(bodyPartButton.button),
            bodyPartGroup = bodyPartButton.bodyPartGroup,
            worldPosition = infectionWorldPosition,
            vesselSpawnPointIndex = vesselSpawnPointIndex,
            canvas = hudCanvas,
            unresolvedDays = Mathf.Max(0, infection.daysUntreated),
            wasBarrierMutated = wasBarrierMutated
        };

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

        return marker;
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
            float pathogenMaxHp = marker.infection.pathogenType switch
            {
                InfectionPathogenType.Viral => stats.virusHp,
                InfectionPathogenType.Fungal => stats.bacteriaHp * FungalHealthMultiplier,
                _ => stats.bacteriaHp
            };
            if (marker.wasBarrierMutated)
                pathogenMaxHp *= BreachedBarrierPathogenMultiplier;
            health.SetMaxHp(pathogenMaxHp);
            health.SetPathogenType(marker.infection.pathogenType);

            marker.threatVisuals.Add(visual);
        }
    }

    private void UpdateInfectionThreatVisuals()
    {
        foreach (InfectionMarker marker in activeInfectionMarkers)
        {
            if (marker == null || marker.isRemoving || !activeInfectionMarkers.Contains(marker))
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
        GameObject avatarObject = new GameObject($"WBC Dispatch - {marker.infection.displayName}", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage), typeof(Button), typeof(CanvasGroup));
        avatarObject.transform.SetParent(marker.canvas.transform, false);
        avatarObject.transform.SetAsLastSibling();

        RectTransform avatarRect = avatarObject.GetComponent<RectTransform>();
        avatarRect.anchorMin = new Vector2(0.5f, 0.5f);
        avatarRect.anchorMax = new Vector2(0.5f, 0.5f);
        avatarRect.pivot = new Vector2(0.5f, 0.5f);
        avatarRect.sizeDelta = dispatchAvatarSize;

        marker.canvasGroup = avatarObject.GetComponent<CanvasGroup>();
        marker.canvasGroup.alpha = 1f;
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

    private Color GetPathogenColor(InfectionPathogenType pathogenType)
    {
        switch (pathogenType)
        {
            case InfectionPathogenType.Viral:
                return viralColor;
            case InfectionPathogenType.Fungal:
                return fungalColor;
            default:
                return bacterialColor;
        }
    }

    private static string GetPathogenCategoryLabel(InfectionPathogenType pathogenType)
    {
        switch (pathogenType)
        {
            case InfectionPathogenType.Viral:
                return "Viral infection";
            case InfectionPathogenType.Fungal:
                return "Fungal infection";
            default:
                return "Bacterial infection";
        }
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
        popupRect.sizeDelta = popupSize;
        selectedInfectionPopupObject.transform.localScale = Vector3.one * popupScale;

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
            popupFontSize, FontStyles.Normal, Color.white, TextAlignmentOptions.Left);
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

    private void ShowAntigenPopup(string virusName, bool antigenAlreadyUnlocked)
    {
        if (adaptiveResponsePopup == null)
            CreateAdaptiveResponsePopup();

        if (adaptiveResponsePopup == null)
        {
            Debug.LogWarning("[InfectionSpawner] Cannot show the antigen collection banner because AdaptiveResponsePopup is unavailable.", this);
            return;
        }

        string resolvedName = string.IsNullOrWhiteSpace(virusName) ? "Virus" : virusName;
        string title = antigenAlreadyUnlocked ? AntigenAlreadyAcquiredTitle : AntigenCollectedTitle;
        string subtitle = antigenAlreadyUnlocked
            ? string.Format(AntigenAlreadyAcquiredSubtitleFormat, resolvedName)
            : string.Format(AntigenCollectedSubtitleFormat, resolvedName);
        MethodInfo enqueueMessageMethod = typeof(AdaptiveResponsePopup).GetMethod(
            AdaptiveResponsePopupEnqueueMessageMethodName, AdaptiveResponsePopupMessageMethodFlags);
        if (enqueueMessageMethod == null)
        {
            Debug.LogWarning("[InfectionSpawner] Cannot show the antigen collection banner because its message queue is unavailable.", this);
            return;
        }

        enqueueMessageMethod.Invoke(adaptiveResponsePopup, new object[] { title, subtitle });
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
                selectedInfectionCategoryLabel.text = GetPathogenCategoryLabel(selectedInfectionPopupMarker.infection.pathogenType);
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
                         popupCamera.transform.up * (popupMinVerticalOffset + popupClusterRadiusMultiplier * clusterRadius);
        Transform popupTransform = selectedInfectionPopupObject.transform;
        popupTransform.position = anchor + offset;
        popupTransform.rotation = popupCamera.transform.rotation;

        float scaledPopupScale = popupScale;
        if (!popupCamera.orthographic)
        {
            float distance = Vector3.Distance(popupCamera.transform.position, popupTransform.position);
            scaledPopupScale *= Mathf.Clamp(distance / InfectionPopupReferenceDistance, 0.25f, 20f);
        }
        scaledPopupScale = Mathf.Abs(scaledPopupScale);
        popupTransform.localScale = new Vector3(scaledPopupScale, scaledPopupScale, scaledPopupScale);
    }

    private void SelectMarker(InfectionMarker marker)
    {
        if (marker == null || marker.isRemoving || !activeInfectionMarkers.Contains(marker))
            return;

        if (selectedMarker != null && selectedMarker != marker && selectedMarker.dispatchButton != null)
            selectedMarker.dispatchButton.gameObject.SetActive(false);

        selectedMarker = marker;
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

    /// <summary>Dispatches a ready Neutrophil batch with the selected innate-cell variant identity.</summary>
    public bool DispatchNeutrophilVariantBatch(int batchId, string variantName, InfectionMarker marker, out string failureReason)
    {
        failureReason = null;
        if (!string.Equals(variantName, NeutrophilVariantName, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(variantName, EosinophilVariantName, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(variantName, BasophilVariantName, StringComparison.OrdinalIgnoreCase))
        {
            failureReason = "The selected Neutrophil variant is not supported.";
            return false;
        }

        if (lymphaticBatchSpawner == null)
            lymphaticBatchSpawner = FindFirstObjectByType<LymphaticBatchSpawner>();
        LymphaticBatchGroup batch = lymphaticBatchSpawner != null ? lymphaticBatchSpawner.GetBatch(batchId) : null;
        if (batch == null || batch.Status != LymphaticBatchGroup.BatchStatus.Ready ||
            !string.Equals(batch.UnitType, NeutrophilUnitName, StringComparison.OrdinalIgnoreCase))
        {
            failureReason = "The selected Neutrophil batch is no longer ready to deploy.";
            return false;
        }

        if (marker == null || marker.infection == null)
        {
            failureReason = "Select a live infection before deploying the Neutrophil batch.";
            return false;
        }

        if (!lymphaticBatchSpawner.DispatchBatch(batchId, marker, out failureReason))
            return false;

        string displayName = GetNeutrophilVariantDisplayName(variantName);
        foreach (GameObject member in batch.Members)
        {
            if (member == null)
                continue;

            ConfigureNeutrophilVariant(member, variantName);
            SelectableUnit selectable = member.GetComponent<SelectableUnit>();
            if (selectable == null)
                selectable = member.AddComponent<SelectableUnit>();
            selectable.Initialize(displayName);
        }

        Debug.Log($"[Lymphatic Dispatch] {displayName} batch {batchId} dispatched to {marker.infection.displayName} with base damage {GetNeutrophilVariantDamage(variantName):0.##}.", this);
        return true;
    }

    private static void ConfigureNeutrophilVariant(GameObject unit, string variantName)
    {
        if (unit == null)
            return;

        NeutrophilScript neutrophil = unit.GetComponent<NeutrophilScript>();
        if (neutrophil != null)
            neutrophil.damage = GetNeutrophilVariantDamage(variantName);

        if (string.Equals(variantName, NeutrophilVariantName, StringComparison.OrdinalIgnoreCase))
            return;

        Color tint = GetNeutrophilVariantTint(variantName);
        Renderer[] renderers = unit.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            foreach (Material material in renderer.materials)
            {
                if (material == null)
                    continue;

                if (material.HasProperty(BaseColorPropertyId))
                    material.SetColor(BaseColorPropertyId, tint);
                if (material.HasProperty(ColorPropertyId))
                    material.SetColor(ColorPropertyId, tint);
                SetTintIfSupported(material, "_1st_ShadeColor", ScaleTint(tint, 0.62f));
                SetTintIfSupported(material, "_2nd_ShadeColor", ScaleTint(tint, 0.36f));
                SetTintIfSupported(material, "_RimLightColor", tint);
            }
        }
    }

    private static void SetTintIfSupported(Material material, string propertyName, Color tint)
    {
        if (material.HasProperty(propertyName))
            material.SetColor(propertyName, tint);
    }

    private static Color ScaleTint(Color tint, float scale)
    {
        return new Color(tint.r * scale, tint.g * scale, tint.b * scale, tint.a);
    }

    private static float GetNeutrophilVariantDamage(string variantName)
    {
        if (string.Equals(variantName, EosinophilVariantName, StringComparison.OrdinalIgnoreCase))
            return EosinophilBaseDamage;
        if (string.Equals(variantName, BasophilVariantName, StringComparison.OrdinalIgnoreCase))
            return BasophilBaseDamage;
        return NeutrophilBaseDamage;
    }

    private static Color GetNeutrophilVariantTint(string variantName)
    {
        if (string.Equals(variantName, EosinophilVariantName, StringComparison.OrdinalIgnoreCase))
            return EosinophilTint;
        if (string.Equals(variantName, BasophilVariantName, StringComparison.OrdinalIgnoreCase))
            return BasophilTint;
        return NeutrophilBaseColor;
    }

    private static string GetNeutrophilVariantName(GameObject unit)
    {
        SelectableUnit selectable = unit != null ? unit.GetComponent<SelectableUnit>() : null;
        string displayName = selectable != null ? selectable.DisplayName : unit != null ? unit.name : string.Empty;
        if (displayName.IndexOf(EosinophilVariantName, StringComparison.OrdinalIgnoreCase) >= 0)
            return EosinophilVariantName;
        if (displayName.IndexOf(BasophilVariantName, StringComparison.OrdinalIgnoreCase) >= 0)
            return BasophilVariantName;
        return NeutrophilVariantName;
    }

    private static bool IsParasiteInfection(InfectionData infection)
    {
        return InfectionMatchesCategory(infection, ParasiteCategoryKeywords);
    }

    private static bool IsAllergenInfection(InfectionData infection)
    {
        return InfectionMatchesCategory(infection, AllergenCategoryKeywords);
    }

    private static bool InfectionMatchesCategory(InfectionData infection, string[] categoryKeywords)
    {
        if (infection == null || categoryKeywords == null)
            return false;

        string searchableInfection = string.Join(" ", infection.displayName, infection.pathogenDatabaseName, infection.entryCause);
        foreach (string keyword in categoryKeywords)
        {
            if (!string.IsNullOrWhiteSpace(keyword) && searchableInfection.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    private static string GetNeutrophilVariantDisplayName(string variantName)
    {
        if (string.Equals(variantName, EosinophilVariantName, StringComparison.OrdinalIgnoreCase))
            return EosinophilDisplayName;
        if (string.Equals(variantName, BasophilVariantName, StringComparison.OrdinalIgnoreCase))
            return BasophilDisplayName;
        return NeutrophilDisplayName;
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
        string infectionName = infection.isGeneric
            ? $"Generic {infection.pathogenType} ({infection.displayName})"
            : $"Named {infection.pathogenType} ({infection.displayName})";
        string message = $"[InfectionSpawner] {infectionName} at {GetBodyPartDisplayName(bodyPartButton)}; profile={infection.pathogenDatabaseName}; rarity={infection.rarity}; severity={infection.severityStage}; resolve=+{infection.resolveGain}; neglect=-{infection.neglectLoss}.";
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

    /// <summary>Returns named bacterial, fungal, and viral profiles used for lymphatic cell specializations.</summary>
    public static List<string> GetKnownTrainingPathogenNames()
    {
        List<string> pathogenNames = new List<string>();
        foreach (InfectionData infection in CreateDefaultInfectionEntries())
        {
            if (infection.isGeneric ||
                (infection.pathogenType != InfectionPathogenType.Bacterial
                    && infection.pathogenType != InfectionPathogenType.Fungal
                    && infection.pathogenType != InfectionPathogenType.Viral))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(infection.displayName) && !pathogenNames.Contains(infection.displayName))
                pathogenNames.Add(infection.displayName);
        }

        return pathogenNames;
    }

    /// <summary>Determines whether a name is one of the named viral profiles used for B/T-cell training.</summary>
    public static bool IsKnownViralTrainingPathogenName(string pathogenName)
    {
        if (string.IsNullOrWhiteSpace(pathogenName))
            return false;

        foreach (InfectionData infection in CreateDefaultInfectionEntries())
        {
            if (!infection.isGeneric && infection.pathogenType == InfectionPathogenType.Viral &&
                string.Equals(infection.displayName, pathogenName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static List<InfectionData> CreateDefaultInfectionEntries()
    {
        return new List<InfectionData>
        {
            CreatePathogenEntry("Staph", InfectionPathogenType.Bacterial, InfectionRarity.Common, InfectionSeverityStage.Mild, 2, 2, 50f, 40f, 30f, 20f, InfectionCorrectResponder.WBC),
            CreatePathogenEntry("Strep", InfectionPathogenType.Bacterial, InfectionRarity.Common, InfectionSeverityStage.Mild, 2, 2, 30f, 30f, 30f, 20f, InfectionCorrectResponder.WBC),
            CreatePathogenEntry("E. coli", InfectionPathogenType.Bacterial, InfectionRarity.Uncommon, InfectionSeverityStage.Moderate, 4, 4, 15f, 20f, 20f, 25f, InfectionCorrectResponder.WBC),
            CreatePathogenEntry("Salmonella", InfectionPathogenType.Bacterial, InfectionRarity.Uncommon, InfectionSeverityStage.Moderate, 4, 4, 5f, 10f, 20f, 20f, InfectionCorrectResponder.WBC),
            CreatePathogenEntry("Listeria", InfectionPathogenType.Bacterial, InfectionRarity.Rare, InfectionSeverityStage.Moderate, 4, 4, 0f, 0f, 10f, 15f, InfectionCorrectResponder.WBC),
            CreatePathogenEntry("Bordetella", InfectionPathogenType.Bacterial, InfectionRarity.Rare, InfectionSeverityStage.Moderate, 4, 4, 0f, 5f, 10f, 15f, InfectionCorrectResponder.WBC),
            CreatePathogenEntry("Pneumococcus", InfectionPathogenType.Bacterial, InfectionRarity.VeryRare, InfectionSeverityStage.Severe, 6, 6, 0f, 0f, 5f, 10f, InfectionCorrectResponder.WBC),
            CreatePathogenEntry("Clostridium", InfectionPathogenType.Bacterial, InfectionRarity.VeryRare, InfectionSeverityStage.Severe, 6, 6, 0f, 0f, 5f, 10f, InfectionCorrectResponder.WBC),

            CreatePathogenEntry("Common Cold", InfectionPathogenType.Viral, InfectionRarity.Common, InfectionSeverityStage.Mild, 2, 2, 50f, 40f, 30f, 20f, InfectionCorrectResponder.KillerT),
            CreatePathogenEntry("Stomach Flu", InfectionPathogenType.Viral, InfectionRarity.Common, InfectionSeverityStage.Mild, 2, 2, 30f, 30f, 30f, 20f, InfectionCorrectResponder.KillerT),
            CreatePathogenEntry("Hand, Foot, Mouth", InfectionPathogenType.Viral, InfectionRarity.Uncommon, InfectionSeverityStage.Mild, 2, 2, 20f, 15f, 15f, 10f, InfectionCorrectResponder.KillerT),
            CreatePathogenEntry("Influenza (Flu)", InfectionPathogenType.Viral, InfectionRarity.Uncommon, InfectionSeverityStage.Moderate, 4, 4, 0f, 15f, 15f, 20f, InfectionCorrectResponder.KillerT),
            CreatePathogenEntry("Chickenpox", InfectionPathogenType.Viral, InfectionRarity.Rare, InfectionSeverityStage.Moderate, 4, 4, 0f, 0f, 10f, 15f, InfectionCorrectResponder.KillerT),
            CreatePathogenEntry("Measles", InfectionPathogenType.Viral, InfectionRarity.Rare, InfectionSeverityStage.Severe, 6, 6, 0f, 0f, 5f, 10f, InfectionCorrectResponder.KillerT),
            CreatePathogenEntry("COVID-19", InfectionPathogenType.Viral, InfectionRarity.VeryRare, InfectionSeverityStage.Severe, 6, 6, 0f, 0f, 0f, 5f, InfectionCorrectResponder.KillerT),
            CreatePathogenEntry("Dengue", InfectionPathogenType.Viral, InfectionRarity.VeryRare, InfectionSeverityStage.Severe, 6, 6, 0f, 0f, 0f, 40f, InfectionCorrectResponder.KillerT),

            CreatePathogenEntry("Ringworm", InfectionPathogenType.Fungal, InfectionRarity.Common, InfectionSeverityStage.Mild, 2, 2, 50f, 30f, 20f, 15f, InfectionCorrectResponder.WBC),
            CreatePathogenEntry("Athlete's Foot", InfectionPathogenType.Fungal, InfectionRarity.Common, InfectionSeverityStage.Mild, 2, 2, 30f, 30f, 20f, 15f, InfectionCorrectResponder.WBC),
            CreatePathogenEntry("Candida", InfectionPathogenType.Fungal, InfectionRarity.Uncommon, InfectionSeverityStage.Moderate, 4, 4, 20f, 20f, 20f, 20f, InfectionCorrectResponder.WBC),
            CreatePathogenEntry("Aspergillus", InfectionPathogenType.Fungal, InfectionRarity.Uncommon, InfectionSeverityStage.Moderate, 4, 4, 0f, 10f, 20f, 20f, InfectionCorrectResponder.WBC),
            CreatePathogenEntry("Histoplasmosis", InfectionPathogenType.Fungal, InfectionRarity.Rare, InfectionSeverityStage.Moderate, 4, 4, 0f, 5f, 10f, 15f, InfectionCorrectResponder.WBC),
            CreatePathogenEntry("Sporotrichosis", InfectionPathogenType.Fungal, InfectionRarity.Rare, InfectionSeverityStage.Moderate, 4, 4, 0f, 5f, 10f, 15f, InfectionCorrectResponder.WBC),
            CreatePathogenEntry("Cryptococcus", InfectionPathogenType.Fungal, InfectionRarity.VeryRare, InfectionSeverityStage.Severe, 6, 6, 0f, 0f, 5f, 10f, InfectionCorrectResponder.WBC),
            CreatePathogenEntry("Mucormycosis", InfectionPathogenType.Fungal, InfectionRarity.VeryRare, InfectionSeverityStage.Severe, 6, 6, 0f, 0f, 5f, 40f, InfectionCorrectResponder.WBC)
        };
    }

    private static InfectionData CreatePathogenEntry(string pathogenName, InfectionPathogenType pathogenType,
        InfectionRarity rarity, InfectionSeverityStage severityStage, int resolveGain, int neglectLoss,
        float easyWeight, float normalWeight, float mediumWeight, float hardWeight,
        InfectionCorrectResponder correctResponder)
    {
        return new InfectionData
        {
            displayName = pathogenName,
            pathogenDatabaseName = pathogenName,
            pathogenType = pathogenType,
            rarity = rarity,
            severityStage = severityStage,
            resolveGain = resolveGain,
            neglectLoss = neglectLoss,
            easySpawnWeight = easyWeight,
            normalSpawnWeight = normalWeight,
            mediumSpawnWeight = mediumWeight,
            hardSpawnWeight = hardWeight,
            entryCause = "Unknown exposure",
            correctResponder = correctResponder,
            preferredBodyParts = new List<InfectionBodyPartGroup>
            {
                InfectionBodyPartGroup.Feet,
                InfectionBodyPartGroup.Hands,
                InfectionBodyPartGroup.Head
            },
            daysUntreated = 0
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
