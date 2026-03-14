using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public class PlayerVehicleMount : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerInputReader inputReader;
    [SerializeField] private FirstPersonPlayerController firstPersonPlayerController;
    [SerializeField] private CharacterController characterController;

    [Header("Player Visual")]
    [SerializeField] private Transform visualRoot;

    [Header("Settings")]
    [SerializeField] private bool disableLookWhileDriving = false;
    [SerializeField] private bool parentPlayerToVehicle = true;
    [SerializeField] private bool alignVisualRootToVisualSeatPoint = true;
    [SerializeField] private bool alignYawToSeatOnEnter = true;

    private TukTukSeat _currentSeat;

    private Transform _originalRootParent;

    private Transform _originalVisualParent;
    private Vector3 _originalVisualLocalPosition;
    private Quaternion _originalVisualLocalRotation;
    private Vector3 _originalVisualLocalScale;

    public bool IsInVehicle => _currentSeat != null;
    public TukTukSeat CurrentSeat => _currentSeat;

    private void Awake()
    {
        if (inputReader == null)
            inputReader = GetComponent<PlayerInputReader>();

        if (firstPersonPlayerController == null)
            firstPersonPlayerController = GetComponent<FirstPersonPlayerController>();

        if (characterController == null)
            characterController = GetComponent<CharacterController>();

        _originalRootParent = transform.parent;
        CacheOriginalVisualState();
    }

    private void Update()
    {
        if (!IsInVehicle)
            return;

        if (inputReader != null && inputReader.ExitVehiclePressed)
            ExitVehicle();
    }

    public void EnterVehicle(TukTukSeat seat)
    {
        if (seat == null || IsInVehicle || seat.IsOccupied)
            return;

        _currentSeat = seat;

        _originalRootParent = transform.parent;
        CacheOriginalVisualState();

        if (characterController != null)
            characterController.enabled = false;

        if (parentPlayerToVehicle)
        {
            Transform targetParent = seat.MountParent;
            transform.SetParent(targetParent, true);
        }

        if (seat.SeatPoint != null)
        {
            transform.position = seat.SeatPoint.position;

            if (alignYawToSeatOnEnter)
            {
                float targetYaw = seat.SeatPoint.eulerAngles.y;
                transform.rotation = Quaternion.Euler(0f, targetYaw, 0f);
            }
        }

        if (firstPersonPlayerController != null)
        {
            firstPersonPlayerController.ForceSetPositionAndRotation(transform.position, transform.rotation);
            firstPersonPlayerController.SetMovementEnabled(false);
            firstPersonPlayerController.SetLookEnabled(!disableLookWhileDriving);
        }

        ApplyVisualSeatAlignment(seat);

        if (inputReader != null)
            inputReader.SwitchToVehicleMap();

        seat.OnPlayerEntered(this);
    }

    public void ExitVehicle()
    {
        if (_currentSeat == null)
            return;

        TukTukSeat previousSeat = _currentSeat;

        RestoreVisualRoot();

        transform.SetParent(_originalRootParent, true);

        Transform exitPoint = previousSeat.ExitPoint != null ? previousSeat.ExitPoint : previousSeat.transform;
        transform.position = exitPoint.position;
        transform.rotation = Quaternion.Euler(0f, exitPoint.eulerAngles.y, 0f);

        if (characterController != null)
            characterController.enabled = true;

        if (firstPersonPlayerController != null)
        {
            firstPersonPlayerController.ForceSetPositionAndRotation(transform.position, transform.rotation);
            firstPersonPlayerController.SetMovementEnabled(true);
            firstPersonPlayerController.SetLookEnabled(true);
        }

        if (inputReader != null)
            inputReader.SwitchToPlayerMap();

        previousSeat.OnPlayerExited(this);
        _currentSeat = null;
    }

    private void ApplyVisualSeatAlignment(TukTukSeat seat)
    {
        if (visualRoot == null)
            return;

        if (!alignVisualRootToVisualSeatPoint)
            return;

        if (seat.VisualSeatPoint == null)
            return;

        visualRoot.SetParent(seat.VisualSeatPoint, false);
        visualRoot.localPosition = Vector3.zero;
        visualRoot.localRotation = Quaternion.identity;
        visualRoot.localScale = Vector3.one;
    }

    private void RestoreVisualRoot()
    {
        if (visualRoot == null)
            return;

        visualRoot.SetParent(_originalVisualParent, false);
        visualRoot.localPosition = _originalVisualLocalPosition;
        visualRoot.localRotation = _originalVisualLocalRotation;
        visualRoot.localScale = _originalVisualLocalScale;
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