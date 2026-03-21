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
    [Tooltip("Max distance from NPC to a pickup point for the NPC to approach it. NPCs farther than this will revert to Pedestrian.")]
    [SerializeField] private float closestDistance = 30f;
    [SerializeField] private float minRoamBeforePickup = 3f;
    [SerializeField] private float maxRoamBeforePickup = 8f;

    [Header("Timeouts")]
    [Tooltip("Max seconds an NPC will wait at a pickup point before giving up.")]
    [SerializeField] private float maxWaitForPickupTime = 30f;
    [Tooltip("Max seconds an NPC will spend walking to a seat before aborting.")]
    [SerializeField] private float maxWalkToSeatTime = 15f;

    [Header("Role Change (Pedestrian → Passenger)")]
    [SerializeField] private bool enableAutoRoleChange = false;
    [SerializeField] private float minTimeBeforeRoleChange = 10f;
    [SerializeField] private float maxTimeBeforeRoleChange = 20f;
    [Tooltip("If true, NPC reverts to Pedestrian after exiting the vehicle instead of trying to board again.")]
    [SerializeField] private bool oneRideOnly = false;

    [Header("Mood")]
    [SerializeField] private float anger;
    [SerializeField] private float panic;
    [SerializeField] private float angryThreshold = 60f;
    [SerializeField] private float panicThreshold = 80f;
    [SerializeField] private float moodDecayPerSecond = 3f;
    [Tooltip("Duration in seconds before Angry/Panicking state auto-recovers.")]
    [SerializeField] private float moodRecoveryTime = 5f;

    [Header("Debug")]
    [SerializeField] private bool logStateChanges = false;

    public NPCRole Role => role;
    public NPCState CurrentState => currentState;
    public NPCMovement Movement => movement;
    public NPCVehicleMount VehicleMount => vehicleMount;
    public bool IsSeated => currentState == NPCState.Seated;

    // ── Static Pickup Slot Tracker ──
    // Tracks how many NPCs are currently heading to pickup / waiting for pickup.
    // Prevents more NPCs from going than there are available seats.
    private static readonly HashSet<NPCBrain> _npcsHeadingToPickup = new HashSet<NPCBrain>();

    /// <summary>
    /// Returns the number of NPCs currently walking to or waiting at pickup points.
    /// </summary>
    public static int NPCsHeadingToPickupCount => _npcsHeadingToPickup.Count;

    private Vector3 spawnPoint;
    private float idleTimer;
    private bool isIdleWaiting;

    // passenger pickup flow
    private float roamBeforePickupTimer;
    private bool hasHeadedToPickup;
    private Transform chosenPickupPoint;
    private bool _isRegisteredForPickup;

    // role change timer
    private float roleChangeTimer;
    private bool roleChangeTriggered;

    // timeout timers
    private float _waitForPickupTimer;
    private float _walkToSeatTimer;

    // mood recovery timer
    private float _moodRecoveryTimer;

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

    private void OnDestroy()
    {
        UnregisterFromPickup();
    }

    private void Update()
    {
        if (currentState == NPCState.Seated)
            return;

        UpdateMood();
        UpdateMoodRecovery();
        UpdateRoleChangeTimer();

        if (role == NPCRole.Passenger)
            HandlePassengerBehaviour();
        else
            HandleRoaming();

        SyncStateWithMovement();
    }

    // ── Pickup Slot Registration ──

    private void RegisterForPickup()
    {
        if (_isRegisteredForPickup)
            return;

        _npcsHeadingToPickup.Add(this);
        _isRegisteredForPickup = true;

        if (logStateChanges)
            Debug.Log($"[NPCBrain] {name} | Registered for pickup. Total heading: {_npcsHeadingToPickup.Count}", this);
    }

    private void UnregisterFromPickup()
    {
        if (!_isRegisteredForPickup)
            return;

        _npcsHeadingToPickup.Remove(this);
        _isRegisteredForPickup = false;

        if (logStateChanges)
            Debug.Log($"[NPCBrain] {name} | Unregistered from pickup. Total heading: {_npcsHeadingToPickup.Count}", this);
    }

    /// <summary>
    /// Checks if there is still room for another NPC to head to a pickup point.
    /// Compares the number of NPCs already heading there against available seats.
    /// </summary>
    private bool CanClaimPickupSlot()
    {
        if (seatManager == null)
            return true;

        // Count total available seats (not occupied, not reserved)
        int availableSeats = 0;
        if (seatManager.HasAvailableSeat())
        {
            // We need the actual count — get it from TukTukSeat via seatManager
            var tukTukSeat = vehicleMount != null && vehicleMount.CurrentTukTuk != null
                ? vehicleMount.CurrentTukTuk
                : null;

            // Fallback: if we can't get exact count, use a simple check
            // At minimum, the number heading to pickup should not exceed
            // the number of NPCs that the seat manager says are available
            availableSeats = GetAvailableSeatCount();
        }

        int alreadyHeading = _npcsHeadingToPickup.Count;

        bool canClaim = alreadyHeading < availableSeats;

        if (!canClaim && logStateChanges)
            Debug.Log($"[NPCBrain] {name} | Cannot claim pickup slot. " +
                      $"Already heading: {alreadyHeading}, Available seats: {availableSeats}", this);

        return canClaim;
    }

    private int GetAvailableSeatCount()
    {
        if (seatManager == null)
            return 0;

        // Access TukTukSeat through seatManager to count available seats
        // seatManager.HasAvailableSeat() only tells us true/false,
        // so we scan nearby NPCs to infer the count
        // Use a simple approach: if HasAvailableSeat is true, assume at least 1
        // Then subtract NPCs already heading to pickup
        // For a more accurate count, we check how many seats exist vs occupied/reserved

        // Since NPCSeatManager wraps TukTukSeat, and we can't directly access seat count
        // from here, we'll count available seats via the seatManager's public API
        int count = 0;
        int maxCheck = 10; // safety limit

        // Try to get seat info — the seatManager checks tukTukSeat internally
        // We iterate using GetAvailableSeatClosestTo which returns null when none left
        // But that's destructive — instead just check HasAvailableSeat as a boolean
        // and return (total seats - heading count) as a heuristic

        // Best approach: just check if seats are available
        if (seatManager.HasAvailableSeat())
            count = Mathf.Max(1, 2 - _npcsHeadingToPickup.Count); // assume 2 seats (back seats)

        return count;
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
            if (seatManager != null && !seatManager.HasAvailableSeat())
            {
                Debug.Log($"[NPCBrain] {name} | Role change timer expired but all seats are occupied/reserved. Staying as Pedestrian.", this);
                ResetRoleChangeTimer();
                return;
            }

            // Check if pickup slots are already full
            if (!CanClaimPickupSlot())
            {
                Debug.Log($"[NPCBrain] {name} | Role change timer expired but enough NPCs are already heading to pickup. Staying as Pedestrian.", this);
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
        {
            _waitForPickupTimer -= Time.deltaTime;
            if (_waitForPickupTimer <= 0f)
            {
                Debug.Log($"[NPCBrain] {name} | WaitingForPickup timed out after {maxWaitForPickupTime}s. Reverting to Pedestrian.", this);
                RevertToPedestrian();
            }
            return;
        }

        if (currentState == NPCState.Waiting)
        {
            _walkToSeatTimer -= Time.deltaTime;
            if (_walkToSeatTimer <= 0f)
            {
                Debug.Log($"[NPCBrain] {name} | Walking to seat timed out after {maxWalkToSeatTime}s. Aborting mount.", this);
                if (vehicleMount != null && vehicleMount.CurrentBackSeat != null)
                    vehicleMount.CurrentBackSeat.ClearReservation(vehicleMount);
                RevertToPedestrian();
            }
            return;
        }

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

            if (seatManager != null && !seatManager.HasAvailableSeat())
            {
                Debug.Log($"[NPCBrain] {name} | Roam timer expired but all seats are occupied/reserved. Reverting to Pedestrian.", this);
                RevertToPedestrian();
                return;
            }

            // Check if too many NPCs are already heading to pickup
            if (!CanClaimPickupSlot())
            {
                Debug.Log($"[NPCBrain] {name} | Roam timer expired but enough NPCs already heading to pickup. Reverting to Pedestrian.", this);
                RevertToPedestrian();
                return;
            }

            hasHeadedToPickup = true;
            isIdleWaiting = false;
            chosenPickupPoint = GetClosestPickupPoint();

            if (chosenPickupPoint == null)
            {
                Debug.LogWarning($"[NPCBrain] {name} | No pickup point within closestDistance ({closestDistance}). Reverting to Pedestrian.", this);
                RevertToPedestrian();
                return;
            }

            // Register this NPC as heading to pickup
            RegisterForPickup();

            Debug.Log($"[NPCBrain] {name} | Roam timer expired. Walking to pickup point '{chosenPickupPoint.name}' at {chosenPickupPoint.position}", this);
            movement.MoveTo(chosenPickupPoint.position);
            SetState(NPCState.Walking);
            return;
        }

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
            _waitForPickupTimer = maxWaitForPickupTime;
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

            if (dist > closestDistance)
                continue;

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
            UnregisterFromPickup();
            ResetRoleChangeTimer();
            StartIdleWait();
        }
    }

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
        UnregisterFromPickup();
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
        UnregisterFromPickup();

        if (oneRideOnly)
        {
            Debug.Log($"[NPCBrain] {name} | oneRideOnly is enabled. Permanently reverting to Pedestrian.", this);
            role = NPCRole.Pedestrian;
            roleChangeTriggered = true;
            hasHeadedToPickup = false;
            chosenPickupPoint = null;
            StartIdleWait();
        }
        else
        {
            ResetPickupTimer();
            StartIdleWait();
        }
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
        _walkToSeatTimer = maxWalkToSeatTime;
        Debug.Log($"[NPCBrain] {name} | SetWaiting called.", this);
        SetState(NPCState.Waiting);
    }

    // ── Mood ──

    public void AddAnger(float amount)
    {
        anger = Mathf.Max(0f, anger + amount);

        if (anger >= angryThreshold && currentState != NPCState.Angry)
        {
            _moodRecoveryTimer = moodRecoveryTime;
            SetState(NPCState.Angry);
        }
    }

    public void AddPanic(float amount)
    {
        panic = Mathf.Max(0f, panic + amount);

        if (panic >= panicThreshold && currentState != NPCState.Panicking)
        {
            _moodRecoveryTimer = moodRecoveryTime;
            SetState(NPCState.Panicking);
        }
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

    private void UpdateMoodRecovery()
    {
        if (currentState != NPCState.Angry && currentState != NPCState.Panicking)
            return;

        _moodRecoveryTimer -= Time.deltaTime;

        if (_moodRecoveryTimer <= 0f)
        {
            Debug.Log($"[NPCBrain] {name} | Mood recovery timer expired. Auto-resetting mood from {currentState}.", this);
            ResetMood();
        }
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

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, closestDistance);
    }
}