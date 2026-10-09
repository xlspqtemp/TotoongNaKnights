using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Displays floating infection icons and forwards picker clicks through InfectionSpawner's public API.</summary>
[DefaultExecutionOrder(-100)]
public sealed class InfectionNotificationManager : MonoBehaviour
{
    private const string NotificationPrefabResourcePath = "InfectionNotification";
    private const string BacteriaSpriteResourcePath = "UI/Red bacteria icon";
    private const string VirusSpriteResourcePath = "UI/Blue virus icon";
    private const string CategoryCanvasObjectName = "NotificationCategoryCanvas";
    private const string CategoryLabelObjectName = "CategoryLabel";
    private const int MaximumNotifications = 5;
    private const int GeneratedSpritePixelsPerUnit = 1024;
    private const float CategoryContainerFontSize = 30f;

    [SerializeField] private InfectionSpawner infectionSpawner;
    [SerializeField] private CameraScript cameraScript;
    [SerializeField] private Camera gameplayCamera;
    [SerializeField] private GameObject notificationPrefab;
    [SerializeField, HideInInspector] private Texture2D bacteriaSprite;
    [SerializeField, HideInInspector] private Texture2D virusSprite;
    [Header("Pathogen Icon Textures")]
    [SerializeField] private Texture2D bacterialIconTexture;
    [SerializeField] private Texture2D viralIconTexture;
    [SerializeField, Min(0.01f)] private float scaleMultiplier = 1f;
    [SerializeField, Min(0f)] private float bobAmplitude = 0.18f;
    [SerializeField, Min(0f)] private float bobSpeed = 1.6f;
    [SerializeField, Min(0f)] private float fadeDuration = 0.3f;
    [SerializeField, Min(0.01f)] private float iconScale = 0.65f;
    [SerializeField, Min(0f)] private float foregroundOffset = 1.5f;
    [SerializeField] private Color containerBackgroundColor = new Color(0.012f, 0.028f, 0.038f, 0.96f);
    [SerializeField] private Color bacterialTextColor = new Color(1f, 0.2f, 0.23f, 1f);
    [SerializeField] private Color viralTextColor = new Color(0.25f, 0.66f, 1f, 1f);
    [SerializeField] private Vector2 containerPadding = new Vector2(9f, 4f);

    private sealed class NotificationState
    {
        public InfectionSpawner.InfectionMarker marker;
        public GameObject gameObject;
        public SpriteRenderer spriteRenderer;
        public BoxCollider boxCollider;
        public Canvas categoryCanvas;
        public Image categoryBackground;
        public TextMeshProUGUI categoryLabel;
        public Color categoryTextColor;
        public float alpha;
        public float bobTime;
        public bool fadingOut;
        public bool desired;
    }

    private readonly Dictionary<InfectionSpawner.InfectionMarker, NotificationState> notifications =
        new Dictionary<InfectionSpawner.InfectionMarker, NotificationState>();
    private readonly List<InfectionSpawner.InfectionMarker> removalBuffer =
        new List<InfectionSpawner.InfectionMarker>();
    private Sprite bacteriaNotificationIcon;
    private Sprite virusNotificationIcon;
    private Texture2D cachedBacteriaTexture;
    private Texture2D cachedVirusTexture;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureManagerExists()
    {
        if (Object.FindFirstObjectByType<InfectionNotificationManager>() != null ||
            Object.FindFirstObjectByType<InfectionSpawner>() == null)
            return;

        GameObject managerObject = new GameObject("Infection Notification Manager");
        managerObject.AddComponent<InfectionNotificationManager>();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        ClearNotifications();
        infectionSpawner = null;
        cameraScript = null;
        gameplayCamera = null;
        ResolveReferences();
        SyncNotifications();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        ClearNotifications();
        infectionSpawner = null;
        cameraScript = null;
        gameplayCamera = null;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ClearNotifications();
        infectionSpawner = null;
        cameraScript = null;
        gameplayCamera = null;
        ResolveReferences();
        SyncNotifications();
    }

    private void Update()
    {
        ResolveReferences();
        if (infectionSpawner == null)
            return;

        SyncNotifications();
        if (IsCirculatoryLayerActive() && infectionSpawner.IsAwaitingInfectionTargetSelection && GameplaySpeed.DeltaTime > 0f)
            HandlePickerClick();
    }

    private void SyncNotifications()
    {
        if (infectionSpawner == null)
            return;

        bool circulatoryActive = IsCirculatoryLayerActive();
        IReadOnlyList<InfectionSpawner.InfectionMarker> activeInfections = infectionSpawner.ActiveInfections;
        foreach (NotificationState state in notifications.Values)
            state.desired = false;

        int visibleInfectionCount = 0;
        for (int index = 0; index < activeInfections.Count && visibleInfectionCount < MaximumNotifications; index++)
        {
            InfectionSpawner.InfectionMarker marker = activeInfections[index];
            if (marker == null || marker.isRemoving || marker.infection == null)
                continue;

            visibleInfectionCount++;
            if (notifications.TryGetValue(marker, out NotificationState existing))
            {
                existing.desired = true;
                existing.fadingOut = false;
            }
            else
            {
                NotificationState created = CreateNotification(marker);
                if (created != null)
                {
                    created.desired = true;
                    notifications.Add(marker, created);
                }
            }
        }

        removalBuffer.Clear();
        foreach (KeyValuePair<InfectionSpawner.InfectionMarker, NotificationState> entry in notifications)
        {
            NotificationState state = entry.Value;
            if (!state.desired)
                state.fadingOut = true;

            UpdateNotification(state, circulatoryActive, GameplaySpeed.DeltaTime);
            if (state.fadingOut && state.alpha <= 0f)
            {
                if (state.gameObject != null)
                    Destroy(state.gameObject);
                removalBuffer.Add(entry.Key);
            }
        }

        foreach (InfectionSpawner.InfectionMarker marker in removalBuffer)
            notifications.Remove(marker);
    }

    private void ClearNotifications()
    {
        foreach (NotificationState state in notifications.Values)
        {
            if (state.gameObject != null)
                Destroy(state.gameObject);
        }

        notifications.Clear();
        removalBuffer.Clear();
    }

    private void ResolveReferences()
    {
        if (infectionSpawner == null)
            infectionSpawner = Object.FindFirstObjectByType<InfectionSpawner>();
        if (cameraScript == null)
            cameraScript = Object.FindFirstObjectByType<CameraScript>();
        if (gameplayCamera == null && cameraScript != null)
            gameplayCamera = cameraScript.GetComponentInChildren<Camera>();
        if (gameplayCamera == null)
            gameplayCamera = Camera.main;
        if (notificationPrefab == null)
            notificationPrefab = Resources.Load<GameObject>(NotificationPrefabResourcePath);
        if (bacterialIconTexture == null)
            bacterialIconTexture = Resources.Load<Texture2D>(BacteriaSpriteResourcePath);
        if (bacterialIconTexture == null)
            bacterialIconTexture = bacteriaSprite;
        if (viralIconTexture == null)
            viralIconTexture = Resources.Load<Texture2D>(VirusSpriteResourcePath);
        if (viralIconTexture == null)
            viralIconTexture = virusSprite;

        if (cachedBacteriaTexture != bacterialIconTexture)
        {
            if (bacteriaNotificationIcon != null)
                Destroy(bacteriaNotificationIcon);
            cachedBacteriaTexture = bacterialIconTexture;
            bacteriaNotificationIcon = CreateNotificationSprite(bacterialIconTexture, "Bacterial Infection Notification");
        }

        if (cachedVirusTexture != viralIconTexture)
        {
            if (virusNotificationIcon != null)
                Destroy(virusNotificationIcon);
            cachedVirusTexture = viralIconTexture;
            virusNotificationIcon = CreateNotificationSprite(viralIconTexture, "Viral Infection Notification");
        }
    }

    private static Sprite CreateNotificationSprite(Texture2D sourceTexture, string spriteName)
    {
        if (sourceTexture == null)
            return null;

        Sprite sprite = Sprite.Create(sourceTexture,
            new Rect(0f, 0f, sourceTexture.width, sourceTexture.height),
            new Vector2(0.5f, 0.5f), GeneratedSpritePixelsPerUnit);
        sprite.name = spriteName;
        return sprite;
    }

    private bool IsCirculatoryLayerActive()
    {
        if (cameraScript == null || cameraScript.floor1 == null || cameraScript.floor2 == null ||
            cameraScript.floor3 == null || cameraScript.floor4 == null)
            return false;

        Vector3 cameraPosition = cameraScript.transform.position;
        float nearestDistanceSquared = float.PositiveInfinity;
        Transform nearestFloor = null;
        Transform[] floors = { cameraScript.floor1, cameraScript.floor2, cameraScript.floor3, cameraScript.floor4 };
        foreach (Transform floor in floors)
        {
            float distanceSquared = (cameraPosition - floor.position).sqrMagnitude;
            if (distanceSquared >= nearestDistanceSquared)
                continue;

            nearestDistanceSquared = distanceSquared;
            nearestFloor = floor;
        }

        return nearestFloor == cameraScript.floor2;
    }

    private NotificationState CreateNotification(InfectionSpawner.InfectionMarker marker)
    {
        if (notificationPrefab == null)
        {
            Debug.LogWarning("[InfectionNotificationManager] InfectionNotification prefab is missing from Resources.", this);
            return null;
        }

        GameObject notificationObject = Instantiate(notificationPrefab);
        notificationObject.name = $"Infection Notification - {marker.infection.displayName}";
        SpriteRenderer spriteRenderer = notificationObject.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = notificationObject.AddComponent<SpriteRenderer>();
        AssignNotificationSprite(spriteRenderer, marker);

        BoxCollider boxCollider = notificationObject.GetComponent<BoxCollider>();
        if (boxCollider == null)
            boxCollider = notificationObject.AddComponent<BoxCollider>();
        boxCollider.isTrigger = false;
        boxCollider.size = new Vector3(0.9f, 0.9f, 0.3f);
        boxCollider.enabled = false;

        Transform categoryCanvasTransform = notificationObject.transform.Find(CategoryCanvasObjectName);
        Canvas categoryCanvas = categoryCanvasTransform != null ? categoryCanvasTransform.GetComponent<Canvas>() : null;
        Image categoryBackground = categoryCanvasTransform != null ? categoryCanvasTransform.GetComponent<Image>() : null;
        Transform categoryLabelTransform = categoryCanvasTransform != null
            ? categoryCanvasTransform.Find(CategoryLabelObjectName)
            : null;
        TextMeshProUGUI categoryLabel = categoryLabelTransform != null
            ? categoryLabelTransform.GetComponent<TextMeshProUGUI>()
            : null;
        Color categoryTextColor = ConfigureCategoryContainer(marker, categoryBackground, categoryLabel);

        return new NotificationState
        {
            marker = marker,
            gameObject = notificationObject,
            spriteRenderer = spriteRenderer,
            boxCollider = boxCollider,
            categoryCanvas = categoryCanvas,
            categoryBackground = categoryBackground,
            categoryLabel = categoryLabel,
            categoryTextColor = categoryTextColor,
            alpha = 0f
        };
    }

    private Color ConfigureCategoryContainer(InfectionSpawner.InfectionMarker marker, Image background,
        TextMeshProUGUI label)
    {
        if (marker == null || marker.infection == null || label == null || background == null)
            return Color.white;

        bool isViral = marker.infection.pathogenType == InfectionPathogenType.Viral;
        label.text = isViral ? "VIRAL" : "BACTERIAL";
        label.fontSize = CategoryContainerFontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        background.color = containerBackgroundColor;
        background.raycastTarget = false;

        RectTransform labelRect = label.rectTransform;
        labelRect.offsetMin = new Vector2(containerPadding.x, containerPadding.y);
        labelRect.offsetMax = new Vector2(-containerPadding.x, -containerPadding.y);
        label.ForceMeshUpdate();

        RectTransform containerRect = background.rectTransform;
        containerRect.sizeDelta = new Vector2(
            label.preferredWidth + containerPadding.x * 2f,
            label.preferredHeight + containerPadding.y * 2f);

        return isViral ? viralTextColor : bacterialTextColor;
    }

    private void AssignNotificationSprite(SpriteRenderer spriteRenderer, InfectionSpawner.InfectionMarker marker)
    {
        if (spriteRenderer == null || marker == null || marker.infection == null)
            return;

        spriteRenderer.sprite = marker.infection.pathogenType == InfectionPathogenType.Viral
            ? virusNotificationIcon
            : bacteriaNotificationIcon;
        spriteRenderer.color = Color.white;
    }

    private void UpdateNotification(NotificationState state, bool circulatoryActive, float gameplayDeltaTime)
    {
        if (state.gameObject == null || state.spriteRenderer == null || state.boxCollider == null)
            return;

        if (state.fadingOut)
            state.alpha = fadeDuration > 0f ? Mathf.MoveTowards(state.alpha, 0f, gameplayDeltaTime / fadeDuration) : 0f;
        else
            state.alpha = fadeDuration > 0f ? Mathf.MoveTowards(state.alpha, 1f, gameplayDeltaTime / fadeDuration) : 1f;

        bool pickerActive = infectionSpawner != null && infectionSpawner.IsAwaitingInfectionTargetSelection;
        state.spriteRenderer.enabled = circulatoryActive;
        state.boxCollider.enabled = circulatoryActive && pickerActive && !state.fadingOut;
        Color displayColor = Color.white;
        displayColor.a = state.alpha;
        state.spriteRenderer.color = displayColor;

        if (state.categoryCanvas != null)
        {
            state.categoryCanvas.worldCamera = gameplayCamera;
            state.categoryCanvas.overrideSorting = true;
            state.categoryCanvas.sortingOrder = state.spriteRenderer.sortingOrder + 1;
            state.categoryCanvas.enabled = circulatoryActive;
        }
        if (state.categoryBackground != null)
        {
            state.categoryBackground.enabled = circulatoryActive;
            Color backgroundColor = containerBackgroundColor;
            backgroundColor.a *= state.alpha;
            state.categoryBackground.color = backgroundColor;
        }
        if (state.categoryLabel != null)
        {
            state.categoryLabel.enabled = circulatoryActive;
            Color textColor = state.categoryTextColor;
            textColor.a *= state.alpha;
            state.categoryLabel.color = textColor;
        }

        if (!circulatoryActive || state.fadingOut)
            return;

        state.bobTime += gameplayDeltaTime * bobSpeed;
        Vector3 anchor = GetInfectionAnchor(state.marker);
        Transform cameraTransform = gameplayCamera != null ? gameplayCamera.transform : null;
        if (cameraTransform != null)
        {
            Vector3 towardCamera = (cameraTransform.position - anchor).normalized;
            float scale = GetReadableScale(cameraTransform, anchor) * scaleMultiplier;
            Vector3 sideOffset = cameraTransform.right * (scale * 0.45f);
            Vector3 bobOffset = cameraTransform.up * (Mathf.Sin(state.bobTime) * bobAmplitude);
            state.gameObject.transform.position = anchor + towardCamera * foregroundOffset + sideOffset + bobOffset;
            state.gameObject.transform.rotation = cameraTransform.rotation * Quaternion.Euler(0f, 0f, 45f);
            if (state.categoryCanvas != null)
                state.categoryCanvas.transform.rotation = cameraTransform.rotation;
            state.gameObject.transform.localScale = Vector3.one * scale;
        }
        else
        {
            state.gameObject.transform.position = anchor + Vector3.up * Mathf.Sin(state.bobTime) * bobAmplitude;
        }
    }

    private Vector3 GetInfectionAnchor(InfectionSpawner.InfectionMarker marker)
    {
        if (marker == null)
            return Vector3.zero;

        Vector3 pathogenCenter = Vector3.zero;
        int pathogenCount = 0;
        if (marker.threatVisuals != null)
        {
            foreach (GameObject pathogen in marker.threatVisuals)
            {
                if (pathogen == null || !pathogen.activeInHierarchy)
                    continue;

                Health health = pathogen.GetComponent<Health>();
                if (health != null && health.IsDead)
                    continue;

                pathogenCenter += pathogen.transform.position;
                pathogenCount++;
            }
        }

        return pathogenCount > 0 ? pathogenCenter / pathogenCount : marker.worldPosition;
    }

    private float GetReadableScale(Transform cameraTransform, Vector3 anchor)
    {
        if (gameplayCamera == null)
            return iconScale;

        if (gameplayCamera.orthographic)
            return Mathf.Max(0.01f, gameplayCamera.orthographicSize * iconScale);

        float distance = Vector3.Distance(cameraTransform.position, anchor);
        return Mathf.Max(0.01f, distance * iconScale * 0.35f);
    }

    private void HandlePickerClick()
    {
        if (gameplayCamera == null || !Input.GetMouseButtonDown(0))
            return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        Ray ray = gameplayCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        NotificationState nearestNotification = null;
        float nearestNotificationDistance = float.PositiveInfinity;
        foreach (RaycastHit hit in hits)
        {
            foreach (NotificationState state in notifications.Values)
            {
                if (!state.desired || state.fadingOut || state.boxCollider == null ||
                    !state.boxCollider.enabled || hit.collider != state.boxCollider)
                    continue;

                if (hit.distance < nearestNotificationDistance)
                {
                    nearestNotification = state;
                    nearestNotificationDistance = hit.distance;
                }
            }
        }

        if (nearestNotification != null && infectionSpawner != null &&
            infectionSpawner.IsAwaitingInfectionTargetSelection)
            infectionSpawner.RequestDispatchToInfection(nearestNotification.marker);
    }
}
