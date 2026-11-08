using System;
using UnityEngine;

/// <summary>Stores Killer T-cell attack stats and applies those attacks to its assigned viral threat visuals.</summary>
[DisallowMultipleComponent]
public sealed class KillerTCellCombatStats : MonoBehaviour
{
    private const string TCellUnitName = "T Cell";
    private const float DefaultAttackDamage = 20f;
    private const float DefaultAttackIntervalSeconds = 1f;
    private const float DefaultAttackRange = 2.5f;

    [SerializeField, Min(0f)] private float attackDamage = DefaultAttackDamage;
    [SerializeField, Min(0.1f)] private float attackIntervalSeconds = DefaultAttackIntervalSeconds;
    [SerializeField, Min(0f)] private float attackRange = DefaultAttackRange;

    private float attackCooldownRemaining;

    /// <summary>Gets this Killer T-cell's configured base attack damage.</summary>
    public float AttackDamage => attackDamage;

    private void Update()
    {
        attackCooldownRemaining = Mathf.Max(0f, attackCooldownRemaining - GameplaySpeed.DeltaTime);
        if (attackCooldownRemaining > 0f)
            return;

        LymphaticBatchGroup batch = GetComponent<LymphaticBatchGroup>();
        if (batch == null || batch.Status != LymphaticBatchGroup.BatchStatus.Working ||
            !string.Equals(batch.UnitType, TCellUnitName, StringComparison.OrdinalIgnoreCase))
            return;

        InfectionSpawner.InfectionMarker infection = batch.TargetInfection;
        if (infection == null || infection.isRemoving || infection.infection == null ||
            infection.infection.pathogenType != InfectionPathogenType.Viral ||
            !string.Equals(batch.PathogenVariant, infection.infection.displayName, StringComparison.OrdinalIgnoreCase))
            return;

        Health targetHealth = FindNearestLivingViralThreat(infection, out GameObject target);
        if (targetHealth == null || target == null ||
            Vector3.Distance(transform.position, target.transform.position) > attackRange)
            return;

        attackCooldownRemaining = Mathf.Max(DefaultAttackIntervalSeconds, attackIntervalSeconds);
        targetHealth.TakeDamage(attackDamage);
        Debug.Log($"Killer T-cell {name} hit {target.name} for {attackDamage:0.##} damage; remaining HP={targetHealth.CurrentHp:0.##}.", this);
    }

    private Health FindNearestLivingViralThreat(InfectionSpawner.InfectionMarker infection, out GameObject target)
    {
        target = null;
        Health nearestHealth = null;
        float nearestDistanceSquared = attackRange * attackRange;

        foreach (GameObject threat in infection.threatVisuals)
        {
            if (threat == null || !threat.activeInHierarchy)
                continue;

            Health health = threat.GetComponent<Health>();
            if (health == null || health.IsDead)
                continue;

            float distanceSquared = (threat.transform.position - transform.position).sqrMagnitude;
            if (distanceSquared > nearestDistanceSquared)
                continue;

            nearestDistanceSquared = distanceSquared;
            nearestHealth = health;
            target = threat;
        }

        return nearestHealth;
    }
}
