using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class Health : MonoBehaviour
{
    [SerializeField, Min(0f)] private float currentHp = 100f;
    [SerializeField, Min(0f)] private float maxHp = 100f;
    [SerializeField] private bool showWorldHealthBar = true;
    [SerializeField, Min(0f)] private float deathShrinkDuration = 0.3f;
    [SerializeField] private Vector3 healthBarLocalOffset = new Vector3(0f, 1.2f, 0f);
    [SerializeField] private Vector2 healthBarSize = new Vector2(0.8f, 0.08f);

    private Canvas healthBarCanvas;
    private Image healthBarFill;
    private float attackCooldownRemaining;
    private bool removalStarted;
    private Vector3 initialScale;

    public float CurrentHp => currentHp;
    public float MaxHp => maxHp;
    public bool IsDead => currentHp <= 0f || removalStarted;
    public bool IsPathogen => GetComponentInChildren<BacteriaAgent>(true) != null;
    public InfectionPathogenType PathogenType { get; private set; } = InfectionPathogenType.Bacterial;

    public void SetPathogenType(InfectionPathogenType pathogenType)
    {
        PathogenType = pathogenType;
    }

    private void Awake()
    {
        DifficultyStats stats = DifficultySettings.CurrentStats;
        maxHp = GetComponentInChildren<BacteriaAgent>(true) != null ? stats.bacteriaHp : stats.wbcHp;
        maxHp = Mathf.Max(0f, maxHp);
        currentHp = maxHp;
        initialScale = transform.localScale;
    }

    private void Start()
    {
        if (showWorldHealthBar && !IsDead)
            CreateHealthBar();
    }

    private void Update()
    {
        attackCooldownRemaining = Mathf.Max(0f, attackCooldownRemaining - GameplaySpeed.DeltaTime);
    }

    private void LateUpdate()
    {
        if (healthBarCanvas == null)
            return;

        Camera camera = Camera.main;
        if (camera != null)
            healthBarCanvas.transform.rotation = Quaternion.LookRotation(camera.transform.position - healthBarCanvas.transform.position);
    }

    public void SetMaxHp(float value)
    {
        maxHp = Mathf.Max(0f, value);
        currentHp = maxHp;
        removalStarted = false;
        initialScale = transform.localScale;
        RefreshHealthBar();
    }

    public bool TryStartAttack(float attackIntervalSeconds)
    {
        if (IsDead || attackCooldownRemaining > 0f)
            return false;

        attackCooldownRemaining = Mathf.Max(0f, attackIntervalSeconds);
        return true;
    }

    public void TakeDamage(float damage)
    {
        if (IsDead || damage <= 0f)
            return;

        currentHp = Mathf.Max(0f, currentHp - damage);
        RefreshHealthBar();
        if (currentHp <= 0f)
            BeginRemoval();
    }

    public void FadeOutAndRemove()
    {
        if (!removalStarted)
            BeginRemoval();
    }

    private void BeginRemoval()
    {
        if (removalStarted)
            return;

        removalStarted = true;
        currentHp = 0f;
        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent != null && agent.enabled)
        {
            if (agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.isStopped = true;
            }
            agent.enabled = false;
        }

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        foreach (Collider item in colliders)
        {
            if (item != null)
                item.enabled = false;
        }

        if (healthBarFill != null)
            healthBarFill.fillAmount = 0f;
        StartCoroutine(ShrinkAndRemove());
    }

    private IEnumerator ShrinkAndRemove()
    {
        float duration = Mathf.Max(0f, deathShrinkDuration);
        if (duration <= 0f)
        {
            gameObject.SetActive(false);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += GameplaySpeed.DeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            transform.localScale = Vector3.Lerp(initialScale, Vector3.zero, progress);
            yield return null;
        }

        transform.localScale = Vector3.zero;
        gameObject.SetActive(false);
    }

    private void CreateHealthBar()
    {
        GameObject canvasObject = new GameObject("Unit Health Bar", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        canvasObject.transform.localPosition = healthBarLocalOffset;
        canvasObject.transform.localRotation = Quaternion.identity;
        canvasObject.transform.localScale = Vector3.one * 0.01f;

        healthBarCanvas = canvasObject.GetComponent<Canvas>();
        healthBarCanvas.renderMode = RenderMode.WorldSpace;
        healthBarCanvas.worldCamera = Camera.main;
        healthBarCanvas.sortingOrder = 20;
        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = healthBarSize * 100f;

        GameObject backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        backgroundObject.transform.SetParent(canvasObject.transform, false);
        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        Image background = backgroundObject.GetComponent<Image>();
        background.color = new Color(0.04f, 0.05f, 0.06f, 0.9f);
        background.raycastTarget = false;

        GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fillObject.transform.SetParent(backgroundObject.transform, false);
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(2f, 1f);
        fillRect.offsetMax = new Vector2(-2f, -1f);
        healthBarFill = fillObject.GetComponent<Image>();
        healthBarFill.color = new Color(0.2f, 0.9f, 0.35f, 1f);
        healthBarFill.type = Image.Type.Filled;
        healthBarFill.fillMethod = Image.FillMethod.Horizontal;
        healthBarFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        healthBarFill.raycastTarget = false;
        RefreshHealthBar();
    }

    private void RefreshHealthBar()
    {
        if (healthBarFill != null)
            healthBarFill.fillAmount = maxHp > 0f ? Mathf.Clamp01(currentHp / maxHp) : 0f;
    }

    private void OnDestroy()
    {
        if (healthBarCanvas != null)
            Destroy(healthBarCanvas.gameObject);
    }
}
