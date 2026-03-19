using UnityEngine;

/// <summary>
/// Attach to the Tuk Tuk. Detects when the vehicle enters a DropZone trigger
/// and tells DropManager to handle the drop.
/// </summary>
[DisallowMultipleComponent]
public class DropZoneDetector : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DropManager dropManager;

    [Header("Settings")]
    [SerializeField] private LayerMask dropZoneLayer;

    [Header("Debug")]
    [SerializeField] private bool logDetection = false;

    private void Awake()
    {
        if (dropManager == null)
            dropManager = GetComponentInParent<DropManager>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null)
            return;

        // Check layer
        if (((1 << other.gameObject.layer) & dropZoneLayer) == 0)
            return;

        DropZone dropZone = other.GetComponent<DropZone>();
        if (dropZone == null)
            dropZone = other.GetComponentInParent<DropZone>();

        if (dropZone == null)
        {
            if (logDetection)
                Debug.Log($"[DropZoneDetector] Entered trigger on '{other.name}' but no DropZone component found.", this);
            return;
        }

        if (!dropZone.IsActive)
        {
            if (logDetection)
                Debug.Log($"[DropZoneDetector] Entered DropZone '{dropZone.ZoneName}' but it is NOT active.", this);
            return;
        }

        if (logDetection)
            Debug.Log($"[DropZoneDetector] Entered active DropZone '{dropZone.ZoneName}'!", this);

        if (dropManager != null)
            dropManager.HandleDrop(dropZone);
        else
            Debug.LogError("[DropZoneDetector] DropManager reference is NULL!", this);
    }
}