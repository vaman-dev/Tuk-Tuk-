using UnityEngine;

[DisallowMultipleComponent]
public class PlayerInteractor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera interactionCamera;
    [SerializeField] private PlayerInputReader inputReader;

    [Header("Detection Settings")]
    [SerializeField] private float interactionDistance = 3f;
    [SerializeField] private float sphereRadius = 0.35f;
    [SerializeField] private LayerMask interactableLayerMask = ~0;
    [SerializeField] private QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.Collide;

    [Header("Debug")]
    [SerializeField] private bool drawDebugRay = true;

    private IInteractable _currentInteractable;
    private RaycastHit _lastHit;
    private bool _hasHit;

    public IInteractable CurrentInteractable => _currentInteractable;
    public bool HasInteractable => _currentInteractable != null;

    private void Awake()
    {
        if (interactionCamera == null)
            interactionCamera = GetComponentInChildren<Camera>();

        if (inputReader == null)
            inputReader = GetComponent<PlayerInputReader>();
    }

    private void Update()
    {
        CheckInteractable();

        if (inputReader != null && inputReader.InteractPressed)
        {
            Interact();
        }
    }

    public void CheckInteractable()
    {
        // if there is already an interactable, we can skip the check to save performance 
        _currentInteractable = null;
        _hasHit = false;

        if (interactionCamera == null)
            return;

        Transform camTransform = interactionCamera.transform;
        Vector3 origin = camTransform.position;
        Vector3 direction = camTransform.forward;

        bool hitSomething = Physics.SphereCast(
            origin,
            sphereRadius,
            direction,
            out _lastHit,
            interactionDistance,
            interactableLayerMask,
            queryTriggerInteraction
        );

        if (!hitSomething)
            return;

        _hasHit = true;

        IInteractable interactable = _lastHit.collider.GetComponent<IInteractable>();

        if (interactable == null)
            interactable = _lastHit.collider.GetComponentInParent<IInteractable>();

        if (interactable == null)
            interactable = _lastHit.collider.GetComponentInChildren<IInteractable>();

        _currentInteractable = interactable;
    }

    public void Interact()
    {
        if (_currentInteractable == null)
            return;

        _currentInteractable.Interact(this);
    }

    public string GetCurrentPrompt()
    {
        if (_currentInteractable == null)
            return string.Empty;

        return _currentInteractable.GetInteractionPrompt();
    }

    public Transform GetCurrentInteractableTransform()
    {
        if (_currentInteractable == null)
            return null;

        return _currentInteractable.GetInteractionTransform();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebugRay)
            return;

        Camera cam = interactionCamera != null ? interactionCamera : GetComponentInChildren<Camera>();
        if (cam == null)
            return;

        Vector3 origin = cam.transform.position;
        Vector3 direction = cam.transform.forward;
        Vector3 endPoint = origin + direction * interactionDistance;
        // for the gizmos, we can use a different color to indicate if there is an interactable or not
        Gizmos.color = HasInteractable ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(origin, sphereRadius);
        Gizmos.DrawWireSphere(endPoint, sphereRadius);
        Gizmos.DrawLine(origin, endPoint);

        if (_hasHit)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(_lastHit.point, 0.08f);
        }
    }
}