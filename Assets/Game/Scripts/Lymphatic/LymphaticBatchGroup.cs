using System.Collections.Generic;
using UnityEngine;

/// <summary>Stores shared membership, specialization, and command state for a five-unit lymphatic batch.</summary>
[DisallowMultipleComponent]
public sealed class LymphaticBatchGroup : MonoBehaviour
{
    public enum BatchStatus
    {
        Ready,
        Mobilizing,
        Working
    }

    [SerializeField] private LymphaticBatchGroup groupLeader;
    [SerializeField] private GameObject[] batchMembers = new GameObject[0];
    [SerializeField] private string unitType;
    [SerializeField] private string pathogenVariant;
    [SerializeField] private int lymphNodeIndex;
    [SerializeField] private BatchStatus status;
    [SerializeField] private InfectionSpawner.InfectionMarker targetInfection;
    [SerializeField] private Transform selectedDuct;
    [SerializeField] private Transform selectedVein;
    [SerializeField] private bool hasEnteredCirculatorySystem;

    /// <summary>Gets the first unit in this batch.</summary>
    public LymphaticBatchGroup GroupLeader => groupLeader != null ? groupLeader : this;

    /// <summary>Gets every unit belonging to this batch.</summary>
    public IReadOnlyList<GameObject> Members => GroupLeader.batchMembers;

    /// <summary>Gets the trained cell type.</summary>
    public string UnitType => GroupLeader.unitType;

    /// <summary>Gets the selected pathogen specialization, if any.</summary>
    public string PathogenVariant => GroupLeader.pathogenVariant;

    /// <summary>Gets the one-based batch identifier, matching its lymph-node slot.</summary>
    public int BatchId => GroupLeader.lymphNodeIndex;

    /// <summary>Gets the display name shown in the batch console.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(PathogenVariant) ? UnitType : UnitType + " - " + PathogenVariant;

    /// <summary>Gets the current command status.</summary>
    public BatchStatus Status => GroupLeader.status;

    /// <summary>Gets the infection currently assigned to this batch.</summary>
    public InfectionSpawner.InfectionMarker TargetInfection => GroupLeader.targetInfection;

    /// <summary>Gets whether this batch has transferred from the lymphatic system to the circulatory system.</summary>
    public bool HasEnteredCirculatorySystem => GroupLeader.hasEnteredCirculatorySystem;

    internal Transform SelectedDuct => GroupLeader.selectedDuct;
    internal Transform SelectedVein => GroupLeader.selectedVein;

    /// <summary>Initializes this member's link to the shared batch leader and metadata.</summary>
    public void Initialize(LymphaticBatchGroup leader, GameObject[] members, string trainedUnitType, string selectedVariant, int nodeIndex)
    {
        groupLeader = leader != null ? leader : this;
        batchMembers = members;
        unitType = trainedUnitType;
        pathogenVariant = selectedVariant ?? string.Empty;
        lymphNodeIndex = nodeIndex;
        if (groupLeader == this)
        {
            status = BatchStatus.Ready;
            targetInfection = null;
            selectedDuct = null;
            selectedVein = null;
            hasEnteredCirculatorySystem = false;
        }
    }

    /// <summary>Assigns a pending infection and begins movement toward the selected duct or infection.</summary>
    public void BeginMobilization(InfectionSpawner.InfectionMarker infection, Transform duct, Transform vein)
    {
        LymphaticBatchGroup leader = GroupLeader;
        leader.targetInfection = infection;
        leader.selectedDuct = duct;
        leader.selectedVein = vein;
        leader.status = BatchStatus.Mobilizing;
    }

    /// <summary>Marks the batch as having arrived at its assigned infection.</summary>
    public void MarkWorking()
    {
        GroupLeader.status = BatchStatus.Working;
    }

    /// <summary>Marks the batch as ready and clears its current infection order.</summary>
    public void AbortOrder()
    {
        LymphaticBatchGroup leader = GroupLeader;
        leader.targetInfection = null;
        leader.selectedDuct = null;
        leader.selectedVein = null;
        leader.status = BatchStatus.Ready;
    }

    /// <summary>Marks the batch as having completed its transfer to the circulatory system.</summary>
    public void MarkEnteredCirculatorySystem()
    {
        GroupLeader.hasEnteredCirculatorySystem = true;
    }
}
