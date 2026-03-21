using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class NPCVehicleMount : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NPCBrain brain;
    [SerializeField] private NPCMovement movement;
    [SerializeField] private NPCAnimator npcAnimator;

    [Header("NPC Visual")]
    [SerializeField] private Transform visualRoot;

    [Header("Settings")]
    [SerializeField] private bool alignVisualToSeatPoint = true;
    [Tooltip("Max time to wait for sit animation before force-seating. Prevents NPCs getting stuck if Animation Event is missing.")]
    [SerializeField] private float maxSitAnimWait = 3f;
    [Tooltip("Max time to wait for stand-up animation (on the seat) before force-exiting. " +
             "Increase this if your StandUp clip is long. Decrease for a faster fallback.")]
    [SerializeField] private float maxExitAnimWait = 3f;

    private TukTukSeat _currentTukTuk;
    private NPCBackSeat _currentBackSeat;
    private NavMeshAgent _agent;
    private CapsuleCollider _capsuleCollider;
    private Rigidbody _rigidbody;
    private bool _isMounted;
    private bool _isWalkingToSeat;
    private bool _isPlayingSitAnim;
    private float _sitAnimTimer;
    private bool _isPlayingExitAnim;
    private float _exitAnimTimer;

    private Transform _originalVisualParent;
    private Vector3 _originalVisualLocalPosition;
    private Quaternion _originalVisualLocalRotation;
    private Vector3 _originalVisualLocalScale;

    public bool IsInVehicle => _isMounted;
    public TukTukSeat CurrentTukTuk => _currentTukTuk;
    public NPCBackSeat CurrentBackSeat => _currentBackSeat;

    private void Awake()
    {
        if (brain == null)
            brain = GetComponent<NPCBrain>();

        if (movement == null)
            movement = GetComponent<NPCMovement>();

        if (npcAnimator == null)
            npcAnimator = GetComponent<NPCAnimator>();

        _agent = GetComponent<NavMeshAgent>();
        _capsuleCollider = GetComponent<CapsuleCollider>();
        _rigidbody = GetComponent<Rigidbody>();

        CacheOriginalVisualState();
    }

    private void OnEnable()
    {
        if (npcAnimator != null)
        {
            npcAnimator.OnSitComplete += HandleSitComplete;
            npcAnimator.OnStandComplete += HandleStandComplete;
        }
    }

    private void OnDisable()
    {
        if (npcAnimator != null)
        {
            npcAnimator.OnSitComplete -= HandleSitComplete;
            npcAnimator.OnStandComplete -= HandleStandComplete;
        }

        CleanupOnDestroyOrDisable();
    }

    private void OnDestroy()
    {
        CleanupOnDestroyOrDisable();
    }

    private void CleanupOnDestroyOrDisable()
    {
        if (_currentBackSeat != null)
        {
            if (_isMounted)
            {
                _currentBackSeat.OnNPCExited(this);
                Debug.Log($"[NPCVehicleMount] {name} | Cleanup: force-exited seat '{_currentBackSeat.SeatName}' on disable/destroy.", this);
            }
            else
            {
                _currentBackSeat.ClearReservation(this);
                Debug.Log($"[NPCVehicleMount] {name} | Cleanup: cleared reservation on seat '{_currentBackSeat.SeatName}' on disable/destroy.", this);
            }

            _currentBackSeat = null;
        }

        _currentTukTuk = null;
        _isMounted = false;
        _isWalkingToSeat = false;
        _isPlayingSitAnim = false;
        _isPlayingExitAnim = false;
    }

    public void WalkToAndEnter(TukTukSeat tukTuk, NPCBackSeat backSeat)
    {
        if (tukTuk == null)
        {
            Debug.LogError($"[NPCVehicleMount] {name} | WalkToAndEnter: tukTuk is NULL!", this);
            return;
        }

        if (backSeat == null)
        {
            Debug.LogError($"[NPCVehicleMount] {name} | WalkToAndEnter: backSeat is NULL!", this);
            return;
        }

        if (_isMounted || _isWalkingToSeat)
        {
            Debug.Log($"[NPCVehicleMount] {name} | Already mounted or walking to seat, ignoring.", this);
            return;
        }

        if (backSeat.IsOccupied)
        {
            Debug.Log($"[NPCVehicleMount] {name} | Seat '{backSeat.SeatName}' is already occupied.", this);
            backSeat.ClearReservation(this);
            return;
        }

        _currentTukTuk = tukTuk;
        _currentBackSeat = backSeat;
        _isWalkingToSeat = true;

        if (brain != null)
            brain.SetWaiting();

        Transform target = backSeat.SeatPoint != null ? backSeat.SeatPoint : tukTuk.transform;

        Debug.Log($"[NPCVehicleMount] {name} | Walking to seat '{backSeat.SeatName}' at {target.position}", this);

        if (movement != null)
            movement.MoveTo(target.position);
        else
            Debug.LogError($"[NPCVehicleMount] {name} | movement is NULL, cannot walk to seat!", this);
    }

    private void Update()
    {
        if (_isWalkingToSeat && movement != null && movement.HasReachedDestination())
        {
            Debug.Log($"[NPCVehicleMount] {name} | Arrived at seat point. Mounting seat '{_currentBackSeat?.SeatName}'...", this);
            MountSeat();
            return;
        }

        if (_isPlayingSitAnim)
        {
            _sitAnimTimer -= Time.deltaTime;
            if (_sitAnimTimer <= 0f)
            {
                Debug.LogWarning($"[NPCVehicleMount] {name} | Sit animation timed out after {maxSitAnimWait}s. Force-seating.", this);
                HandleSitComplete();
            }
        }

        if (_isPlayingExitAnim)
        {
            _exitAnimTimer -= Time.deltaTime;
            if (_exitAnimTimer <= 0f)
            {
                Debug.LogWarning($"[NPCVehicleMount] {name} | Stand animation timed out after {maxExitAnimWait}s. Force-finishing exit.", this);
                HandleStandComplete();
            }
        }
    }

    public void EnterVehicle(TukTukSeat tukTuk, NPCBackSeat backSeat)
    {
        if (tukTuk == null || backSeat == null || _isMounted || _isWalkingToSeat || backSeat.IsOccupied)
            return;

        _currentTukTuk = tukTuk;
        _currentBackSeat = backSeat;

        Debug.Log($"[NPCVehicleMount] {name} | Instant enter seat '{backSeat.SeatName}'.", this);
        MountSeat();
    }

    private void MountSeat()
    {
        if (_currentBackSeat == null)
        {
            Debug.LogError($"[NPCVehicleMount] {name} | MountSeat: _currentBackSeat is NULL!", this);
            _isWalkingToSeat = false;
            return;
        }

        if (_currentBackSeat.IsOccupied)
        {
            Debug.Log($"[NPCVehicleMount] {name} | Seat '{_currentBackSeat.SeatName}' was taken while walking. Aborting mount.", this);
            _currentBackSeat.ClearReservation(this);
            _currentBackSeat = null;
            _currentTukTuk = null;
            _isWalkingToSeat = false;

            if (brain != null)
                brain.OnSeatUnavailable();

            return;
        }

        _isWalkingToSeat = false;
        _isMounted = true;

        if (movement != null)
            movement.Stop();

        if (_agent != null)
        {
            _agent.enabled = false;
            Debug.Log($"[NPCVehicleMount] {name} | NavMeshAgent disabled.", this);
        }

        SetPhysicsEnabled(false);
        CacheOriginalVisualState();

        if (_currentBackSeat.SeatPoint != null)
        {
            transform.position = _currentBackSeat.SeatPoint.position;
            transform.rotation = Quaternion.Euler(0f, _currentBackSeat.SeatPoint.eulerAngles.y, 0f);
            Debug.Log($"[NPCVehicleMount] {name} | Root positioned at {transform.position}", this);
        }
        else
        {
            Debug.LogWarning($"[NPCVehicleMount] {name} | Seat '{_currentBackSeat.SeatName}' has no SeatPoint assigned!", this);
        }

        if (visualRoot == null)
            Debug.LogWarning($"[NPCVehicleMount] {name} | visualRoot is NULL! NPC visual won't move to seat.", this);
        else if (!alignVisualToSeatPoint)
            Debug.Log($"[NPCVehicleMount] {name} | alignVisualToSeatPoint is OFF, skipping visual alignment.", this);
        else if (_currentBackSeat.VisualSeatPoint == null)
            Debug.LogWarning($"[NPCVehicleMount] {name} | Seat '{_currentBackSeat.SeatName}' has no VisualSeatPoint assigned!", this);

        ApplyVisualSeatAlignment();

        _currentBackSeat.OnNPCEntered(this);

        Debug.Log($"[NPCVehicleMount] {name} | MOUNTED seat '{_currentBackSeat.SeatName}' | IsOccupied={_currentBackSeat.IsOccupied}", this);

        if (npcAnimator != null)
        {
            _isPlayingSitAnim = true;
            _sitAnimTimer = maxSitAnimWait;
            npcAnimator.PlayStandToSit();
            Debug.Log($"[NPCVehicleMount] {name} | Sit animation triggered. Timeout in {maxSitAnimWait}s if no callback.", this);
        }
        else
        {
            if (brain != null)
                brain.OnSeated();
        }
    }

    private void HandleSitComplete()
    {
        if (!_isMounted)
            return;

        _isPlayingSitAnim = false;

        Debug.Log($"[NPCVehicleMount] {name} | Sit animation complete. NPC is now fully seated.", this);

        if (brain != null)
            brain.OnSeated();
    }

    // ── EXIT FLOW ──
    // Step 1: ExitVehicle()    → play stand animation ON THE SEAT (NPC stays parented to seat)
    // Step 2: HandleStandComplete() → animation done, now detach + teleport to exit point
    // Step 3: ResumeAfterExit()     → tell brain to resume roaming

    public void ExitVehicle()
    {
        if (!_isMounted || _currentBackSeat == null || _isPlayingExitAnim)
            return;

        if (npcAnimator != null)
        {
            // Play stand-up animation while NPC is still on the seat
            _isPlayingExitAnim = true;
            _exitAnimTimer = maxExitAnimWait;
            npcAnimator.PlaySitToStand();
            Debug.Log($"[NPCVehicleMount] {name} | Stand animation playing ON seat '{_currentBackSeat.SeatName}'. " +
                      $"NPC will detach after animation finishes (or timeout in {maxExitAnimWait}s).", this);
        }
        else
        {
            // No animator — detach and resume immediately
            DetachFromSeat();
            ResumeAfterExit();
        }
    }

    private void HandleStandComplete()
    {
        if (!_isPlayingExitAnim)
            return;

        _isPlayingExitAnim = false;

        Debug.Log($"[NPCVehicleMount] {name} | Stand animation complete. Now detaching from seat.", this);

        // Animation is done — NOW detach and teleport to exit point
        DetachFromSeat();
        ResumeAfterExit();
    }

    /// <summary>
    /// Detaches the NPC from the seat: restores visual, repositions to exit point,
    /// re-enables physics and NavMeshAgent, frees the seat.
    /// Only called AFTER the stand-up animation finishes.
    /// </summary>
    private void DetachFromSeat()
    {
        if (_currentBackSeat == null)
            return;

        string seatName = _currentBackSeat.SeatName;
        Debug.Log($"[NPCVehicleMount] {name} | Detaching from seat '{seatName}'...", this);

        NPCBackSeat previousSeat = _currentBackSeat;

        // Restore visual root back to NPC parent
        RestoreVisualRoot();

        // Reposition NPC to exit point
        Transform exitTarget = previousSeat.ExitPoint;
        if (exitTarget == null && _currentTukTuk != null)
            exitTarget = _currentTukTuk.ExitPoint;
        if (exitTarget == null && _currentTukTuk != null)
            exitTarget = _currentTukTuk.transform;

        if (exitTarget != null)
        {
            transform.position = exitTarget.position;
            transform.rotation = Quaternion.Euler(0f, exitTarget.eulerAngles.y, 0f);
        }
        else
        {
            Debug.LogWarning($"[NPCVehicleMount] {name} | No exit point found! NPC will exit at current position.", this);
        }

        // Re-enable physics
        SetPhysicsEnabled(true);

        // Re-enable NavMeshAgent and warp to valid NavMesh position
        if (_agent != null)
        {
            _agent.enabled = true;

            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                _agent.Warp(hit.position);
                Debug.Log($"[NPCVehicleMount] {name} | NavMeshAgent re-enabled & warped to {hit.position}.", this);
            }
            else
            {
                Debug.LogWarning($"[NPCVehicleMount] {name} | NavMeshAgent re-enabled but no valid NavMesh position found near {transform.position}!", this);
            }
        }

        // Free the seat
        previousSeat.OnNPCExited(this);

        // Clear references
        _currentBackSeat = null;
        _currentTukTuk = null;
        _isMounted = false;
        _isWalkingToSeat = false;

        Debug.Log($"[NPCVehicleMount] {name} | Detached from seat '{seatName}' | Seat IsOccupied={previousSeat.IsOccupied}", this);
    }

    private void ResumeAfterExit()
    {
        Debug.Log($"[NPCVehicleMount] {name} | Resuming NPC behaviour after vehicle exit.", this);

        if (brain != null)
            brain.OnExitedVehicle();
    }

    private void SetPhysicsEnabled(bool enabled)
    {
        if (_capsuleCollider != null)
        {
            _capsuleCollider.enabled = enabled;
            Debug.Log($"[NPCVehicleMount] {name} | CapsuleCollider {(enabled ? "enabled" : "disabled")}.", this);
        }

        if (_rigidbody != null)
        {
            _rigidbody.isKinematic = !enabled;
            Debug.Log($"[NPCVehicleMount] {name} | Rigidbody isKinematic={!enabled}.", this);
        }
    }

    private void ApplyVisualSeatAlignment()
    {
        if (visualRoot == null || !alignVisualToSeatPoint || _currentBackSeat == null)
            return;

        Transform target = _currentBackSeat.VisualSeatPoint;
        if (target == null)
            return;

        visualRoot.SetParent(target, false);
        visualRoot.localPosition = Vector3.zero;
        visualRoot.localRotation = Quaternion.identity;
        visualRoot.localScale = Vector3.one;

        Debug.Log($"[NPCVehicleMount] {name} | Visual reparented to '{target.name}' at {target.position}", this);
    }

    private void RestoreVisualRoot()
    {
        if (visualRoot == null)
            return;

        visualRoot.SetParent(_originalVisualParent, false);
        visualRoot.localPosition = _originalVisualLocalPosition;
        visualRoot.localRotation = _originalVisualLocalRotation;
        visualRoot.localScale = _originalVisualLocalScale;

        Debug.Log($"[NPCVehicleMount] {name} | Visual restored to original parent.", this);
    }

    private void CacheOriginalVisualState()
    {
        if (visualRoot == null)
            return;

        _originalVisualParent = visualRoot.parent;
        _originalVisualLocalPosition = visualRoot.localPosition;
        _originalVisualLocalRotation = visualRoot.localRotation;
        _originalVisualLocalScale = visualRoot.localScale;
    }
}