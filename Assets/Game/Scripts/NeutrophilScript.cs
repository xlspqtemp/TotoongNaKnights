using UnityEngine;
using UnityEngine.AI;

public class NeutrophilScript : MonoBehaviour
{
    public NavMeshAgent agent;
    public Transform target;

    public float radius = 5f;
    public float damage = 1f;
    public int health = 180;
    private bool isCirculatoryCell;

    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        isCirculatoryCell = GetComponent<CirculatoryCellRoute>() != null;

        if (isCirculatoryCell)
        {
            return;
        }
/*
        InvokeRepeating(nameof(AttackBacteria), 1f, 1f);
        Destroy(gameObject, health);
        */
    }

    private void Update()
    {
        if (isCirculatoryCell)
        {
            return;
        }
/*
        if (target == null)
        {
            Track();
        }

        if (target != null)
        {
            agent.SetDestination(target.position);
        }

        else
        {
            agent.ResetPath();
        }*/
    }
/*
    void Track()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Bacteria");
        float closestDist = Mathf.Infinity;
        Transform mainTarget = null;

        foreach (GameObject enemy in enemies)
        {
            float distance = Vector3.Distance(transform.position, enemy.transform.position);

            if (distance < closestDist)
            {
                closestDist = distance;
                mainTarget = enemy.transform;
            }
        }
        target = mainTarget;
    }
    void AttackBacteria()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, radius);

        foreach (var hit in hits)
        {
            BacteriaScript_A bacteria = hit.GetComponentInParent<BacteriaScript_A>();

            if (bacteria != null && bacteria.bacteriaHP > 0)
            {
                bacteria.TakeDamage(damage);
            }
        }
    }
    */
}