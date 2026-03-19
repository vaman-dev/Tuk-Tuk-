using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages NPC drop-off logic on the Tuk Tuk.
/// Assigns a random drop zone when passengers board, ejects them on arrival.
/// </summary>
[DisallowMultipleComponent]
public class DropManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TukTukSeat tukTukSeat;

    [Header("Drop Zones")]
    [Tooltip("Assign all DropZone objects in the map here.")]
    [SerializeField] private List<DropZone> allDropZones = new List<DropZone>();

    [Header("Settings")]
    [SerializeField] private bool autoAssignOnPickup = true;
    [SerializeField] private float minDropZoneDistance = 20f;

    [Header("Debug")]
    [SerializeField] private bool logDropEvents = false;

    private DropZone _currentDropZone;
    private bool _hasPassengers;

    // ── Public Accessors ──
    public DropZone CurrentDropZone => _currentDropZone;
    public bool HasActiveDropZone => _currentDropZone != null && _currentDropZone.IsActive;
    public bool HasPassengers => _hasPassengers;

    /// <summary>
    /// Fired when a drop zone is assigned. Passes the DropZone.
    /// Use this for destination marker UI.
    /// </summary>
    public event Action<DropZone> OnDropZoneAssigned;

    /// <summary>
    /// Fired when passengers are dropped off. Passes (DropZone, number of NPCs dropped).
    /// Use this for scoring, UI, etc.
    /// </summary>
    public event Action<DropZone, int> OnPassengersDropped;

    /// <summary>
    /// Fired when the drop zone is cleared (after drop or when no passengers remain).
    /// </summary>
    public event Action OnDropZoneCleared;

    private void Update()
    {
        if (tukTukSeat == null)
            return;

        bool currentlyHasPassengers = HasSeatedNPCs();

        // Detect when passengers first board
        if (currentlyHasPassengers && !_hasPassengers)
        {
            _hasPassengers = true;

            if (autoAssignOnPickup && _currentDropZone == null)
                AssignRandomDropZone();
        }

        // Detect when all passengers have left (e.g. dropped or exited)
        if (!currentlyHasPassengers && _hasPassengers)
        {
            _hasPassengers = false;
            ClearDropZone();
        }
    }

    /// <summary>
    /// Called by DropZoneDetector when the Tuk Tuk enters an active drop zone.
    /// </summary>
    public void HandleDrop(DropZone dropZone)
    {
        if (dropZone == null)
            return;

        if (tukTukSeat == null)
        {
            Debug.LogError("[DropManager] tukTukSeat is NULL!", this);
            return;
        }

        // Only drop at the assigned drop zone
        if (_currentDropZone != null && _currentDropZone != dropZone)
        {
            if (logDropEvents)
                Debug.Log($"[DropManager] Entered DropZone '{dropZone.ZoneName}' but current target is '{_currentDropZone.ZoneName}'. Ignoring.", this);
            return;
        }

        int droppedCount = EjectAllPassengers(dropZone);

        if (droppedCount > 0)
        {
            if (logDropEvents)
                Debug.Log($"[DropManager] Dropped {droppedCount} passenger(s) at '{dropZone.ZoneName}'!", this);

            dropZone.OnDropCompleted();

            OnPassengersDropped?.Invoke(dropZone, droppedCount);

            ClearDropZone();
        }
        else
        {
            if (logDropEvents)
                Debug.Log($"[DropManager] Arrived at DropZone '{dropZone.ZoneName}' but no passengers to drop.", this);
        }
    }

    /// <summary>
    /// Assigns a random available drop zone from the list.
    /// Prefers zones farther than minDropZoneDistance from the Tuk Tuk.
    /// </summary>
    public void AssignRandomDropZone()
    {
        if (allDropZones == null || allDropZones.Count == 0)
        {
            Debug.LogWarning("[DropManager] No drop zones assigned!", this);
            return;
        }

        // Collect valid candidates
        List<DropZone> candidates = new List<DropZone>();
        List<DropZone> fallbackCandidates = new List<DropZone>();

        for (int i = 0; i < allDropZones.Count; i++)
        {
            if (allDropZones[i] == null || !allDropZones[i].CanBeActivated())
                continue;

            float dist = Vector3.Distance(transform.position, allDropZones[i].Position);

            if (dist >= minDropZoneDistance)
                candidates.Add(allDropZones[i]);
            else
                fallbackCandidates.Add(allDropZones[i]);
        }

        // Use fallback if no zones meet distance requirement
        if (candidates.Count == 0)
            candidates = fallbackCandidates;

        if (candidates.Count == 0)
        {
            Debug.LogWarning("[DropManager] No available drop zones found!", this);
            return;
        }

        // Pick random
        DropZone chosen = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        SetDropZone(chosen);
    }

    /// <summary>
    /// Manually assign a specific drop zone.
    /// </summary>
    public void SetDropZone(DropZone dropZone)
    {
        if (dropZone == null)
            return;

        // Deactivate previous if different
        if (_currentDropZone != null && _currentDropZone != dropZone)
            _currentDropZone.Deactivate();

        _currentDropZone = dropZone;
        _currentDropZone.Activate();

        if (logDropEvents)
            Debug.Log($"[DropManager] Drop zone assigned: '{dropZone.ZoneName}' at {dropZone.Position}", this);

        OnDropZoneAssigned?.Invoke(dropZone);
    }

    /// <summary>
    /// Clears the current drop zone assignment.
    /// </summary>
    public void ClearDropZone()
    {
        if (_currentDropZone != null)
        {
            _currentDropZone.Deactivate();

            if (logDropEvents)
                Debug.Log($"[DropManager] Drop zone cleared: '{_currentDropZone.ZoneName}'", this);

            _currentDropZone = null;
        }

        OnDropZoneCleared?.Invoke();
    }

    private int EjectAllPassengers(DropZone dropZone)
    {
        int count = 0;
        List<NPCBackSeat> seats = tukTukSeat.NPCBackSeats;

        if (seats == null)
            return 0;

        for (int i = 0; i < seats.Count; i++)
        {
            if (!seats[i].IsOccupied)
                continue;

            NPCVehicleMount npcMount = seats[i].CurrentNPC;
            if (npcMount == null)
                continue;

            if (logDropEvents)
                Debug.Log($"[DropManager] Ejecting NPC '{npcMount.name}' from seat '{seats[i].SeatName}'", this);

            npcMount.ExitVehicle();
            count++;
        }

        return count;
    }

    private bool HasSeatedNPCs()
    {
        if (tukTukSeat == null)
            return false;

        List<NPCBackSeat> seats = tukTukSeat.NPCBackSeats;
        if (seats == null)
            return false;

        for (int i = 0; i < seats.Count; i++)
        {
            if (seats[i].IsOccupied)
                return true;
        }

        return false;
    }

    private void OnDrawGizmosSelected()
    {
        if (_currentDropZone != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, _currentDropZone.Position);
            Gizmos.DrawWireSphere(_currentDropZone.Position, 2f);
        }

        // Show all drop zones
        if (allDropZones != null)
        {
            for (int i = 0; i < allDropZones.Count; i++)
            {
                if (allDropZones[i] == null)
                    continue;

                bool isCurrent = allDropZones[i] == _currentDropZone;
                Gizmos.color = isCurrent ? Color.green : Color.gray;
                Gizmos.DrawWireSphere(allDropZones[i].Position, 1f);
            }
        }
    }
}