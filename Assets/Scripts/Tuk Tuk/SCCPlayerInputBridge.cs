using UnityEngine;

[DisallowMultipleComponent]
public class SCCPlayerInputBridge : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SCC_InputProcessor inputProcessor;

    [Header("Runtime")]
    [SerializeField] private PlayerInputReader currentDriver;
    [SerializeField] private bool vehicleInputActive;

    [Header("Mapping")]
    [SerializeField] private bool useDriveAxisForThrottleAndBrake = true;
    [SerializeField] private bool brakeButtonOverridesBrakeAxis = true;
    [SerializeField] private bool useBrakeAsHandbrake = false;
    [SerializeField, Range(0f, 1f)] private float brakeButtonStrength = 1f;

    private SCC_Inputs _overrideInputs;

    public bool HasDriver => currentDriver != null;
    public bool VehicleInputActive => vehicleInputActive;
    public PlayerInputReader CurrentDriver => currentDriver;

    private void Awake()
    {
        if (inputProcessor == null)
            inputProcessor = GetComponent<SCC_InputProcessor>();

        _overrideInputs = new SCC_Inputs();

        ForceDisableSCCDefaultInput();
        PushZeroInputs();
    }

    private void OnEnable()
    {
        ForceDisableSCCDefaultInput();
        PushZeroInputs();
    }

    private void Update()
    {
        ForceDisableSCCDefaultInput();

        if (!vehicleInputActive || currentDriver == null)
        {
            PushZeroInputs();
            return;
        }

        PushPlayerInputsToSCC();
    }

    public void SetDriver(PlayerInputReader driver)
    {
        currentDriver = driver;
        vehicleInputActive = currentDriver != null;

        ForceDisableSCCDefaultInput();

        if (!vehicleInputActive)
            PushZeroInputs();
    }

    public void ClearDriver()
    {
        currentDriver = null;
        vehicleInputActive = false;

        ForceDisableSCCDefaultInput();
        PushZeroInputs();
    }

    private void PushPlayerInputsToSCC()
    {
        if (inputProcessor == null || currentDriver == null)
            return;

        float steer = currentDriver.SteerInput;
        float drive = currentDriver.DriveInput;
        bool brakeHeld = currentDriver.BrakeHeld;

        float throttle = 0f;
        float brake = 0f;
        float handbrake = 0f;

        if (useDriveAxisForThrottleAndBrake)
        {
            if (drive > 0f)
                throttle = drive;
            else if (drive < 0f)
                brake = Mathf.Abs(drive);
        }

        if (brakeButtonOverridesBrakeAxis && brakeHeld)
            brake = Mathf.Max(brake, brakeButtonStrength);

        if (useBrakeAsHandbrake && brakeHeld)
            handbrake = 1f;

        _overrideInputs.steerInput = Mathf.Clamp(steer, -1f, 1f);
        _overrideInputs.throttleInput = Mathf.Clamp01(throttle);
        _overrideInputs.brakeInput = Mathf.Clamp01(brake);
        _overrideInputs.handbrakeInput = Mathf.Clamp01(handbrake);

        inputProcessor.OverrideInputs(_overrideInputs);
    }

    private void PushZeroInputs()
    {
        if (inputProcessor == null)
            return;

        _overrideInputs.steerInput = 0f;
        _overrideInputs.throttleInput = 0f;
        _overrideInputs.brakeInput = 0f;
        _overrideInputs.handbrakeInput = 0f;

        inputProcessor.inputs.steerInput = 0f;
        inputProcessor.inputs.throttleInput = 0f;
        inputProcessor.inputs.brakeInput = 0f;
        inputProcessor.inputs.handbrakeInput = 0f;

        inputProcessor.OverrideInputs(_overrideInputs);
    }

    private void ForceDisableSCCDefaultInput()
    {
        if (inputProcessor != null)
            inputProcessor.receiveInputsFromInputManager = false;
    }

    private void OnValidate()
    {
        if (inputProcessor == null)
            inputProcessor = GetComponent<SCC_InputProcessor>();
    }
}