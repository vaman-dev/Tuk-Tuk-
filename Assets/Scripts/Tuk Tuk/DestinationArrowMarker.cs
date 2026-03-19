using UnityEngine;

/// <summary>
/// 3D world-space arrow that hovers above the Tuk Tuk and rotates to
/// point toward the active drop zone. Uses a child model (arrow mesh).
/// Attach this as a child of the Tuk Tuk — it stays with the vehicle automatically.
/// </summary>
[DisallowMultipleComponent]
public class DestinationArrowMarker : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DropManager dropManager;

    [Header("Arrow Model")]
    [Tooltip("The arrow model GameObject (child of this transform). Gets shown/hidden.")]
    [SerializeField] private GameObject arrowModel;

    [Header("Position")]
    [Tooltip("Local offset from the parent Tuk Tuk. Adjust X, Y, Z to place the arrow exactly.")]
    [SerializeField] private Vector3 localOffset = new Vector3(-1.5f, 2.5f, 1f);
    [SerializeField] private float bobAmount = 0.2f;
    [SerializeField] private float bobSpeed = 2f;

    [Header("Rotation")]
    [SerializeField] private float rotationSmoothSpeed = 8f;
    [Tooltip("Only rotate on the Y axis (flat rotation). Keeps the arrow level.")]
    [SerializeField] private bool flatRotationOnly = true;

    [Header("Scale Pulse When Close")]
    [SerializeField] private bool pulseWhenClose = true;
    [SerializeField] private float pulseStartDistance = 20f;
    [SerializeField] private float pulseSpeed = 3f;
    [SerializeField] private float pulseMinScale = 0.8f;
    [SerializeField] private float pulseMaxScale = 1.2f;

    [Header("Color Change")]
    [SerializeField] private bool changeColorByDistance = true;
    [SerializeField] private float colorChangeMaxDistance = 50f;
    [SerializeField] private Color farColor = Color.yellow;
    [SerializeField] private Color closeColor = Color.green;

    [Header("Debug")]
    [SerializeField] private bool logEvents = false;

    private DropZone _targetDropZone;
    private bool _isSubscribed;
    private bool _isVisible;
    private Renderer _arrowRenderer;
    private MaterialPropertyBlock _propBlock;

    private void Awake()
    {
        if (arrowModel != null)
            _arrowRenderer = arrowModel.GetComponentInChildren<Renderer>();

        _propBlock = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void OnDisable()
    {
        if (dropManager != null)
        {
            dropManager.OnDropZoneAssigned -= HandleDropZoneAssigned;
            dropManager.OnDropZoneCleared -= HandleDropZoneCleared;
            dropManager.OnPassengersDropped -= HandlePassengersDropped;
            _isSubscribed = false;
        }
    }

    private void Start()
    {
        TrySubscribe();
        HideArrow();
    }

    private void Update()
    {
        if (!_isSubscribed)
            TrySubscribe();

        if (!_isVisible || _targetDropZone == null)
            return;

        UpdatePosition();
        UpdateRotation();
        UpdatePulse();
        UpdateColor();
    }

    // ── Subscription ──

    private void TrySubscribe()
    {
        if (_isSubscribed)
            return;

        if (dropManager == null)
            return;

        dropManager.OnDropZoneAssigned -= HandleDropZoneAssigned;
        dropManager.OnDropZoneAssigned += HandleDropZoneAssigned;

        dropManager.OnDropZoneCleared -= HandleDropZoneCleared;
        dropManager.OnDropZoneCleared += HandleDropZoneCleared;

        dropManager.OnPassengersDropped -= HandlePassengersDropped;
        dropManager.OnPassengersDropped += HandlePassengersDropped;

        _isSubscribed = true;

        // Sync with current state
        if (dropManager.HasActiveDropZone && dropManager.CurrentDropZone != null)
            ShowArrow(dropManager.CurrentDropZone);
        else
            HideArrow();

        if (logEvents)
            Debug.Log("[DestinationArrowMarker] Subscribed to DropManager.", this);
    }

    // ── Event Handlers ──

    private void HandleDropZoneAssigned(DropZone dropZone)
    {
        if (logEvents)
            Debug.Log($"[DestinationArrowMarker] Drop zone assigned: '{dropZone.ZoneName}'", this);

        ShowArrow(dropZone);
    }

    private void HandleDropZoneCleared()
    {
        if (logEvents)
            Debug.Log("[DestinationArrowMarker] Drop zone cleared.", this);

        HideArrow();
    }

    private void HandlePassengersDropped(DropZone dropZone, int count)
    {
        if (logEvents)
            Debug.Log($"[DestinationArrowMarker] Passengers dropped: {count} at '{dropZone.ZoneName}'", this);

        HideArrow();
    }

    // ── Show / Hide ──

    private void ShowArrow(DropZone dropZone)
    {
        _targetDropZone = dropZone;
        _isVisible = true;

        if (arrowModel != null)
            arrowModel.SetActive(true);
    }

    private void HideArrow()
    {
        _targetDropZone = null;
        _isVisible = false;

        if (arrowModel != null)
            arrowModel.SetActive(false);
    }

    // ── Position (offset + bob) ──

    private void UpdatePosition()
    {
        float bob = Mathf.Sin(Time.time * bobSpeed) * bobAmount;
        transform.localPosition = localOffset + new Vector3(0f, bob, 0f);
    }

    // ── Rotation (point toward drop zone) ──

    private void UpdateRotation()
    {
        Vector3 targetPos = _targetDropZone.Position;
        Vector3 myPos = transform.position;

        Vector3 direction = targetPos - myPos;

        if (flatRotationOnly)
            direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSmoothSpeed);
    }

    // ── Pulse ──

    private void UpdatePulse()
    {
        if (!pulseWhenClose || arrowModel == null)
            return;

        float dist = Vector3.Distance(transform.position, _targetDropZone.Position);

        if (dist <= pulseStartDistance)
        {
            float pulse = Mathf.Lerp(pulseMinScale, pulseMaxScale,
                (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f);
            arrowModel.transform.localScale = Vector3.one * pulse;
        }
        else
        {
            arrowModel.transform.localScale = Vector3.one;
        }
    }

    // ── Color ──

    private void UpdateColor()
    {
        if (!changeColorByDistance || _arrowRenderer == null)
            return;

        float dist = Vector3.Distance(transform.position, _targetDropZone.Position);
        float t = Mathf.Clamp01(dist / colorChangeMaxDistance);
        Color currentColor = Color.Lerp(closeColor, farColor, t);

        _arrowRenderer.GetPropertyBlock(_propBlock);
        _propBlock.SetColor("_Color", currentColor);
        _arrowRenderer.SetPropertyBlock(_propBlock);
    }

    private void OnDrawGizmosSelected()
    {
        if (_targetDropZone != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, _targetDropZone.Position);
        }

        // Show offset position
        Gizmos.color = Color.cyan;
        Vector3 previewPos = transform.parent != null
            ? transform.parent.TransformPoint(localOffset)
            : transform.position + localOffset;
        Gizmos.DrawWireSphere(previewPos, 0.2f);
    }
}