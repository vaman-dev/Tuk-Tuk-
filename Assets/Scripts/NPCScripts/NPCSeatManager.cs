using UnityEngine;

[DisallowMultipleComponent]
public class NPCSeatManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TukTukSeat tukTukSeat;

    public bool TryAssignSeat(NPCVehicleMount npcMount)
    {
        if (tukTukSeat == null)
        {
            Debug.LogError($"[NPCSeatManager] {name} | tukTukSeat is NULL! Assign it in the Inspector.", this);
            return false;
        }

        if (npcMount == null)
        {
            Debug.LogError($"[NPCSeatManager] {name} | npcMount is NULL!", this);
            return false;
        }

        if (npcMount.IsInVehicle)
        {
            Debug.Log($"[NPCSeatManager] {name} | NPC '{npcMount.name}' is already in a vehicle.", this);
            return false;
        }

        if (tukTukSeat.AreAllNPCSeatsOccupied)
        {
            Debug.Log($"[NPCSeatManager] {name} | All {tukTukSeat.NPCBackSeatCount} NPC back seats are occupied or reserved.", this);
            return false;
        }

        NPCBackSeat availableSeat = tukTukSeat.GetClosestAvailableNPCBackSeat(npcMount.transform.position);

        if (availableSeat == null)
        {
            Debug.LogWarning($"[NPCSeatManager] {name} | GetClosestAvailableNPCBackSeat returned null even though not all seats are occupied!", this);
            return false;
        }

        // Reserve the seat immediately so no other NPC can claim it
        if (!availableSeat.TryReserve(npcMount))
        {
            Debug.Log($"[NPCSeatManager] {name} | Failed to reserve seat '{availableSeat.SeatName}' for NPC '{npcMount.name}'.", this);
            return false;
        }

        Debug.Log($"[NPCSeatManager] {name} | Reserved & assigning NPC '{npcMount.name}' -> seat '{availableSeat.SeatName}' | " +
                  $"SeatPoint={(availableSeat.SeatPoint != null ? availableSeat.SeatPoint.name : "NULL")} | " +
                  $"ExitPoint={(availableSeat.ExitPoint != null ? availableSeat.ExitPoint.name : "NULL")} | " +
                  $"VisualSeatPoint={(availableSeat.VisualSeatPoint != null ? availableSeat.VisualSeatPoint.name : "NULL")}", this);

        npcMount.WalkToAndEnter(tukTukSeat, availableSeat);
        return true;
    }

    public bool HasAvailableSeat()
    {
        if (tukTukSeat == null)
            return false;

        return !tukTukSeat.AreAllNPCSeatsOccupied;
    }

    public NPCBackSeat GetAvailableSeatClosestTo(Vector3 position)
    {
        if (tukTukSeat == null)
            return null;

        return tukTukSeat.GetClosestAvailableNPCBackSeat(position);
    }
}