using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class TukTukSeat : MonoBehaviour, IInteractable
{
    [Header("Player Seat References")]
    [SerializeField] private Transform seatPoint;
    [SerializeField] private Transform exitPoint;
    [SerializeField] private Transform visualSeatPoint;
    [SerializeField] private Transform mountParent;

    [Header("NPC Back Seats")]
    [SerializeField] private List<NPCBackSeat> npcBackSeats = new List<NPCBackSeat>();

    [Header("Vehicle SCC References")]
    [SerializeField] private SCCPlayerInputBridge sccPlayerInputBridge;

    [Header("Interaction")]
    [SerializeField] private string interactionPrompt = "Press E to Enter Tuk Tuk";

    private PlayerVehicleMount _currentPlayerMount;
    private bool _isPlayerSeatOccupied;

    // ── Player seat ──
    public Transform SeatPoint => seatPoint;
    public Transform ExitPoint => exitPoint;
    public Transform VisualSeatPoint => visualSeatPoint;
    public Transform MountParent => mountParent != null ? mountParent : transform;

    // ── NPC back seats ──
    public List<NPCBackSeat> NPCBackSeats => npcBackSeats;
    public int NPCBackSeatCount => npcBackSeats != null ? npcBackSeats.Count : 0;

    // ── Shared ──
    public SCCPlayerInputBridge SCCPlayerInputBridge => sccPlayerInputBridge;
    public bool IsOccupied => _isPlayerSeatOccupied;
    public PlayerVehicleMount CurrentMount => _currentPlayerMount;
    public bool IsOccupiedByPlayer => _currentPlayerMount != null;

    /// <summary>
    /// Returns true if ALL NPC back seats are occupied or reserved.
    /// </summary>
    public bool AreAllNPCSeatsOccupied
    {
        get
        {
            if (npcBackSeats == null || npcBackSeats.Count == 0)
                return true;

            for (int i = 0; i < npcBackSeats.Count; i++)
            {
                if (!npcBackSeats[i].IsOccupied && !npcBackSeats[i].IsReserved)
                    return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Returns the first available NPC back seat, or null if all are occupied.
    /// </summary>
    public NPCBackSeat GetAvailableNPCBackSeat()
    {
        if (npcBackSeats == null)
            return null;

        for (int i = 0; i < npcBackSeats.Count; i++)
        {
            if (!npcBackSeats[i].IsOccupied && !npcBackSeats[i].IsReserved)
                return npcBackSeats[i];
        }

        return null;
    }

    /// <summary>
    /// Returns the available NPC back seat closest to the given position, or null.
    /// </summary>
    public NPCBackSeat GetClosestAvailableNPCBackSeat(Vector3 fromPosition)
    {
        NPCBackSeat closest = null;
        float closestDist = float.MaxValue;

        if (npcBackSeats == null)
            return null;

        for (int i = 0; i < npcBackSeats.Count; i++)
        {
            if (npcBackSeats[i].IsOccupied || npcBackSeats[i].IsReserved)
                continue;

            if (npcBackSeats[i].SeatPoint == null)
                continue;

            float dist = Vector3.Distance(fromPosition, npcBackSeats[i].SeatPoint.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = npcBackSeats[i];
            }
        }

        return closest;
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (_isPlayerSeatOccupied || interactor == null)
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
        return _isPlayerSeatOccupied ? string.Empty : interactionPrompt;
    }

    public Transform GetInteractionTransform()
    {
        return seatPoint != null ? seatPoint : transform;
    }

    // ── Player ──

    public void OnPlayerEntered(PlayerVehicleMount mount)
    {
        _currentPlayerMount = mount;
        _isPlayerSeatOccupied = true;

        if (sccPlayerInputBridge != null && mount != null)
            sccPlayerInputBridge.SetDriver(mount.InputReader);
    }

    public void OnPlayerExited(PlayerVehicleMount mount)
    {
        if (_currentPlayerMount == mount)
            _currentPlayerMount = null;

        _isPlayerSeatOccupied = false;

        if (sccPlayerInputBridge != null)
            sccPlayerInputBridge.ClearDriver();
    }

    private void OnDrawGizmosSelected()
    {
        // Player seat gizmos
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

        // NPC back seat gizmos
        if (npcBackSeats != null)
        {
            for (int i = 0; i < npcBackSeats.Count; i++)
            {
                NPCBackSeat bs = npcBackSeats[i];
                Color seatColor = bs.IsOccupied ? Color.red : (bs.IsReserved ? Color.yellow : Color.blue);

                if (bs.SeatPoint != null)
                {
                    Gizmos.color = seatColor;
                    Gizmos.DrawWireSphere(bs.SeatPoint.position, 0.15f);
                    Gizmos.DrawLine(bs.SeatPoint.position, bs.SeatPoint.position + bs.SeatPoint.forward * 0.5f);
                }

                if (bs.VisualSeatPoint != null)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(bs.VisualSeatPoint.position, 0.12f);
                }

                if (bs.ExitPoint != null)
                {
                    Gizmos.color = Color.white;
                    Gizmos.DrawWireSphere(bs.ExitPoint.position, 0.15f);
                }
            }
        }
    }
}

[System.Serializable]
public class NPCBackSeat
{
    [SerializeField] private string seatName;
    [SerializeField] private Transform seatPoint;
    [SerializeField] private Transform exitPoint;
    [SerializeField] private Transform visualSeatPoint;

    [System.NonSerialized] private NPCVehicleMount _currentNPC;
    [System.NonSerialized] private bool _isOccupied;
    [System.NonSerialized] private NPCVehicleMount _reservedBy;

    public string SeatName => seatName;
    public Transform SeatPoint => seatPoint;
    public Transform ExitPoint => exitPoint;
    public Transform VisualSeatPoint => visualSeatPoint;
    public bool IsOccupied => _isOccupied;
    public bool IsReserved => _reservedBy != null;
    public NPCVehicleMount CurrentNPC => _currentNPC;

    public bool TryReserve(NPCVehicleMount mount)
    {
        if (_isOccupied || _reservedBy != null)
            return false;

        _reservedBy = mount;
        return true;
    }

    public void ClearReservation(NPCVehicleMount mount)
    {
        if (_reservedBy == mount)
            _reservedBy = null;
    }

    public void OnNPCEntered(NPCVehicleMount mount)
    {
        _currentNPC = mount;
        _isOccupied = true;

        // Clear reservation since NPC is now seated
        if (_reservedBy == mount)
            _reservedBy = null;
    }

    public void OnNPCExited(NPCVehicleMount mount)
    {
        if (_currentNPC == mount)
        {
            _currentNPC = null;
            _isOccupied = false;
        }
    }
}