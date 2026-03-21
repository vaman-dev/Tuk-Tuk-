using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInput))]
public class PlayerInputReader : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerInput playerInput;

    [Header("Action Map Names")]
    [SerializeField] private string playerMapName = "Player";
    [SerializeField] private string vehicleMapName = "Vehicle";

    [Header("Player Action Names")]
    [SerializeField] private string moveActionName = "Move";
    [SerializeField] private string lookActionName = "Look";
    [SerializeField] private string jumpActionName = "Jump";
    [SerializeField] private string runActionName = "Run";
    [SerializeField] private string crouchActionName = "Crouch";
    [SerializeField] private string interactActionName = "Interact";

    [Header("Vehicle Action Names")]
    [SerializeField] private string steerActionName = "Steer";
    [SerializeField] private string driveActionName = "Drive";
    [SerializeField] private string brakeActionName = "Brake";
    [SerializeField] private string exitVehicleActionName = "ExitVehicle";
    [SerializeField] private string vehicleLookActionName = "Look";

    [Header("Player Inputs")]
    public Vector2 MoveInput { get; private set; }
    public Vector2 LookInput { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool RunHeld { get; private set; }
    public bool CrouchHeld { get; private set; }
    public bool InteractPressed { get; private set; }

    [Header("Vehicle Inputs")]
    public float SteerInput { get; private set; }
    public float DriveInput { get; private set; }
    public bool BrakeHeld { get; private set; }
    public bool ExitVehiclePressed { get; private set; }

    public string CurrentActionMapName =>
        playerInput != null && playerInput.currentActionMap != null
            ? playerInput.currentActionMap.name
            : string.Empty;

    private InputActionMap _playerMap;
    private InputActionMap _vehicleMap;

    private InputAction _moveAction;
    private InputAction _lookAction;
    private InputAction _jumpAction;
    private InputAction _runAction;
    private InputAction _crouchAction;
    private InputAction _interactAction;

    private InputAction _steerAction;
    private InputAction _driveAction;
    private InputAction _brakeAction;
    private InputAction _exitVehicleAction;
    private InputAction _vehicleLookAction;

    private void Awake()
    {
        if (playerInput == null)
            playerInput = GetComponent<PlayerInput>();

        CacheMapsAndActions();
    }

    private void OnEnable()
    {
        CacheMapsAndActions();
        SubscribePlayerActions();
        SubscribeVehicleActions();
    }

    private void OnDisable()
    {
        UnsubscribePlayerActions();
        UnsubscribeVehicleActions();
        ResetAllInputs();
    }

    private void LateUpdate()
    {
        ClearOneFrameInputs();
    }

    private void CacheMapsAndActions()
    {
        if (playerInput == null || playerInput.actions == null)
        {
            Debug.LogError($"[{nameof(PlayerInputReader)}] PlayerInput or InputActionAsset is missing.", this);
            return;
        }

        InputActionAsset asset = playerInput.actions;

        _playerMap = asset.FindActionMap(playerMapName, false);
        _vehicleMap = asset.FindActionMap(vehicleMapName, false);

        if (_playerMap == null)
            Debug.LogError($"[{nameof(PlayerInputReader)}] Could not find action map '{playerMapName}'.", this);

        if (_vehicleMap == null)
            Debug.LogError($"[{nameof(PlayerInputReader)}] Could not find action map '{vehicleMapName}'.", this);

        _moveAction = _playerMap?.FindAction(moveActionName, false);
        _lookAction = _playerMap?.FindAction(lookActionName, false);
        _jumpAction = _playerMap?.FindAction(jumpActionName, false);
        _runAction = _playerMap?.FindAction(runActionName, false);
        _crouchAction = _playerMap?.FindAction(crouchActionName, false);
        _interactAction = _playerMap?.FindAction(interactActionName, false);

        _steerAction = _vehicleMap?.FindAction(steerActionName, false);
        _driveAction = _vehicleMap?.FindAction(driveActionName, false);
        _brakeAction = _vehicleMap?.FindAction(brakeActionName, false);
        _exitVehicleAction = _vehicleMap?.FindAction(exitVehicleActionName, false);
        _vehicleLookAction = _vehicleMap?.FindAction(vehicleLookActionName, false);

        ValidateAction(_moveAction, moveActionName, playerMapName);
        ValidateAction(_lookAction, lookActionName, playerMapName);
        ValidateAction(_jumpAction, jumpActionName, playerMapName);
        ValidateAction(_runAction, runActionName, playerMapName);
        ValidateAction(_crouchAction, crouchActionName, playerMapName);
        ValidateAction(_interactAction, interactActionName, playerMapName);

        ValidateAction(_steerAction, steerActionName, vehicleMapName);
        ValidateAction(_driveAction, driveActionName, vehicleMapName);
        ValidateAction(_brakeAction, brakeActionName, vehicleMapName);
        ValidateAction(_exitVehicleAction, exitVehicleActionName, vehicleMapName);
    }

    private void SubscribePlayerActions()
    {
        if (_moveAction != null)
        {
            _moveAction.performed += OnMovePerformed;
            _moveAction.canceled += OnMoveCanceled;
        }

        if (_lookAction != null)
        {
            _lookAction.performed += OnLookPerformed;
            _lookAction.canceled += OnLookCanceled;
        }

        if (_jumpAction != null)
        {
            _jumpAction.performed += OnJumpPerformed;
        }

        if (_runAction != null)
        {
            _runAction.performed += OnRunPerformed;
            _runAction.canceled += OnRunCanceled;
        }

        if (_crouchAction != null)
        {
            _crouchAction.performed += OnCrouchPerformed;
            _crouchAction.canceled += OnCrouchCanceled;
        }

        if (_interactAction != null)
        {
            _interactAction.performed += OnInteractPerformed;
        }
    }

    private void UnsubscribePlayerActions()
    {
        if (_moveAction != null)
        {
            _moveAction.performed -= OnMovePerformed;
            _moveAction.canceled -= OnMoveCanceled;
        }

        if (_lookAction != null)
        {
            _lookAction.performed -= OnLookPerformed;
            _lookAction.canceled -= OnLookCanceled;
        }

        if (_jumpAction != null)
        {
            _jumpAction.performed -= OnJumpPerformed;
        }

        if (_runAction != null)
        {
            _runAction.performed -= OnRunPerformed;
            _runAction.canceled -= OnRunCanceled;
        }

        if (_crouchAction != null)
        {
            _crouchAction.performed -= OnCrouchPerformed;
            _crouchAction.canceled -= OnCrouchCanceled;
        }

        if (_interactAction != null)
        {
            _interactAction.performed -= OnInteractPerformed;
        }
    }

    private void SubscribeVehicleActions()
    {
        if (_steerAction != null)
        {
            _steerAction.performed += OnSteerPerformed;
            _steerAction.canceled += OnSteerCanceled;
        }

        if (_driveAction != null)
        {
            _driveAction.performed += OnDrivePerformed;
            _driveAction.canceled += OnDriveCanceled;
        }

        if (_brakeAction != null)
        {
            _brakeAction.performed += OnBrakePerformed;
            _brakeAction.canceled += OnBrakeCanceled;
        }

        if (_exitVehicleAction != null)
        {
            _exitVehicleAction.performed += OnExitVehiclePerformed;
        }

        if (_vehicleLookAction != null)
        {
            _vehicleLookAction.performed += OnLookPerformed;
            _vehicleLookAction.canceled += OnLookCanceled;
        }
    }

    private void UnsubscribeVehicleActions()
    {
        if (_steerAction != null)
        {
            _steerAction.performed -= OnSteerPerformed;
            _steerAction.canceled -= OnSteerCanceled;
        }

        if (_driveAction != null)
        {
            _driveAction.performed -= OnDrivePerformed;
            _driveAction.canceled -= OnDriveCanceled;
        }

        if (_brakeAction != null)
        {
            _brakeAction.performed -= OnBrakePerformed;
            _brakeAction.canceled -= OnBrakeCanceled;
        }

        if (_exitVehicleAction != null)
        {
            _exitVehicleAction.performed -= OnExitVehiclePerformed;
        }

        if (_vehicleLookAction != null)
        {
            _vehicleLookAction.performed -= OnLookPerformed;
            _vehicleLookAction.canceled -= OnLookCanceled;
        }
    }

    public void SwitchToPlayerMap()
    {
        if (playerInput == null)
            return;

        ResetAllInputs();
        playerInput.SwitchCurrentActionMap(playerMapName);
    }

    public void SwitchToVehicleMap()
    {
        if (playerInput == null)
            return;

        ResetAllInputs();
        playerInput.SwitchCurrentActionMap(vehicleMapName);
    }

    public void ClearOneFrameInputs()
    {
        JumpPressed = false;
        InteractPressed = false;
        ExitVehiclePressed = false;
    }

    public void ResetAllInputs()
    {
        MoveInput = Vector2.zero;
        LookInput = Vector2.zero;
        JumpPressed = false;
        RunHeld = false;
        CrouchHeld = false;
        InteractPressed = false;

        SteerInput = 0f;
        DriveInput = 0f;
        BrakeHeld = false;
        ExitVehiclePressed = false;
    }

    private void ValidateAction(InputAction action, string actionName, string mapName)
    {
        if (action == null)
        {
            Debug.LogWarning(
                $"[{nameof(PlayerInputReader)}] Could not find action '{actionName}' in map '{mapName}'.",
                this
            );
        }
    }

    private void OnMovePerformed(InputAction.CallbackContext context)
    {
        MoveInput = context.ReadValue<Vector2>();
    }

    private void OnMoveCanceled(InputAction.CallbackContext context)
    {
        MoveInput = Vector2.zero;
    }

    private void OnLookPerformed(InputAction.CallbackContext context)
    {
        LookInput = context.ReadValue<Vector2>();
    }

    private void OnLookCanceled(InputAction.CallbackContext context)
    {
        LookInput = Vector2.zero;
    }

    private void OnJumpPerformed(InputAction.CallbackContext context)
    {
        JumpPressed = true;
    }

    private void OnRunPerformed(InputAction.CallbackContext context)
    {
        RunHeld = true;
    }

    private void OnRunCanceled(InputAction.CallbackContext context)
    {
        RunHeld = false;
    }

    private void OnCrouchPerformed(InputAction.CallbackContext context)
    {
        CrouchHeld = true;
    }

    private void OnCrouchCanceled(InputAction.CallbackContext context)
    {
        CrouchHeld = false;
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        InteractPressed = true;
    }

    private void OnSteerPerformed(InputAction.CallbackContext context)
    {
        SteerInput = context.ReadValue<float>();
    }

    private void OnSteerCanceled(InputAction.CallbackContext context)
    {
        SteerInput = 0f;
    }

    private void OnDrivePerformed(InputAction.CallbackContext context)
    {
        DriveInput = context.ReadValue<float>();
    }

    private void OnDriveCanceled(InputAction.CallbackContext context)
    {
        DriveInput = 0f;
    }

    private void OnBrakePerformed(InputAction.CallbackContext context)
    {
        BrakeHeld = true;
    }

    private void OnBrakeCanceled(InputAction.CallbackContext context)
    {
        BrakeHeld = false;
    }

    private void OnExitVehiclePerformed(InputAction.CallbackContext context)
    {
        ExitVehiclePressed = true;
    }
}