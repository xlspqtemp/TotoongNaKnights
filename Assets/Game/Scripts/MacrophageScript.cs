using UnityEngine;
using UnityEngine.AI;

public class MacrophageScript : MonoBehaviour
{
    public NavMeshAgent agent;
    public Transform target;

    public float radius = 5f;
    public float damage = 1f;
    public int health = 300;
    private bool isCirculatoryCell;

    /*private void OnTriggerEnter(Collider other)
    {
        // Make sure your Lava Block GameObject has the Tag "Lava"
        if (other.CompareTag("Lava"))
        {
            temp = 10f;
        }
    }*/

    private void Start()
    {
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
        }
        */
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