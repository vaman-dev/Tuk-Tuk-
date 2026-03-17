using UnityEngine;

[DisallowMultipleComponent]
public class NPCInteraction : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NPCBrain brain;
    [SerializeField] private NPCVehicleMount vehicleMount;

    [Header("Detection Settings")]
    [SerializeField] private float detectionRadius = 8f;
    [SerializeField] private float detectionInterval = 1f;
    [SerializeField] private LayerMask vehicleLayerMask;
    [SerializeField] private Transform interactionPoint;

    [Header("Fallback")]
    [Tooltip("How many scans with no available seat before this NPC reverts to Pedestrian.")]
    [SerializeField] private int maxFailedScans = 5;

    private float _detectionTimer;
    private int _failedScanCount;
    private readonly Collider[] _overlapResults = new Collider[10];

    private void Awake()
    {
        if (brain == null)
            brain = GetComponent<NPCBrain>();

        if (vehicleMount == null)
            vehicleMount = GetComponent<NPCVehicleMount>();
    }

    private void Update()
    {
        if (brain == null)
            return;

        if (brain.Role != NPCBrain.NPCRole.Passenger)
            return;

        if (brain.CurrentState != NPCBrain.NPCState.WaitingForPickup)
            return;

        if (brain.IsSeated)
            return;

        _detectionTimer -= Time.deltaTime;
        if (_detectionTimer > 0f)
            return;

        _detectionTimer = detectionInterval;
        ScanForTukTuk();
    }

    private void ScanForTukTuk()
    {
        Vector3 scanOrigin = interactionPoint != null ? interactionPoint.position : transform.position;
        int count = Physics.OverlapSphereNonAlloc(scanOrigin, detectionRadius, _overlapResults, vehicleLayerMask);

        Debug.Log($"[NPCInteraction] {name} | Scanning at {scanOrigin} | radius={detectionRadius} | layerMask={vehicleLayerMask.value} | hits={count}", this);

        if (count == 0)
        {
            Debug.LogWarning($"[NPCInteraction] {name} | No colliders found. Check: Vehicle Layer Mask is not 'Nothing', Tuk Tuk has a Collider on the correct layer.", this);
            HandleFailedScan();
            return;
        }

        for (int i = 0; i < count; i++)
        {
            Collider col = _overlapResults[i];
            Debug.Log($"[NPCInteraction] {name} | Hit: '{col.name}' layer='{LayerMask.LayerToName(col.gameObject.layer)}'", this);

            NPCSeatManager seatManager = col.GetComponentInParent<NPCSeatManager>();

            if (seatManager == null)
            {
                Debug.Log($"[NPCInteraction] {name} | No NPCSeatManager on '{col.name}' or parents. Skipping.", this);
                continue;
            }

            if (!seatManager.HasAvailableSeat())
            {
                Debug.Log($"[NPCInteraction] {name} | NPCSeatManager on '{seatManager.name}' has no available seats.", this);
                continue;
            }

            Debug.Log($"[NPCInteraction] {name} | Found available seat on '{seatManager.name}'. Assigning...", this);

            if (seatManager.TryAssignSeat(vehicleMount))
            {
                Debug.Log($"[NPCInteraction] {name} | Seat assigned successfully!", this);
                _failedScanCount = 0;
                return;
            }
            else
            {
                Debug.LogWarning($"[NPCInteraction] {name} | TryAssignSeat FAILED on '{seatManager.name}'.", this);
            }
        }

        Debug.Log($"[NPCInteraction] {name} | Scan complete. No seat was assigned.", this);
        HandleFailedScan();
    }

    private void HandleFailedScan()
    {
        _failedScanCount++;
        Debug.Log($"[NPCInteraction] {name} | Failed scan {_failedScanCount}/{maxFailedScans}.", this);

        if (_failedScanCount >= maxFailedScans)
        {
            _failedScanCount = 0;
            Debug.Log($"[NPCInteraction] {name} | Max failed scans reached. Reverting to Pedestrian.", this);

            if (brain != null)
                brain.RevertToPedestrian();
        }
    }

    public void ResetFailedScans()
    {
        _failedScanCount = 0;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 center = interactionPoint != null ? interactionPoint.position : transform.position;
        Gizmos.DrawWireSphere(center, detectionRadius);
    }
}