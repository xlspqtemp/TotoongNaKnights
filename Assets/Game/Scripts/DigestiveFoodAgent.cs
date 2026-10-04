using UnityEngine;

/// <summary>
/// Moves a digestive item through every configured pathway waypoint and removes it at Anus.
/// </summary>
public sealed class DigestiveFoodAgent : MonoBehaviour
{
    private const float WaypointArrivalDistance = 0.1f;

    private Transform[] pathwayWaypoints;
    private int currentWaypointIndex;
    private float movementSpeed;
    private bool isConfigured;
    private bool hasReachedIntestines;
    private string wellnessEventKey;

    /// <summary>Returns the wellness event occurrence associated with this contaminated item.</summary>
    public string WellnessEventKey => wellnessEventKey;

    /// <summary>Configures the item to move through the full digestive pathway.</summary>
    /// <param name="waypoints">Ordered pathway transforms, ending at Anus.</param>
    /// <param name="transitSpeed">Movement speed in Unity units per gameplay second.</param>
    /// <param name="eventKey">The wellness event occurrence carried by this item, if any.</param>
    public void Initialize(Transform[] waypoints, float transitSpeed, string eventKey)
    {
        if (waypoints == null || waypoints.Length == 0)
        {
            Debug.LogError($"Digestive item '{name}' cannot start because its pathway has no waypoints.", this);
            return;
        }

        pathwayWaypoints = waypoints;
        movementSpeed = Mathf.Max(0.01f, transitSpeed);
        currentWaypointIndex = 0;
        wellnessEventKey = eventKey;
        hasReachedIntestines = false;
        isConfigured = true;
    }

    private void Update()
    {
        if (!isConfigured)
            return;

        if (currentWaypointIndex >= pathwayWaypoints.Length)
        {
            CompletePathway();
            return;
        }

        Transform targetWaypoint = pathwayWaypoints[currentWaypointIndex];
        if (targetWaypoint == null)
        {
            Debug.LogError($"Digestive item '{name}' stopped at waypoint {currentWaypointIndex + 1}/{pathwayWaypoints.Length}; it did not complete the entire pathway.", this);
            isConfigured = false;
            Destroy(gameObject);
            return;
        }

        float gameplayDeltaTime = GameplaySpeed.DeltaTime;
        if (gameplayDeltaTime <= 0f)
            return;

        transform.position = Vector3.MoveTowards(
            transform.position,
            targetWaypoint.position,
            movementSpeed * gameplayDeltaTime);

        if (Vector3.Distance(transform.position, targetWaypoint.position) <= WaypointArrivalDistance)
        {
            if (currentWaypointIndex == 1 && !hasReachedIntestines && !string.IsNullOrWhiteSpace(wellnessEventKey))
            {
                hasReachedIntestines = true;
                WellnessManager.Instance?.MarkEventReachedTarget(wellnessEventKey);
            }

            currentWaypointIndex++;
            if (currentWaypointIndex >= pathwayWaypoints.Length)
                CompletePathway();
        }
    }

    private void CompletePathway()
    {
        isConfigured = false;
        Debug.Log($"Digestive item '{name}' completed all {pathwayWaypoints.Length} pathway waypoints and reached Anus.", this);
        Destroy(gameObject);
    }
}
