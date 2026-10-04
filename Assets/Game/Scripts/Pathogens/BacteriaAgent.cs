using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Registers a stationary bacteria target and tracks uninterrupted immune-cell contact.</summary>
public sealed class BacteriaAgent : MonoBehaviour
{
    private const float RequiredContactSeconds = 5f;

    private static readonly List<BacteriaAgent> ActiveBacteria = new List<BacteriaAgent>();

    private readonly Dictionary<BacteriaCellResponder, float> contactDurations = new Dictionary<BacteriaCellResponder, float>();
    private NavMeshAgent navMeshAgent;
    private bool isCleared;
    private string wellnessEventKey;

    /// <summary>Returns the number of currently registered bacteria.</summary>
    public static int ActiveCount => ActiveBacteria.Count;

    private void Awake()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
    }

    private void OnEnable()
    {
        if (!ActiveBacteria.Contains(this))
        {
            ActiveBacteria.Add(this);
        }
    }

    private void OnDisable()
    {
        ActiveBacteria.Remove(this);
        contactDurations.Clear();
    }

    /// <summary>Places the bacteria on its NavMesh and prevents autonomous movement.</summary>
    /// <param name="agent">The NavMeshAgent on this bacteria.</param>
    /// <param name="spawnPosition">The NavMesh position at which the bacteria is created.</param>
    /// <param name="eventKey">The wellness event occurrence associated with the wound, if any.</param>
    /// <returns>True when the bacteria is correctly stationary on the NavMesh.</returns>
    public bool InitializeStationary(NavMeshAgent agent, Vector3 spawnPosition, string eventKey = null)
    {
        navMeshAgent = agent;
        wellnessEventKey = eventKey;
        if (navMeshAgent == null || !navMeshAgent.enabled)
        {
            return false;
        }

        if (!navMeshAgent.isOnNavMesh && !navMeshAgent.Warp(spawnPosition))
        {
            return false;
        }

        navMeshAgent.ResetPath();
        navMeshAgent.isStopped = true;
        return true;
    }

    /// <summary>Returns whether this bacteria is active and has not been cleared.</summary>
    public bool IsAvailable => this != null && isActiveAndEnabled && !isCleared;

    /// <summary>Returns one currently available target by registry index.</summary>
    /// <param name="index">Index into the active bacteria registry.</param>
    /// <returns>The active bacteria at that index, or null when out of range or unavailable.</returns>
    public static BacteriaAgent GetActiveAt(int index)
    {
        if (index < 0 || index >= ActiveBacteria.Count)
        {
            return null;
        }

        BacteriaAgent bacteria = ActiveBacteria[index];
        return bacteria != null && bacteria.IsAvailable ? bacteria : null;
    }

    /// <summary>Adds one frame of uninterrupted contact for an immune cell and destroys the bacteria after five seconds.</summary>
    /// <param name="responder">The immune cell in contact with the bacteria.</param>
    /// <param name="elapsedSeconds">Unscaled duration of this contact frame.</param>
    public void RegisterContact(BacteriaCellResponder responder, float elapsedSeconds)
    {
        if (!IsAvailable || responder == null)
        {
            return;
        }

        contactDurations.TryGetValue(responder, out float elapsedContactSeconds);
        elapsedContactSeconds += Mathf.Max(0f, elapsedSeconds);
        contactDurations[responder] = elapsedContactSeconds;
        if (elapsedContactSeconds >= RequiredContactSeconds)
        {
            isCleared = true;
            if (!string.IsNullOrWhiteSpace(wellnessEventKey))
                WellnessManager.Instance?.TryAwardEventPoints(wellnessEventKey, "BacteriaCleared", 3f);
            Destroy(gameObject);
        }
    }

    /// <summary>Resets the contact timer when an immune cell leaves the bacteria.</summary>
    /// <param name="responder">The immune cell that left.</param>
    public void StopContact(BacteriaCellResponder responder)
    {
        if (responder != null)
        {
            contactDurations.Remove(responder);
        }
    }
}
