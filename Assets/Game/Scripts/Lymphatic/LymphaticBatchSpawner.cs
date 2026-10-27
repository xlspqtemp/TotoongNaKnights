using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Reserves lymph nodes, validates routes, and creates stationary five-unit batches.</summary>
[DisallowMultipleComponent]
public sealed class LymphaticBatchSpawner : MonoBehaviour
{
    private const int MaximumBatchCount = 10;
    private const int UnitsPerBatch = 5;
    private const float DefaultNavMeshSampleRadius = 2f;
    private const float FormationSpacing = 1.3f;
    private const float MinimumFormationSeparation = 0.9f;
    private const float PathValidationTolerance = 0.05f;

    private static readonly Vector3[] FormationOffsets =
    {
        Vector3.zero,
        new Vector3(-FormationSpacing, 0f, 0f),
        new Vector3(FormationSpacing, 0f, 0f),
        new Vector3(0f, 0f, -FormationSpacing),
        new Vector3(0f, 0f, FormationSpacing)
    };

    [Header("Lymph Node and Duct References")]
    [SerializeField] private Transform lymphNodeRoot;
    [SerializeField] private Transform rightLymphaticDuct;
    [SerializeField] private Transform thoracicDuct;

    [Header("Circulatory Transfer References")]
    [SerializeField] private Transform leftSubclavianVein;
    [SerializeField] private Transform rightSubclavianVein;
    [SerializeField, Min(0.1f)] private float transferNavMeshSampleRadius = 25f;
    [SerializeField, Min(0.1f)] private float arrivalDistance = 2f;

    [Header("Unit Prefabs")]
    [SerializeField] private GameObject neutrophilPrefab;
    [SerializeField] private GameObject macrophagePrefab;
    [SerializeField] private GameObject tCellPrefab;
    [SerializeField] private GameObject bCellPrefab;

    [Header("Navigation Validation")]
    [SerializeField, Min(0.1f)] private float navMeshSampleRadius = DefaultNavMeshSampleRadius;

    private readonly Dictionary<int, BatchReservation> reservations = new Dictionary<int, BatchReservation>();
    private readonly List<NodeSlot> availableNodes = new List<NodeSlot>();
    private readonly List<int> releasedNodeIndices = new List<int>();
    private bool nodesInitialized;

    /// <summary>Gets the count of reserved or completed batches.</summary>
    public int CurrentBatchCount => reservations.Count;

    /// <summary>Gets the maximum number of batches that can occupy the lymph nodes.</summary>
    public int MaximumBatches => MaximumBatchCount;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        InitializeNodesAndValidateRoutes();
    }

    private void Update()
    {
        releasedNodeIndices.Clear();
        foreach (KeyValuePair<int, BatchReservation> entry in reservations)
        {
            GameObject[] members = entry.Value.Members;
            if (members == null)
                continue;

            bool hasLivingMember = false;
            for (int index = 0; index < members.Length; index++)
            {
                if (members[index] != null && members[index].activeInHierarchy)
                {
                    hasLivingMember = true;
                    break;
                }
            }

            if (!hasLivingMember)
                releasedNodeIndices.Add(entry.Key);
        }

        foreach (int nodeIndex in releasedNodeIndices)
            reservations.Remove(nodeIndex);

        UpdateBatchMovement();
    }

    /// <summary>Copies all completed batches into the supplied list, ordered by batch identifier.</summary>
    public void GetBatches(List<LymphaticBatchGroup> output)
    {
        if (output == null)
            return;

        output.Clear();
        foreach (BatchReservation reservation in reservations.Values)
        {
            if (reservation.Members == null || reservation.Members.Length == 0 || reservation.Members[0] == null)
                continue;

            LymphaticBatchGroup group = reservation.Members[0].GetComponent<LymphaticBatchGroup>();
            if (group != null)
                output.Add(group.GroupLeader);
        }

        output.Sort((first, second) => first.BatchId.CompareTo(second.BatchId));
    }

    /// <summary>Returns the completed batch occupying the requested one-based slot, if any.</summary>
    public LymphaticBatchGroup GetBatch(int batchId)
    {
        if (!reservations.TryGetValue(batchId - 1, out BatchReservation reservation) ||
            reservation.Members == null || reservation.Members.Length == 0 || reservation.Members[0] == null)
            return null;

        LymphaticBatchGroup group = reservation.Members[0].GetComponent<LymphaticBatchGroup>();
        return group != null ? group.GroupLeader : null;
    }

    /// <summary>Starts routing a ready batch to its closest lymphatic duct and then to an infection.</summary>
    public bool DispatchBatch(int batchId, InfectionSpawner.InfectionMarker infection)
    {
        LymphaticBatchGroup batch = GetBatch(batchId);
        if (batch == null || batch.Status != LymphaticBatchGroup.BatchStatus.Ready ||
            infection == null || infection.isRemoving || infection.infection == null)
            return false;

        Transform duct = null;
        Transform vein = null;
        if (!batch.HasEnteredCirculatorySystem)
        {
            Vector3 batchPosition = GetBatchPosition(batch);
            float rightDuctDistance = rightLymphaticDuct != null ? (rightLymphaticDuct.position - batchPosition).sqrMagnitude : float.PositiveInfinity;
            float thoracicDuctDistance = thoracicDuct != null ? (thoracicDuct.position - batchPosition).sqrMagnitude : float.PositiveInfinity;
            if (rightDuctDistance <= thoracicDuctDistance)
            {
                duct = rightLymphaticDuct;
                vein = leftSubclavianVein;
            }
            else
            {
                duct = thoracicDuct;
                vein = rightSubclavianVein;
            }

            if (duct == null || vein == null)
            {
                Debug.LogWarning("LymphaticBatchSpawner cannot dispatch: the nearest duct or its paired subclavian vein is missing.", this);
                return false;
            }
        }

        batch.BeginMobilization(infection, duct, vein);
        Vector3 firstDestination = batch.HasEnteredCirculatorySystem ? infection.worldPosition : duct.position;
        if (TrySetDestinations(batch, firstDestination, transferNavMeshSampleRadius))
        {
            if (ConsoleLogUI.Instance != null)
                ConsoleLogUI.Instance.LogBattleAction("Batch " + batch.BatchId + " mobilizing to " + infection.infection.displayName + ".", ConsoleLogUI.LogType.Success);
            return true;
        }

        StopBatchAgents(batch);
        batch.AbortOrder();
        if (ConsoleLogUI.Instance != null)
            ConsoleLogUI.Instance.LogBattleAction("Batch " + batch.BatchId + " could not reach the selected infection.", ConsoleLogUI.LogType.Warning);
        return false;
    }

    /// <summary>Stops a batch's movement and clears its infection assignment.</summary>
    public bool AbortBatch(int batchId)
    {
        LymphaticBatchGroup batch = GetBatch(batchId);
        if (batch == null || batch.Status == LymphaticBatchGroup.BatchStatus.Ready)
            return false;

        StopBatchAgents(batch);
        batch.AbortOrder();
        if (ConsoleLogUI.Instance != null)
            ConsoleLogUI.Instance.LogBattleAction("Batch " + batch.BatchId + " target aborted; batch is ready for reassignment.", ConsoleLogUI.LogType.Info);
        return true;
    }

    /// <summary>Destroys a completed batch and releases its lymph-node slot.</summary>
    public bool DestroyBatch(int batchId)
    {
        int nodeIndex = batchId - 1;
        if (!reservations.TryGetValue(nodeIndex, out BatchReservation reservation) || reservation.Members == null)
            return false;

        LymphaticBatchGroup batch = reservation.Members[0] != null
            ? reservation.Members[0].GetComponent<LymphaticBatchGroup>()
            : null;
        string destroyedBatchName = batch != null ? batch.DisplayName : "Unit batch";
        if (ConsoleLogUI.Instance != null)
            ConsoleLogUI.Instance.LogBattleAction("Batch " + batchId + " destroyed: " + destroyedBatchName + ".", ConsoleLogUI.LogType.Warning);

        foreach (GameObject member in reservation.Members)
        {
            if (member == null)
                continue;
            member.SetActive(false);
            Destroy(member);
        }

        reservations.Remove(nodeIndex);
        return true;
    }

    /// <summary>Reserves the first free, route-validated node for a batch that is beginning training.</summary>
    public bool TryReserveBatch(out int nodeIndex)
    {
        EnsureNodesInitialized();
        nodeIndex = -1;
        if (reservations.Count >= MaximumBatchCount)
            return false;

        foreach (NodeSlot node in availableNodes)
        {
            if (reservations.ContainsKey(node.Index))
                continue;

            reservations.Add(node.Index, new BatchReservation(node));
            nodeIndex = node.Index;
            return true;
        }

        return false;
    }

    /// <summary>Completes a reserved training slot by spawning five stationary units at its reserved node.</summary>
    public bool CompleteReservedBatch(int nodeIndex, string unitType, string pathogenVariant)
    {
        EnsureNodesInitialized();
        if (!reservations.TryGetValue(nodeIndex, out BatchReservation reservation) || reservation.Members != null)
            return false;

        GameObject prefab = GetPrefab(unitType);
        if (prefab == null || reservation.Node.SpawnPositions.Length != UnitsPerBatch)
        {
            Debug.LogError($"Lymphatic batch {nodeIndex + 1} could not be completed: its prefab or validated node positions are unavailable.", this);
            reservations.Remove(nodeIndex);
            return false;
        }

        GameObject[] members = new GameObject[UnitsPerBatch];
        string displayName = string.IsNullOrWhiteSpace(pathogenVariant) ? unitType : unitType + " - " + pathogenVariant;
        for (int index = 0; index < UnitsPerBatch; index++)
        {
            GameObject unit = Instantiate(prefab, reservation.Node.SpawnPositions[index], Quaternion.identity);
            members[index] = unit;
            if (unit == null || !ConfigureStationaryUnit(unit, displayName, reservation.Node.SpawnPositions[index]))
            {
                DestroySpawnedMembers(members);
                reservations.Remove(nodeIndex);
                Debug.LogError($"Lymphatic batch {nodeIndex + 1} failed to place all five {displayName} units on the NavMesh.", this);
                return false;
            }

            unit.name = string.Format("{0} (Node {1} - {2}/5)", displayName, nodeIndex + 1, index + 1);
        }

        LymphaticBatchGroup leader = members[0].GetComponent<LymphaticBatchGroup>();
        if (leader == null)
            leader = members[0].AddComponent<LymphaticBatchGroup>();
        leader.Initialize(leader, members, unitType, pathogenVariant, nodeIndex + 1);

        for (int index = 1; index < members.Length; index++)
        {
            LymphaticBatchGroup groupMember = members[index].GetComponent<LymphaticBatchGroup>();
            if (groupMember == null)
                groupMember = members[index].AddComponent<LymphaticBatchGroup>();
            groupMember.Initialize(leader, members, unitType, pathogenVariant, nodeIndex + 1);
        }

        reservation.Members = members;
        if (ConsoleLogUI.Instance != null)
            ConsoleLogUI.Instance.LogBattleAction("Batch " + (nodeIndex + 1) + " trained: " + displayName + ".", ConsoleLogUI.LogType.Success);
        Debug.Log($"Lymphatic batch {nodeIndex + 1}/{MaximumBatchCount} deployed: {displayName} (5 units) at '{reservation.Node.Transform.name}'.", this);
        return true;
    }

    /// <summary>Returns a validated node's current world-space position, or false if it cannot be used.</summary>
    public bool TryGetNodePosition(int nodeIndex, out Vector3 worldPosition)
    {
        EnsureNodesInitialized();
        foreach (NodeSlot node in availableNodes)
        {
            if (node.Index != nodeIndex)
                continue;

            worldPosition = node.SpawnPositions[0];
            return true;
        }

        worldPosition = Vector3.zero;
        return false;
    }

    private void ResolveReferences()
    {
        if (lymphNodeRoot == null)
            lymphNodeRoot = transform.Find("Lymph Nodes (10 max)");
        if (rightLymphaticDuct == null)
            rightLymphaticDuct = transform.Find("Upper-Left Side/Right Lymphatic Duct");
        if (thoracicDuct == null)
            thoracicDuct = transform.Find("Lower-Right Side/Thoracic Duct");
        if (leftSubclavianVein == null)
            leftSubclavianVein = GameObject.Find("Destination -  Left Subclavian Vein")?.transform
                ?? GameObject.Find("Destination - Left Subclavian Vein")?.transform;
        if (rightSubclavianVein == null)
            rightSubclavianVein = GameObject.Find("Destination -  Right Subclavian Vein")?.transform
                ?? GameObject.Find("Destination - Right Subclavian Vein")?.transform;
    }

    private void EnsureNodesInitialized()
    {
        if (!nodesInitialized)
            InitializeNodesAndValidateRoutes();
    }

    private void InitializeNodesAndValidateRoutes()
    {
        if (nodesInitialized)
            return;

        nodesInitialized = true;
        ResolveReferences();
        if (lymphNodeRoot == null || rightLymphaticDuct == null || thoracicDuct == null)
        {
            Debug.LogError("LymphaticBatchSpawner requires the Lymph Nodes (10 max) root and both lymphatic duct transforms.", this);
            return;
        }

        Transform[] nodeTransforms = lymphNodeRoot.GetComponentsInChildren<Transform>(true);
        List<Transform> orderedNodes = new List<Transform>();
        foreach (Transform node in nodeTransforms)
        {
            if (node != lymphNodeRoot && node.name == "Lymph Node" && node.gameObject.activeInHierarchy)
                orderedNodes.Add(node);
        }

        GameObject[] prefabs = { neutrophilPrefab, macrophagePrefab, tCellPrefab, bCellPrefab };
        for (int prefabIndex = 0; prefabIndex < prefabs.Length; prefabIndex++)
        {
            if (prefabs[prefabIndex] == null || prefabs[prefabIndex].GetComponent<NavMeshAgent>() == null)
            {
                Debug.LogError($"LymphaticBatchSpawner requires all four unit prefabs to have a root NavMeshAgent. Missing or invalid prefab index {prefabIndex}.", this);
                return;
            }
        }

        GameObject[] navigationProbes = new GameObject[prefabs.Length];
        NavMeshAgent[] probeAgents = new NavMeshAgent[prefabs.Length];
        Vector3 probeSpawnPosition = orderedNodes.Count > 0 ? orderedNodes[0].position : transform.position;
        for (int prefabIndex = 0; prefabIndex < prefabs.Length; prefabIndex++)
        {
            navigationProbes[prefabIndex] = Instantiate(prefabs[prefabIndex], probeSpawnPosition, Quaternion.identity);
            if (navigationProbes[prefabIndex] == null)
            {
                DestroyNavigationProbes(navigationProbes);
                Debug.LogError($"LymphaticBatchSpawner could not create a navigation probe from '{prefabs[prefabIndex].name}'.", this);
                return;
            }

            DisableAutonomousMovement(navigationProbes[prefabIndex]);
            probeAgents[prefabIndex] = navigationProbes[prefabIndex].GetComponent<NavMeshAgent>();
            if (probeAgents[prefabIndex] == null || !probeAgents[prefabIndex].enabled)
            {
                DestroyNavigationProbes(navigationProbes);
                Debug.LogError($"LymphaticBatchSpawner could not use the NavMeshAgent on '{prefabs[prefabIndex].name}'.", this);
                return;
            }
        }

        for (int nodeIndex = 0; nodeIndex < orderedNodes.Count && nodeIndex < MaximumBatchCount; nodeIndex++)
        {
            if (TryValidateNode(orderedNodes[nodeIndex], nodeIndex, prefabs, probeAgents, out NodeSlot nodeSlot, out string failure))
            {
                availableNodes.Add(nodeSlot);
            }
            else
            {
                Debug.LogWarning($"[Lymphatic NavMesh Test] Node {nodeIndex + 1} is unavailable: {failure}", orderedNodes[nodeIndex]);
            }
        }

        DestroyNavigationProbes(navigationProbes);
        Debug.Log($"[Lymphatic NavMesh Test] {availableNodes.Count}/{orderedNodes.Count} nodes validated; every position in each five-unit formation has complete routes for all four prefab agents to both ducts.", this);
    }

    private bool TryValidateNode(Transform nodeTransform, int nodeIndex, GameObject[] prefabs, NavMeshAgent[] probeAgents,
        out NodeSlot nodeSlot, out string failure)
    {
        nodeSlot = null;
        failure = string.Empty;
        Vector3[] spawnPositions = new Vector3[UnitsPerBatch];
        for (int spawnIndex = 0; spawnIndex < UnitsPerBatch; spawnIndex++)
        {
            Vector3 requestedPosition = nodeTransform.position + FormationOffsets[spawnIndex];
            Vector3 validatedPosition = Vector3.zero;
            bool hasValidatedPosition = false;

            for (int prefabIndex = 0; prefabIndex < prefabs.Length; prefabIndex++)
            {
                GameObject prefab = prefabs[prefabIndex];
                NavMeshAgent agent = probeAgents[prefabIndex];
                NavMeshQueryFilter filter = CreateFilter(agent);
                if (!NavMesh.SamplePosition(requestedPosition, out NavMeshHit sampleHit, navMeshSampleRadius, filter))
                {
                    failure = $"formation position {spawnIndex + 1} cannot be sampled for '{prefab.name}'.";
                    return false;
                }

                if (hasValidatedPosition && (sampleHit.position - validatedPosition).sqrMagnitude > 0.04f)
                {
                    failure = $"formation position {spawnIndex + 1} samples to different NavMesh surfaces for '{prefab.name}'.";
                    return false;
                }

                if (!hasValidatedPosition)
                {
                    validatedPosition = sampleHit.position;
                    hasValidatedPosition = true;
                }

                if (!agent.Warp(sampleHit.position))
                {
                    failure = $"'{prefab.name}' could not be warped to formation position {spawnIndex + 1}.";
                    return false;
                }

                agent.ResetPath();
                if (!CanReachDuct(agent, rightLymphaticDuct) || !CanReachDuct(agent, thoracicDuct))
                {
                    failure = $"position {spawnIndex + 1} cannot reach both ducts with '{prefab.name}'.";
                    return false;
                }
            }

            if (!hasValidatedPosition || (spawnIndex > 0 && IsTooCloseToExistingPosition(validatedPosition, spawnPositions, spawnIndex)))
            {
                failure = $"formation position {spawnIndex + 1} overlaps another unit position.";
                return false;
            }

            spawnPositions[spawnIndex] = validatedPosition;
        }

        nodeSlot = new NodeSlot(nodeIndex, nodeTransform, spawnPositions);
        return true;
    }

    private static void DestroyNavigationProbes(GameObject[] navigationProbes)
    {
        for (int index = 0; index < navigationProbes.Length; index++)
        {
            if (navigationProbes[index] != null)
                Destroy(navigationProbes[index]);
        }
    }

    private bool CanReachDuct(NavMeshAgent agent, Transform duct)
    {
        if (agent == null || duct == null || !agent.enabled)
            return false;

        NavMeshQueryFilter filter = CreateFilter(agent);
        if (!NavMesh.SamplePosition(duct.position, out NavMeshHit ductHit, navMeshSampleRadius * 5f, filter))
            return false;

        NavMeshPath path = new NavMeshPath();
        return agent.CalculatePath(ductHit.position, path) && path.status == NavMeshPathStatus.PathComplete;
    }

    private static NavMeshQueryFilter CreateFilter(NavMeshAgent agent)
    {
        return new NavMeshQueryFilter
        {
            agentTypeID = agent.agentTypeID,
            areaMask = agent.areaMask
        };
    }

    private static bool IsTooCloseToExistingPosition(Vector3 candidate, Vector3[] positions, int count)
    {
        float minimumDistanceSquared = MinimumFormationSeparation * MinimumFormationSeparation;
        for (int index = 0; index < count; index++)
        {
            if ((candidate - positions[index]).sqrMagnitude < minimumDistanceSquared)
                return true;
        }

        return false;
    }

    private bool ConfigureStationaryUnit(GameObject unit, string displayName, Vector3 position)
    {
        DisableAutonomousMovement(unit);
        NavMeshAgent agent = unit.GetComponent<NavMeshAgent>();
        if (agent == null || !agent.enabled || !agent.Warp(position))
            return false;

        agent.ResetPath();
        agent.isStopped = true;

        SelectableUnit selectableUnit = unit.GetComponent<SelectableUnit>();
        if (selectableUnit == null)
            selectableUnit = unit.AddComponent<SelectableUnit>();
        selectableUnit.Initialize(displayName);
        return true;
    }

    private static void DisableAutonomousMovement(GameObject unit)
    {
        NeutrophilScript neutrophil = unit.GetComponent<NeutrophilScript>();
        if (neutrophil != null)
            neutrophil.enabled = false;

        MacrophageScript macrophage = unit.GetComponent<MacrophageScript>();
        if (macrophage != null)
            macrophage.enabled = false;

        BacteriaCellResponder responder = unit.GetComponent<BacteriaCellResponder>();
        if (responder != null)
            responder.enabled = false;

        LymphaticCellRoute lymphaticRoute = unit.GetComponent<LymphaticCellRoute>();
        if (lymphaticRoute != null)
            lymphaticRoute.enabled = false;

        CirculatoryCellRoute circulatoryRoute = unit.GetComponent<CirculatoryCellRoute>();
        if (circulatoryRoute != null)
            circulatoryRoute.enabled = false;
    }

    /// <summary>Estimates remaining travel time in gameplay seconds, including the duct-to-infection leg when not yet transferred.</summary>
    public float EstimateTravelTime(LymphaticBatchGroup batch)
    {
        if (batch == null || batch.Status != LymphaticBatchGroup.BatchStatus.Mobilizing)
            return 0f;

        float longestArrivalTime = 0f;
        foreach (GameObject member in batch.Members)
        {
            if (member == null || !member.activeInHierarchy)
                continue;

            NavMeshAgent agent = member.GetComponent<NavMeshAgent>();
            if (agent == null || !agent.enabled || agent.speed <= 0f)
                continue;

            Vector3 activeWaypoint = !batch.HasEnteredCirculatorySystem && batch.SelectedDuct != null
                ? batch.SelectedDuct.position
                : batch.TargetInfection != null ? batch.TargetInfection.worldPosition : member.transform.position;
            float remainingDistance = agent.pathPending || !agent.hasPath
                ? Vector3.Distance(agent.transform.position, activeWaypoint)
                : agent.remainingDistance;
            float estimatedTime = remainingDistance / agent.speed;

            if (!batch.HasEnteredCirculatorySystem && batch.SelectedVein != null && batch.TargetInfection != null)
                estimatedTime += Vector3.Distance(batch.SelectedVein.position, batch.TargetInfection.worldPosition) / agent.speed;

            longestArrivalTime = Mathf.Max(longestArrivalTime, estimatedTime);
        }

        return longestArrivalTime;
    }

    private void UpdateBatchMovement()
    {
        foreach (BatchReservation reservation in reservations.Values)
        {
            if (reservation.Members == null || reservation.Members.Length == 0 || reservation.Members[0] == null)
                continue;

            LymphaticBatchGroup batch = reservation.Members[0].GetComponent<LymphaticBatchGroup>();
            if (batch == null || batch.Status != LymphaticBatchGroup.BatchStatus.Mobilizing)
                continue;

            InfectionSpawner.InfectionMarker infection = batch.TargetInfection;
            if (infection == null || infection.isRemoving || infection.infection == null)
            {
                StopBatchAgents(batch);
                batch.AbortOrder();
                continue;
            }

            if (!batch.HasEnteredCirculatorySystem)
            {
                Transform duct = batch.SelectedDuct;
                if (duct != null && HasReachedDestination(batch))
                {
                    if (TryTransferToVein(batch, batch.SelectedVein))
                    {
                        batch.MarkEnteredCirculatorySystem();
                        if (!TrySetDestinations(batch, infection.worldPosition, transferNavMeshSampleRadius))
                            Debug.LogWarning($"Lymphatic batch {batch.BatchId} transferred, but its infection target could not be sampled for navigation.", this);
                    }
                }
                continue;
            }

            if (HasReachedDestination(batch))
            {
                StopBatchAgents(batch);
                batch.MarkWorking();
            }
            else if (AllAgentsStoppedWithoutPath(batch))
            {
                TrySetDestinations(batch, infection.worldPosition, transferNavMeshSampleRadius);
            }
        }
    }

    private bool TryTransferToVein(LymphaticBatchGroup batch, Transform vein)
    {
        if (vein == null)
            return false;

        List<NavMeshAgent> agents = GetBatchAgents(batch);
        if (agents.Count == 0)
            return false;

        for (int index = 0; index < agents.Count; index++)
        {
            NavMeshAgent agent = agents[index];
            NavMeshQueryFilter filter = CreateFilter(agent);
            Vector3 requestedPosition = vein.position + FormationOffsets[index % FormationOffsets.Length];
            if (!NavMesh.SamplePosition(requestedPosition, out NavMeshHit hit, transferNavMeshSampleRadius, filter) &&
                !NavMesh.SamplePosition(vein.position, out hit, transferNavMeshSampleRadius * 2f, filter))
            {
                Debug.LogWarning($"Lymphatic batch {batch.BatchId} could not find a circulatory NavMesh point at '{vein.name}'.", this);
                return false;
            }

            agent.ResetPath();
            agent.isStopped = true;
            if (!agent.Warp(hit.position))
            {
                Debug.LogWarning($"Lymphatic batch {batch.BatchId} could not transfer a member to '{vein.name}'.", this);
                return false;
            }
        }

        return true;
    }

    private bool TrySetDestinations(LymphaticBatchGroup batch, Vector3 requestedDestination, float sampleRadius)
    {
        List<NavMeshAgent> agents = GetBatchAgents(batch);
        if (agents.Count == 0)
            return false;

        Vector3[] destinations = new Vector3[agents.Count];
        for (int index = 0; index < agents.Count; index++)
        {
            NavMeshAgent agent = agents[index];
            NavMeshQueryFilter filter = CreateFilter(agent);
            Vector3 formationPosition = requestedDestination + FormationOffsets[index % FormationOffsets.Length];
            if (!NavMesh.SamplePosition(formationPosition, out NavMeshHit hit, sampleRadius, filter) &&
                !NavMesh.SamplePosition(requestedDestination, out hit, sampleRadius, filter))
                return false;
            destinations[index] = hit.position;
        }

        for (int index = 0; index < agents.Count; index++)
        {
            NavMeshAgent agent = agents[index];
            agent.isStopped = false;
            if (agent.SetDestination(destinations[index]))
                continue;

            StopBatchAgents(batch);
            return false;
        }

        return true;
    }

    private bool HasReachedDestination(LymphaticBatchGroup batch)
    {
        List<NavMeshAgent> agents = GetBatchAgents(batch);
        if (agents.Count == 0)
            return false;

        foreach (NavMeshAgent agent in agents)
        {
            if (agent.pathPending)
                return false;

            float threshold = Mathf.Max(arrivalDistance, agent.stoppingDistance + 0.25f);
            float distanceToSampledDestination = agent.hasPath
                ? agent.remainingDistance
                : Vector3.Distance(agent.transform.position, agent.destination);
            if (distanceToSampledDestination > threshold)
                return false;
        }

        return true;
    }

    private bool AllAgentsStoppedWithoutPath(LymphaticBatchGroup batch)
    {
        foreach (NavMeshAgent agent in GetBatchAgents(batch))
        {
            if (agent.pathPending || agent.hasPath || !agent.isStopped)
                return false;
        }

        return true;
    }

    private static List<NavMeshAgent> GetBatchAgents(LymphaticBatchGroup batch)
    {
        List<NavMeshAgent> agents = new List<NavMeshAgent>();
        if (batch == null)
            return agents;

        foreach (GameObject member in batch.Members)
        {
            if (member == null || !member.activeInHierarchy)
                continue;
            NavMeshAgent agent = member.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled && agent.isOnNavMesh)
                agents.Add(agent);
        }

        return agents;
    }

    private void StopBatchAgents(LymphaticBatchGroup batch)
    {
        foreach (NavMeshAgent agent in GetBatchAgents(batch))
        {
            agent.ResetPath();
            agent.isStopped = true;
        }
    }

    private static Vector3 GetBatchPosition(LymphaticBatchGroup batch)
    {
        Vector3 positionSum = Vector3.zero;
        int liveCount = 0;
        foreach (GameObject member in batch.Members)
        {
            if (member == null || !member.activeInHierarchy)
                continue;
            positionSum += member.transform.position;
            liveCount++;
        }

        return liveCount > 0 ? positionSum / liveCount : batch.transform.position;
    }

    private static Vector3 GetCurrentDestination(LymphaticBatchGroup batch)
    {
        if (batch.HasEnteredCirculatorySystem && batch.TargetInfection != null)
            return batch.TargetInfection.worldPosition;
        return batch.SelectedDuct != null ? batch.SelectedDuct.position : batch.transform.position;
    }

    private GameObject GetPrefab(string unitType)
    {
        switch (unitType)
        {
            case "Neutrophil": return neutrophilPrefab;
            case "Macrophage": return macrophagePrefab;
            case "T Cell": return tCellPrefab;
            case "B Cell": return bCellPrefab;
            default: return null;
        }
    }

    private void DestroySpawnedMembers(GameObject[] members)
    {
        for (int index = 0; index < members.Length; index++)
        {
            if (members[index] != null)
                Destroy(members[index]);
        }
    }

    private sealed class NodeSlot
    {
        public readonly int Index;
        public readonly Transform Transform;
        public readonly Vector3[] SpawnPositions;

        public NodeSlot(int index, Transform transform, Vector3[] spawnPositions)
        {
            Index = index;
            Transform = transform;
            SpawnPositions = spawnPositions;
        }
    }

    private sealed class BatchReservation
    {
        public readonly NodeSlot Node;
        public GameObject[] Members;

        public BatchReservation(NodeSlot node)
        {
            Node = node;
        }
    }
}
