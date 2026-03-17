using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class NPCVehicleMount : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NPCBrain brain;
    [SerializeField] private NPCMovement movement;

    [Header("NPC Visual")]
    [SerializeField] private Transform visualRoot;

    [Header("Settings")]
    [SerializeField] private bool alignVisualToSeatPoint = true;

    private TukTukSeat _currentTukTuk;
    private NPCBackSeat _currentBackSeat;
    private NavMeshAgent _agent;
    private bool _isMounted;
    private bool _isWalkingToSeat;

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

        _agent = GetComponent<NavMeshAgent>();

        CacheOriginalVisualState();
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
        // only process while walking to a seat, not after already mounted
        if (!_isWalkingToSeat)
            return;

        if (movement == null)
            return;

        if (movement.HasReachedDestination())
        {
            Debug.Log($"[NPCVehicleMount] {name} | Arrived at seat point. Mounting seat '{_currentBackSeat?.SeatName}'...", this);
            MountSeat();
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

        // If someone else occupied the seat while we were walking
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

        // prevent double mount
        _isWalkingToSeat = false;
        _isMounted = true;

        if (movement != null)
            movement.Stop();

        if (_agent != null)
        {
            _agent.enabled = false;
            Debug.Log($"[NPCVehicleMount] {name} | NavMeshAgent disabled.", this);
        }

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

        if (brain != null)
            brain.OnSeated();
    }

    public void ExitVehicle()
    {
        if (!_isMounted || _currentBackSeat == null)
            return;

        string seatName = _currentBackSeat.SeatName;
        Debug.Log($"[NPCVehicleMount] {name} | Exiting seat '{seatName}'...", this);

        NPCBackSeat previousSeat = _currentBackSeat;

        RestoreVisualRoot();

        Transform exitTarget = previousSeat.ExitPoint;
        if (exitTarget == null && _currentTukTuk != null)
            exitTarget = _currentTukTuk.ExitPoint;
        if (exitTarget == null && _currentTukTuk != null)
            exitTarget = _currentTukTuk.transform;

        transform.position = exitTarget.position;
        transform.rotation = Quaternion.Euler(0f, exitTarget.eulerAngles.y, 0f);

        if (_agent != null)
        {
            _agent.enabled = true;
            Debug.Log($"[NPCVehicleMount] {name} | NavMeshAgent re-enabled.", this);
        }

        previousSeat.OnNPCExited(this);

        _currentBackSeat = null;
        _currentTukTuk = null;
        _isMounted = false;
        _isWalkingToSeat = false;

        Debug.Log($"[NPCVehicleMount] {name} | EXITED seat '{seatName}' | Seat IsOccupied={previousSeat.IsOccupied}", this);

        if (brain != null)
            brain.OnExitedVehicle();
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