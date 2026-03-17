using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class NPCBrain : MonoBehaviour
{
    public enum NPCRole
    {
        Pedestrian,
        Passenger
    }

    public enum NPCState
    {
        Idle,
        Walking,
        Waiting,
        WaitingForPickup,
        Angry,
        Panicking,
        Seated
    }

    [Header("Identity")]
    [SerializeField] private NPCRole role = NPCRole.Pedestrian;
    [SerializeField] private NPCState currentState = NPCState.Idle;

    [Header("References")]
    [SerializeField] private NPCMovement movement;
    [SerializeField] private NPCVehicleMount vehicleMount;
    [SerializeField] private NPCSeatManager seatManager;

    [Header("Roaming")]
    [SerializeField] private float roamRadius = 10f;
    [SerializeField] private float minIdleTime = 1f;
    [SerializeField] private float maxIdleTime = 4f;

    [Header("Passenger Settings")]
    [SerializeField] private List<Transform> pickupPoints = new List<Transform>();
    [SerializeField] private float minRoamBeforePickup = 3f;
    [SerializeField] private float maxRoamBeforePickup = 8f;

    [Header("Role Change (Pedestrian → Passenger)")]
    [SerializeField] private bool enableAutoRoleChange = false;
    [SerializeField] private float minTimeBeforeRoleChange = 10f;
    [SerializeField] private float maxTimeBeforeRoleChange = 20f;

    [Header("Mood")]
    [SerializeField] private float anger;
    [SerializeField] private float panic;
    [SerializeField] private float angryThreshold = 60f;
    [SerializeField] private float panicThreshold = 80f;
    [SerializeField] private float moodDecayPerSecond = 3f;

    [Header("Debug")]
    [SerializeField] private bool logStateChanges = false;

    public NPCRole Role => role;
    public NPCState CurrentState => currentState;
    public NPCMovement Movement => movement;
    public NPCVehicleMount VehicleMount => vehicleMount;
    public bool IsSeated => currentState == NPCState.Seated;

    private Vector3 spawnPoint;
    private float idleTimer;
    private bool isIdleWaiting;

    // passenger pickup flow
    private float roamBeforePickupTimer;
    private bool hasHeadedToPickup;
    private Transform chosenPickupPoint;

    // role change timer
    private float roleChangeTimer;
    private bool roleChangeTriggered;

    private void Awake()
    {
        if (movement == null)
            movement = GetComponent<NPCMovement>();

        if (vehicleMount == null)
            vehicleMount = GetComponent<NPCVehicleMount>();
    }

    private void Start()
    {
        spawnPoint = transform.position;

        if (role == NPCRole.Passenger)
        {
            // If starting as Passenger, check seat availability first
            if (seatManager != null && !seatManager.HasAvailableSeat())
            {
                Debug.Log($"[NPCBrain] {name} | Started as PASSENGER but no seats available. Falling back to PEDESTRIAN.", this);
                role = NPCRole.Pedestrian;
                ResetRoleChangeTimer();
            }
            else
            {
                ResetPickupTimer();
                roleChangeTriggered = true;
                Debug.Log($"[NPCBrain] {name} | Started as PASSENGER | pickupPoints={pickupPoints.Count} | roamTimer={roamBeforePickupTimer:F1}s", this);
            }
        }

        if (role == NPCRole.Pedestrian)
        {
            ResetRoleChangeTimer();
            Debug.Log($"[NPCBrain] {name} | Started as PEDESTRIAN | roleChange={enableAutoRoleChange} | roleChangeTimer={roleChangeTimer:F1}s", this);
        }

        StartIdleWait();
    }

    private void Update()
    {
        if (currentState == NPCState.Seated)
            return;

        UpdateMood();
        UpdateRoleChangeTimer();

        if (role == NPCRole.Passenger)
            HandlePassengerBehaviour();
        else
            HandleRoaming();

        SyncStateWithMovement();
    }

    // ── Role Change Timer ──

    private void ResetRoleChangeTimer()
    {
        if (enableAutoRoleChange)
        {
            roleChangeTimer = Random.Range(minTimeBeforeRoleChange, maxTimeBeforeRoleChange);
            roleChangeTriggered = false;
        }
        else
        {
            roleChangeTriggered = true;
        }
    }

    private void UpdateRoleChangeTimer()
    {
        if (roleChangeTriggered)
            return;

        if (role != NPCRole.Pedestrian)
            return;

        roleChangeTimer -= Time.deltaTime;

        if (roleChangeTimer <= 0f)
        {
            // Check seat availability BEFORE converting to Passenger
            if (seatManager != null && !seatManager.HasAvailableSeat())
            {
                Debug.Log($"[NPCBrain] {name} | Role change timer expired but all seats are occupied/reserved. Staying as Pedestrian.", this);
                ResetRoleChangeTimer();
                return;
            }

            roleChangeTriggered = true;
            Debug.Log($"[NPCBrain] {name} | Role change timer expired. Converting Pedestrian -> Passenger.", this);
            SetRole(NPCRole.Passenger);
        }
    }

    // ── Pedestrian Roaming ──

    private void HandleRoaming()
    {
        if (movement == null)
            return;

        if (currentState == NPCState.Angry || currentState == NPCState.Panicking)
            return;

        if (isIdleWaiting)
        {
            idleTimer -= Time.deltaTime;
            if (idleTimer <= 0f)
            {
                isIdleWaiting = false;
                RoamToRandomPoint();
            }
        }
        else if (!movement.HasDestination && movement.HasReachedDestination())
        {
            StartIdleWait();
        }
    }

    // ── Passenger Behaviour ──

    private void HandlePassengerBehaviour()
    {
        if (movement == null)
            return;

        if (currentState == NPCState.Angry || currentState == NPCState.Panicking)
            return;

        if (currentState == NPCState.WaitingForPickup)
            return;

        // If NPC is in Waiting state (walking to seat via NPCVehicleMount),
        // let NPCVehicleMount handle it — don't interfere
        if (currentState == NPCState.Waiting)
            return;

        if (pickupPoints == null || pickupPoints.Count == 0)
        {
            Debug.LogWarning($"[NPCBrain] {name} | No pickup points assigned! Reverting to Pedestrian.", this);
            RevertToPedestrian();
            return;
        }

        if (!hasHeadedToPickup)
        {
            roamBeforePickupTimer -= Time.deltaTime;

            if (roamBeforePickupTimer > 0f)
            {
                HandleRoaming();
                return;
            }

            // Check seat availability BEFORE walking to pickup point
            if (seatManager != null && !seatManager.HasAvailableSeat())
            {
                Debug.Log($"[NPCBrain] {name} | Roam timer expired but all seats are occupied/reserved. Reverting to Pedestrian.", this);
                RevertToPedestrian();
                return;
            }

            hasHeadedToPickup = true;
            isIdleWaiting = false;
            chosenPickupPoint = GetClosestPickupPoint();

            if (chosenPickupPoint == null)
            {
                Debug.LogWarning($"[NPCBrain] {name} | All pickup points are null! Reverting to Pedestrian.", this);
                RevertToPedestrian();
                return;
            }

            Debug.Log($"[NPCBrain] {name} | Roam timer expired. Walking to pickup point '{chosenPickupPoint.name}' at {chosenPickupPoint.position}", this);
            movement.MoveTo(chosenPickupPoint.position);
            SetState(NPCState.Walking);
            return;
        }

        // NPC is walking to pickup point — re-check seat availability while walking
        if (currentState == NPCState.Walking && seatManager != null && !seatManager.HasAvailableSeat())
        {
            Debug.Log($"[NPCBrain] {name} | Seats became occupied/reserved while walking to pickup. Reverting to Pedestrian.", this);
            RevertToPedestrian();
            return;
        }

        if (!movement.HasDestination && movement.HasReachedDestination())
        {
            movement.Stop();
            Debug.Log($"[NPCBrain] {name} | Arrived at pickup point '{chosenPickupPoint.name}'. Now WaitingForPickup.", this);
            SetState(NPCState.WaitingForPickup);
        }
    }

    public void OnSeatUnavailable()
    {
        Debug.Log($"[NPCBrain] {name} | Seat unavailable. Reverting to Pedestrian.", this);
        RevertToPedestrian();
    }

    private Transform GetClosestPickupPoint()
    {
        Transform closest = null;
        float closestDist = float.MaxValue;

        for (int i = 0; i < pickupPoints.Count; i++)
        {
            if (pickupPoints[i] == null)
                continue;

            float dist = Vector3.Distance(transform.position, pickupPoints[i].position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = pickupPoints[i];
            }
        }

        return closest;
    }

    private Transform GetAlternatePickupPoint()
    {
        for (int i = 0; i < pickupPoints.Count; i++)
        {
            if (pickupPoints[i] == null)
                continue;

            if (pickupPoints[i] != chosenPickupPoint)
                return pickupPoints[i];
        }

        return null;
    }

    private void ResetPickupTimer()
    {
        roamBeforePickupTimer = Random.Range(minRoamBeforePickup, maxRoamBeforePickup);
        hasHeadedToPickup = false;
        chosenPickupPoint = null;
    }

    // ── Shared Helpers ──

    private void StartIdleWait()
    {
        if (movement != null)
            movement.Stop();

        isIdleWaiting = true;
        idleTimer = Random.Range(minIdleTime, maxIdleTime);
        SetState(NPCState.Idle);
    }

    private void RoamToRandomPoint()
    {
        if (TryGetRandomPoint(spawnPoint, roamRadius, out Vector3 point))
        {
            movement.MoveTo(point);
            SetState(NPCState.Walking);
        }
        else
        {
            StartIdleWait();
        }
    }

    private bool TryGetRandomPoint(Vector3 center, float radius, out Vector3 result)
    {
        for (int i = 0; i < 5; i++)
        {
            Vector3 randomDir = Random.insideUnitSphere * radius;
            randomDir.y = 0f;
            Vector3 candidate = center + randomDir;

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
        }

        result = Vector3.zero;
        return false;
    }

    public void SetRole(NPCRole newRole)
    {
        if (role == newRole)
            return;

        Debug.Log($"[NPCBrain] {name} | Role changed: {role} -> {newRole}", this);
        role = newRole;

        if (newRole == NPCRole.Passenger)
        {
            roleChangeTriggered = true;
            ResetPickupTimer();
            StartIdleWait();
        }
        else if (newRole == NPCRole.Pedestrian)
        {
            hasHeadedToPickup = false;
            chosenPickupPoint = null;
            ResetRoleChangeTimer();
            StartIdleWait();
        }
    }

    /// <summary>
    /// Called when the NPC cannot find an available seat after multiple attempts.
    /// Reverts to Pedestrian and restarts the auto role-change timer if enabled.
    /// </summary>
    public void RevertToPedestrian()
    {
        if (role == NPCRole.Pedestrian)
            return;

        Debug.Log($"[NPCBrain] {name} | Reverting to Pedestrian. No seats available.", this);
        spawnPoint = transform.position;
        SetRole(NPCRole.Pedestrian);
    }

    public void SetState(NPCState newState)
    {
        if (currentState == newState)
            return;

        if (logStateChanges)
            Debug.Log($"[NPCBrain] {name} | State: {currentState} -> {newState}", this);

        currentState = newState;
    }

    // ── Vehicle Seat Integration ──

    public void OnSeated()
    {
        isIdleWaiting = false;
        Debug.Log($"[NPCBrain] {name} | OnSeated called. NPC is now SEATED.", this);
        SetState(NPCState.Seated);
    }

    public void LeaveSeat()
    {
        if (vehicleMount == null || !vehicleMount.IsInVehicle)
            return;

        Debug.Log($"[NPCBrain] {name} | LeaveSeat called.", this);
        vehicleMount.ExitVehicle();
    }

    public void OnExitedVehicle()
    {
        Debug.Log($"[NPCBrain] {name} | OnExitedVehicle called. Resuming roam cycle.", this);
        spawnPoint = transform.position;
        ResetPickupTimer();
        StartIdleWait();
    }

    // ── Movement ──

    public void MoveTo(Vector3 worldPoint)
    {
        if (movement == null)
            return;

        isIdleWaiting = false;
        movement.MoveTo(worldPoint);
        SetState(NPCState.Walking);
    }

    public void StopMoving()
    {
        if (movement == null)
            return;

        isIdleWaiting = false;
        movement.Stop();
        SetState(NPCState.Idle);
    }

    public void SetWaiting()
    {
        if (movement != null)
            movement.Stop();

        isIdleWaiting = false;
        Debug.Log($"[NPCBrain] {name} | SetWaiting called.", this);
        SetState(NPCState.Waiting);
    }

    // ── Mood ──

    public void AddAnger(float amount)
    {
        anger = Mathf.Max(0f, anger + amount);

        if (anger >= angryThreshold)
            SetState(NPCState.Angry);
    }

    public void AddPanic(float amount)
    {
        panic = Mathf.Max(0f, panic + amount);

        if (panic >= panicThreshold)
            SetState(NPCState.Panicking);
    }

    public void ResetMood()
    {
        anger = 0f;
        panic = 0f;

        if (currentState == NPCState.Angry || currentState == NPCState.Panicking)
        {
            SetState(NPCState.Idle);
            StartIdleWait();
        }
    }

    private void UpdateMood()
    {
        if (currentState == NPCState.Angry || currentState == NPCState.Panicking)
            return;

        anger = Mathf.MoveTowards(anger, 0f, moodDecayPerSecond * Time.deltaTime);
        panic = Mathf.MoveTowards(panic, 0f, moodDecayPerSecond * Time.deltaTime);
    }

    private void SyncStateWithMovement()
    {
        if (movement == null)
            return;

        if (currentState == NPCState.Angry || currentState == NPCState.Panicking
            || currentState == NPCState.Waiting || currentState == NPCState.WaitingForPickup
            || currentState == NPCState.Seated)
            return;

        if (movement.IsMoving)
            SetState(NPCState.Walking);
        else
            SetState(NPCState.Idle);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 center = Application.isPlaying ? spawnPoint : transform.position;
        Gizmos.DrawWireSphere(center, roamRadius);

        if (role == NPCRole.Passenger && pickupPoints != null)
        {
            for (int i = 0; i < pickupPoints.Count; i++)
            {
                if (pickupPoints[i] == null)
                    continue;

                Gizmos.color = (chosenPickupPoint == pickupPoints[i]) ? Color.yellow : Color.green;
                Gizmos.DrawWireSphere(pickupPoints[i].position, 0.3f);
                Gizmos.DrawLine(transform.position, pickupPoints[i].position);
            }
        }
    }
}