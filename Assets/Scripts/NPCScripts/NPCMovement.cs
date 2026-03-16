using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public class NPCMovement : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NavMeshAgent agent;

    [Header("Settings")]
    [SerializeField] private float destinationReachedThreshold = 0.35f;
    [SerializeField] private bool rotateToVelocity = true;
    [SerializeField] private float rotateSpeed = 10f;

    [Header("Runtime")]
    [SerializeField] private bool hasDestination;
    [SerializeField] private Vector3 currentDestination;

    public NavMeshAgent Agent => agent;
    public bool HasDestination => hasDestination;
    public Vector3 CurrentDestination => currentDestination;
    public float CurrentSpeed => agent != null ? agent.velocity.magnitude : 0f;

    public bool IsMoving
    {
        get
        {
            if (agent == null || !agent.enabled)
                return false;

            return !agent.isStopped && agent.velocity.sqrMagnitude > 0.01f;
        }
    }

    private void Awake()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();
    }

    private void Update()
    {
        if (agent == null || !agent.enabled)
            return;

        if (rotateToVelocity)
            HandleRotation();

        if (hasDestination && HasReachedDestination())
            hasDestination = false;
    }

    public void MoveTo(Vector3 worldPoint)
    {
        if (agent == null || !agent.enabled)
            return;

        currentDestination = worldPoint;
        hasDestination = true;
        agent.isStopped = false;
        agent.SetDestination(worldPoint);
    }

    public void Stop()
    {
        if (agent == null || !agent.enabled)
            return;

        hasDestination = false;
        agent.isStopped = true;
        agent.ResetPath();
    }

    public bool HasReachedDestination()
    {
        if (agent == null || !agent.enabled)
            return true;

        if (agent.pathPending)
            return false;

        if (agent.remainingDistance > destinationReachedThreshold)
            return false;

        if (agent.hasPath && agent.velocity.sqrMagnitude > 0.01f)
            return false;

        return true;
    }

    private void HandleRotation()
    {
        Vector3 velocity = agent.velocity;
        velocity.y = 0f;

        if (velocity.sqrMagnitude < 0.001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(velocity.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotateSpeed);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = hasDestination ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(currentDestination, 0.2f);
    }
}