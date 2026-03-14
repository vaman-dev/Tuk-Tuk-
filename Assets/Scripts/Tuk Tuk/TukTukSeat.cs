using UnityEngine;

[DisallowMultipleComponent]
public class TukTukSeat : MonoBehaviour, IInteractable
{
    [Header("Seat References")]
    [SerializeField] private Transform seatPoint;
    [SerializeField] private Transform exitPoint;
    [SerializeField] private Transform visualSeatPoint;
    [SerializeField] private Transform mountParent;

    [Header("Vehicle SCC References")]
    [SerializeField] private SCCPlayerInputBridge sccPlayerInputBridge;

    [Header("Interaction")]
    [SerializeField] private string interactionPrompt = "Press E to Enter Tuk Tuk";

    private PlayerVehicleMount _currentMount;
    private bool _isOccupied;

    public Transform SeatPoint => seatPoint;
    public Transform ExitPoint => exitPoint;
    public Transform VisualSeatPoint => visualSeatPoint;
    public Transform MountParent => mountParent != null ? mountParent : transform;
    public SCCPlayerInputBridge SCCPlayerInputBridge => sccPlayerInputBridge;
    public bool IsOccupied => _isOccupied;
    public PlayerVehicleMount CurrentMount => _currentMount;

    public void Interact(PlayerInteractor interactor)
    {
        if (_isOccupied || interactor == null)
            return;

        PlayerVehicleMount mount = interactor.GetComponent<PlayerVehicleMount>();

        if (mount == null)
        {
            Debug.LogWarning("[TukTukSeat] No PlayerVehicleMount found on interacting player.", this);
            return;
        }

        mount.EnterVehicle(this);
    }

    public string GetInteractionPrompt()
    {
        return _isOccupied ? string.Empty : interactionPrompt;
    }

    public Transform GetInteractionTransform()
    {
        return seatPoint != null ? seatPoint : transform;
    }

    public void OnPlayerEntered(PlayerVehicleMount mount)
    {
        _currentMount = mount;
        _isOccupied = true;

        if (sccPlayerInputBridge != null && mount != null)
            sccPlayerInputBridge.SetDriver(mount.InputReader);
    }

    public void OnPlayerExited(PlayerVehicleMount mount)
    {
        if (_currentMount == mount)
            _currentMount = null;

        _isOccupied = false;

        if (sccPlayerInputBridge != null)
            sccPlayerInputBridge.ClearDriver();
    }

    private void OnDrawGizmosSelected()
    {
        if (seatPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(seatPoint.position, 0.15f);
            Gizmos.DrawLine(seatPoint.position, seatPoint.position + seatPoint.forward * 0.5f);
        }

        if (visualSeatPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(visualSeatPoint.position, 0.12f);
            Gizmos.DrawLine(visualSeatPoint.position, visualSeatPoint.position + visualSeatPoint.forward * 0.4f);
        }

        if (exitPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(exitPoint.position, 0.15f);
            Gizmos.DrawLine(exitPoint.position, exitPoint.position + exitPoint.forward * 0.5f);
        }

        if (mountParent != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireCube(mountParent.position, Vector3.one * 0.2f);
        }
    }
}