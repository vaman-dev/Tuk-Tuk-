using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Place this on empty GameObjects in the world to mark NPC drop-off locations.
/// Requires a trigger Collider (e.g. BoxCollider or SphereCollider with IsTrigger = true).
/// </summary>
[DisallowMultipleComponent]
public class DropZone : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private string zoneName = "Drop Zone";

    [Header("NPC Exit Points")]
    [SerializeField] private List<Transform> npcExitPoints = new List<Transform>();

    [Header("Visual")]
    [SerializeField] private GameObject activeVisual;
    [SerializeField] private GameObject inactiveVisual;

    [Header("Settings")]
    [SerializeField] private float cooldownAfterDrop = 10f;

    [Header("Debug")]
    [SerializeField] private bool logEvents = false;

    private bool _isActive;
    private float _cooldownTimer;
    private int _nextExitPointIndex;

    public string ZoneName => zoneName;
    public bool IsActive => _isActive;
    public Vector3 Position => transform.position;
    public int ExitPointCount => npcExitPoints != null ? npcExitPoints.Count : 0;

    private void Start()
    {
        Deactivate();
    }

    private void Update()
    {
        if (!_isActive && _cooldownTimer > 0f)
        {
            _cooldownTimer -= Time.deltaTime;
        }
    }

    /// <summary>
    /// Returns the next available exit point in round-robin order.
    /// Each call cycles to the next exit point so multiple NPCs don't overlap.
    /// </summary>
    public Transform GetNextExitPoint()
    {
        if (npcExitPoints == null || npcExitPoints.Count == 0)
            return transform;

        Transform exitPoint = npcExitPoints[_nextExitPointIndex];
        _nextExitPointIndex = (_nextExitPointIndex + 1) % npcExitPoints.Count;

        return exitPoint != null ? exitPoint : transform;
    }

    /// <summary>
    /// Returns the exit point closest to the given position.
    /// </summary>
    public Transform GetClosestExitPoint(Vector3 fromPosition)
    {
        if (npcExitPoints == null || npcExitPoints.Count == 0)
            return transform;

        Transform closest = null;
        float closestDist = float.MaxValue;

        for (int i = 0; i < npcExitPoints.Count; i++)
        {
            if (npcExitPoints[i] == null)
                continue;

            float dist = Vector3.Distance(fromPosition, npcExitPoints[i].position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = npcExitPoints[i];
            }
        }

        return closest != null ? closest : transform;
    }

    /// <summary>
    /// Returns the exit point at the given index, or the transform itself if invalid.
    /// </summary>
    public Transform GetExitPoint(int index)
    {
        if (npcExitPoints == null || index < 0 || index >= npcExitPoints.Count)
            return transform;

        return npcExitPoints[index] != null ? npcExitPoints[index] : transform;
    }

    /// <summary>
    /// Resets the round-robin index back to the first exit point.
    /// </summary>
    public void ResetExitPointIndex()
    {
        _nextExitPointIndex = 0;
    }

    /// <summary>
    /// Activates this drop zone so the player can drop NPCs here.
    /// </summary>
    public void Activate()
    {
        if (_isActive)
            return;

        if (_cooldownTimer > 0f)
        {
            if (logEvents)
                Debug.Log($"[DropZone] '{zoneName}' | Cannot activate, cooldown remaining: {_cooldownTimer:F1}s", this);
            return;
        }

        _isActive = true;
        _nextExitPointIndex = 0;

        if (activeVisual != null)
            activeVisual.SetActive(true);
        if (inactiveVisual != null)
            inactiveVisual.SetActive(false);

        if (logEvents)
            Debug.Log($"[DropZone] '{zoneName}' | ACTIVATED | exitPoints={ExitPointCount}", this);
    }

    /// <summary>
    /// Deactivates this drop zone after a successful drop.
    /// </summary>
    public void Deactivate()
    {
        _isActive = false;

        if (activeVisual != null)
            activeVisual.SetActive(false);
        if (inactiveVisual != null)
            inactiveVisual.SetActive(true);

        if (logEvents)
            Debug.Log($"[DropZone] '{zoneName}' | DEACTIVATED", this);
    }

    /// <summary>
    /// Called after a successful drop. Starts cooldown before zone can be reactivated.
    /// </summary>
    public void OnDropCompleted()
    {
        Deactivate();
        _cooldownTimer = cooldownAfterDrop;

        if (logEvents)
            Debug.Log($"[DropZone] '{zoneName}' | Drop completed. Cooldown={cooldownAfterDrop:F1}s", this);
    }

    /// <summary>
    /// Returns true if this zone is off cooldown and can be activated.
    /// </summary>
    public bool CanBeActivated()
    {
        return !_isActive && _cooldownTimer <= 0f;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = _isActive ? Color.green : Color.gray;
        Gizmos.DrawWireSphere(transform.position, 1f);

        if (npcExitPoints != null)
        {
            for (int i = 0; i < npcExitPoints.Count; i++)
            {
                if (npcExitPoints[i] == null)
                    continue;

                Gizmos.color = (i == 0) ? Color.cyan : Color.blue;
                Gizmos.DrawWireSphere(npcExitPoints[i].position, 0.3f);
                Gizmos.DrawLine(transform.position, npcExitPoints[i].position);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = _isActive ? new Color(0f, 1f, 0f, 0.2f) : new Color(0.5f, 0.5f, 0.5f, 0.2f);
        Gizmos.DrawSphere(transform.position, 1f);
    }
}