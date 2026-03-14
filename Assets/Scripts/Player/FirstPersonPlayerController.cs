using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[AddComponentMenu("Crazy Tuk Tuk Driver/Player/First Person Player Controller")]
public class FirstPersonPlayerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Transform cameraRoot;
    [SerializeField] private PlayerInputReader inputReader;

    [Header("Movement Settings")]
    [Tooltip("FOR THE MOVEMENT OF THE PLAYER ")]    
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float runSpeed = 9f;
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float gravity = -25f;
    [SerializeField] private float lookSensitivity = 2f;

    [Header("Camera Look Limits")]
    [SerializeField] private float maxLookUpAngle = 90f;
    [SerializeField] private float maxLookDownAngle = -90f;

    [Header("Head Bob")]
    [SerializeField] private bool enableHeadBob = true;
    [SerializeField, Range(0.01f, 0.15f)] private float bobAmountX = 0.04f;
    [SerializeField, Range(0.01f, 0.15f)] private float bobAmountY = 0.05f;
    [SerializeField] private float walkBobFrequency = 12f;
    [SerializeField] private float runBobFrequency = 16f;
    [SerializeField] private float crouchBobFrequency = 8f;
    [SerializeField] private float bobSmoothness = 10f;

    [Header("Camera Inertia")]
    [SerializeField, Range(1f, 30f)] private float cameraWeight = 12f;

    [Header("Camera Tilt")]
    [SerializeField] private bool enableCameraTilt = true;
    [SerializeField] private float tiltAmount = 2f;
    [SerializeField] private float tiltSmoothness = 8f;
    [SerializeField] private float runTiltMultiplier = 1.2f;
    [SerializeField] private float crouchTiltMultiplier = 0.5f;
    [SerializeField] private float turnTiltAmount = 1.5f;
    [SerializeField] private float maxTotalTilt = 5f;

    [Header("Crouch Settings")]
    [SerializeField] private float crouchHeight = 1.2f;
    [SerializeField] private float crouchSmoothTime = 0.1f;

    [Header("FOV Settings")]
    [SerializeField] private bool enableRunFov = true;
    [SerializeField] private float normalFov = 60f;
    [SerializeField] private float runFov = 70f;
    [SerializeField] private float fovChangeSpeed = 8f;

    [Header("Standing Detection & Ground Check")]
    [SerializeField] private GameObject standingHeightMarker;
    [SerializeField] private float standingCheckRadius = 0.2f;
    [SerializeField] private LayerMask obstacleLayerMask = ~0;
    [SerializeField] private float minStandingClearance = 0.01f;
    [SerializeField] private LayerMask groundLayer = 1;
    [SerializeField] private float groundCheckDistance = 0.5f;

    [Header("State")]
    [SerializeField] private bool movementEnabled = true;
    [SerializeField] private bool lookEnabled = true;

    private Vector3 _velocity;
    private float _currentTilt;
    private float _headBobTimer;
    private float _originalHeight;
    private float _targetHeight;
    private float _currentMovementSpeed;
    private float _cameraBaseHeight;
    private float _markerHeightOffset;

    private float _targetYaw;
    private float _targetPitch;
    private float _currentYaw;
    private float _currentPitch;
    private float _smoothInputX;

    private bool _isGrounded;
    private bool _isCrouching;
    private bool _hasJumped;

    public enum MovementState
    {
        Walking,
        Running,
        Crouching,
        Jumping
    }

    private MovementState _currentMovementState = MovementState.Walking;

    public bool IsGrounded => _isGrounded;
    public bool IsCrouching => _isCrouching;
    public bool MovementEnabled => movementEnabled;
    public bool LookEnabled => lookEnabled;
    public MovementState CurrentState => _currentMovementState;

    private void Start()
    {
        // intialize the character controller 
        if (characterController == null)
            characterController = GetComponent<CharacterController>();

        if (inputReader == null)
            inputReader = GetComponent<PlayerInputReader>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        _originalHeight = characterController.height;
        _targetHeight = _originalHeight;

        if (cameraRoot != null)
        {
            _cameraBaseHeight = cameraRoot.localPosition.y;
            _targetYaw = transform.eulerAngles.y;
            _targetPitch = NormalizePitch(cameraRoot.localEulerAngles.x);
            _currentYaw = _targetYaw;
            _currentPitch = _targetPitch;
        }

        if (standingHeightMarker != null)
            _markerHeightOffset = standingHeightMarker.transform.position.y - transform.position.y;
    }

    private void Update()
    {
        if (characterController == null)
            return;

        if (!characterController.enabled)
        {
            HandleCameraControl();

            if (enableCameraTilt)
                HandleCameraTilt();

            if (enableRunFov)
                HandleFovChange();

            return;
        }

        CheckGroundStatus();
        HandleCrouchLogic();
        UpdateMovementState();

        HandleMovement();
        HandleHeightAndCamera();
        HandleCameraControl();
        HandleCameraTilt();
        HandleFovChange();

        if (enableHeadBob)
            HandleHeadBob();
    }

    private void CheckGroundStatus()
    {
        // ground check 
        Vector3 origin = transform.position + Vector3.up * characterController.radius;
        bool groundHit = Physics.SphereCast(
            origin,
            characterController.radius * 0.8f,
            Vector3.down,
            out _,
            groundCheckDistance,
            groundLayer
        );

        _isGrounded = groundHit || characterController.isGrounded;

        if (_isGrounded && _velocity.y < 0f)
        {
            _hasJumped = false;
            _velocity.y = -5f;
        }
    }

    private void UpdateMovementState()
    {
        // checking the input here for boolean
        bool wantsToRun = movementEnabled &&
                          inputReader != null &&
                          inputReader.RunHeld &&
                          inputReader.MoveInput.y > 0.1f &&
                          !_isCrouching;

        if (!_isGrounded)
        {
            _currentMovementState = MovementState.Jumping;
            _currentMovementSpeed = wantsToRun ? runSpeed : moveSpeed;
            return;
        }

        if (_isCrouching)
        {
            _currentMovementState = MovementState.Crouching;
            _currentMovementSpeed = moveSpeed * 0.5f;
        }
        else
        {
            _currentMovementState = wantsToRun ? MovementState.Running : MovementState.Walking;
            _currentMovementSpeed = wantsToRun ? runSpeed : moveSpeed;
        }
    }

    private void HandleMovement()
    {
        if (characterController == null || !characterController.enabled)
            return;

        // getting the value here for the movement input .
        Vector2 moveInput2D = movementEnabled && inputReader != null
            ? inputReader.MoveInput
            : Vector2.zero;

        // translation of the Player
        Vector3 moveInput = transform.right * moveInput2D.x + transform.forward * moveInput2D.y;
        if (moveInput.magnitude > 1f)
            moveInput.Normalize();

        if (movementEnabled &&
            inputReader != null &&
            inputReader.JumpPressed &&
            _isGrounded &&
            !_isCrouching)
        {
            _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            _hasJumped = true;
            _isGrounded = false;
        }

        if (standingHeightMarker != null)
        {
            standingHeightMarker.transform.position = new Vector3(
                transform.position.x,
                transform.position.y + _markerHeightOffset,
                transform.position.z
            );
        }

        characterController.Move(moveInput * _currentMovementSpeed * Time.deltaTime);

        _velocity.y += gravity * Time.deltaTime;
        characterController.Move(_velocity * Time.deltaTime);
    }

    private void HandleCrouchLogic()
    {
        // checking if the crouch input is held
        bool crouchHeld = movementEnabled && inputReader != null && inputReader.CrouchHeld;
        _isCrouching = crouchHeld || !CanStandUp();
        _targetHeight = _isCrouching ? crouchHeight : _originalHeight;
    }

    private void HandleHeightAndCamera()
    {
        if (characterController == null || cameraRoot == null)
            return;

        float prevHeight = characterController.height;

        float lerpSpeed = crouchSmoothTime > 0.0001f ? (1f / crouchSmoothTime) : 999f;
        characterController.height = Mathf.Lerp(characterController.height, _targetHeight, Time.deltaTime * lerpSpeed);

        if (_isGrounded)
        {
            float heightDiff = characterController.height - prevHeight;
            if (heightDiff > 0f)
                characterController.Move(Vector3.up * heightDiff);
        }

        float currentRelativeHeight = _cameraBaseHeight * (characterController.height / _originalHeight);
        Vector3 camPos = cameraRoot.localPosition;
        camPos.y = Mathf.Lerp(camPos.y, currentRelativeHeight, Time.deltaTime * lerpSpeed);
        cameraRoot.localPosition = camPos;
    }

    private void HandleCameraControl()
    {

        if (!lookEnabled || cameraRoot == null || inputReader == null)
            return;

        Vector2 lookInput = inputReader.LookInput;
        float mouseX = lookInput.x * lookSensitivity;
        float mouseY = lookInput.y * lookSensitivity;

        _smoothInputX = Mathf.Lerp(_smoothInputX, mouseX, Time.deltaTime * cameraWeight);

        _targetYaw += mouseX;
        _targetPitch -= mouseY;
        _targetPitch = Mathf.Clamp(_targetPitch, maxLookDownAngle, maxLookUpAngle);

        float smoothFactor = Mathf.Clamp01(Time.deltaTime * cameraWeight);
        _currentYaw = Mathf.Lerp(_currentYaw, _targetYaw, smoothFactor);
        _currentPitch = Mathf.Lerp(_currentPitch, _targetPitch, smoothFactor);

        transform.rotation = Quaternion.Euler(0f, _currentYaw, 0f);
        cameraRoot.localRotation = Quaternion.Euler(_currentPitch, 0f, _currentTilt);
    }

    private void HandleCameraTilt()
    {
        if (!enableCameraTilt || inputReader == null)
        {
            _currentTilt = 0f;
            return;
        }

        float keyboardTilt = -inputReader.MoveInput.x * tiltAmount;
        float mouseTilt = -_smoothInputX * turnTiltAmount;
        float targetTiltTotal = keyboardTilt + mouseTilt;

        if (_currentMovementState == MovementState.Running)
            targetTiltTotal *= runTiltMultiplier;

        if (_isCrouching)
            targetTiltTotal *= crouchTiltMultiplier;

        targetTiltTotal = Mathf.Clamp(targetTiltTotal, -maxTotalTilt, maxTotalTilt);
        _currentTilt = Mathf.Lerp(_currentTilt, targetTiltTotal, Time.deltaTime * tiltSmoothness);
    }

    private void HandleFovChange()
    {
        if (!enableRunFov || cameraRoot == null || inputReader == null)
            return;

        Camera cam = cameraRoot.GetComponent<Camera>();
        if (cam == null)
            return;

        bool isActuallyRunning =
            movementEnabled &&
            inputReader.RunHeld &&
            inputReader.MoveInput.y > 0.1f &&
            _currentMovementState == MovementState.Running;

        cam.fieldOfView = Mathf.Lerp(
            cam.fieldOfView,
            isActuallyRunning ? runFov : normalFov,
            Time.deltaTime * fovChangeSpeed
        );
    }

    private void HandleHeadBob()
    {
        if (cameraRoot == null || inputReader == null)
            return;

        float moveMag = movementEnabled ? inputReader.MoveInput.magnitude : 0f;
        float currentCamH = _cameraBaseHeight * (characterController.height / _originalHeight);

        if (!_isGrounded || moveMag <= 0.1f)
        {
            _headBobTimer = 0f;
            playerCameraReturn(currentCamH);
            return;
        }

        float freq = _currentMovementState == MovementState.Running
            ? runBobFrequency
            : (_isCrouching ? crouchBobFrequency : walkBobFrequency);

        _headBobTimer += Time.deltaTime * freq;

        Vector3 newPos = new Vector3(
            Mathf.Cos(_headBobTimer * 0.5f) * bobAmountX,
            currentCamH + Mathf.Sin(_headBobTimer) * bobAmountY,
            0f
        );

        cameraRoot.localPosition = Vector3.Lerp(
            cameraRoot.localPosition,
            newPos,
            Time.deltaTime * bobSmoothness
        );
    }

    private void playerCameraReturn(float targetHeight)
    {
        cameraRoot.localPosition = Vector3.Lerp(
            cameraRoot.localPosition,
            new Vector3(0f, targetHeight, 0f),
            Time.deltaTime * bobSmoothness
        );
    }

    public void SetMovementEnabled(bool value)
    {
        movementEnabled = value;

        if (!movementEnabled)
        {
            _currentMovementSpeed = 0f;
        }
    }

    public void SetLookEnabled(bool value)
    {
        lookEnabled = value;
    }

    public void ForceSetPositionAndRotation(Vector3 position, Quaternion rotation)
    {
        bool wasEnabled = characterController != null && characterController.enabled;

        if (characterController != null)
            characterController.enabled = false;

        transform.SetPositionAndRotation(position, rotation);

        _targetYaw = transform.eulerAngles.y;
        _currentYaw = _targetYaw;

        if (cameraRoot != null)
        {
            Vector3 localEuler = cameraRoot.localEulerAngles;
            _targetPitch = NormalizePitch(localEuler.x);
            _currentPitch = _targetPitch;
            cameraRoot.localRotation = Quaternion.Euler(_currentPitch, 0f, _currentTilt);
        }

        _velocity = Vector3.zero;

        if (characterController != null)
            characterController.enabled = wasEnabled;
    }

    public bool CanStandUp()
    {
        if (standingHeightMarker == null)
            return true;

        Collider[] hits = Physics.OverlapSphere(
            standingHeightMarker.transform.position,
            standingCheckRadius,
            obstacleLayerMask
        );

        foreach (Collider col in hits)
        {
            if (col.transform.IsChildOf(transform) || col.transform == transform || col.isTrigger)
                continue;

            if (col.bounds.min.y < standingHeightMarker.transform.position.y + minStandingClearance)
                return false;
        }

        return true;
    }

    private float NormalizePitch(float angle)
    {
        if (angle > 180f)
            angle -= 360f;

        return angle;
    }

    private void OnDrawGizmosSelected()
    {
        if (standingHeightMarker == null)
            return;

        Gizmos.color = Application.isPlaying
            ? (CanStandUp() ? Color.green : Color.red)
            : Color.yellow;

        Gizmos.DrawWireSphere(standingHeightMarker.transform.position, standingCheckRadius);
    }
}