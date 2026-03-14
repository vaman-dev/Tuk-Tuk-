using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class TukTukController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private PlayerInputReader inputReader;

    [Header("Drive Settings")]
    [SerializeField] private float accelerationForce = 18f;
    [SerializeField] private float reverseForce = 10f;
    [SerializeField] private float maxForwardSpeed = 14f;
    [SerializeField] private float maxReverseSpeed = 6f;
    [SerializeField] private float brakeForce = 20f;

    [Header("Steering Settings")]
    [SerializeField] private float turnSpeed = 90f;
    [SerializeField] private float turnSpeedAtZeroVelocity = 35f;
    [SerializeField] private float steeringResponsiveness = 8f;

    [Header("Stability Settings")]
    [SerializeField] private float groundDrag = 2.5f;
    [SerializeField] private float idleDrag = 4.5f;
    [SerializeField] private float extraDownForce = 8f;
    [SerializeField] private bool freezeXRotation = true;
    [SerializeField] private bool freezeZRotation = true;

    [Header("Runtime State")]
    [SerializeField] private bool driverActive;

    private float _currentSteer;
    private float _currentDrive;

    public bool DriverActive => driverActive;

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (inputReader == null)
            inputReader = FindFirstObjectByType<PlayerInputReader>();

        SetupRigidbody();
    }

    private void FixedUpdate()
    {
        ApplyBaseForces();

        if (!driverActive || inputReader == null)
        {
            ApplyIdleDrag();
            return;
        }

        ReadInputs();
        HandleAcceleration();
        HandleSteering();
        HandleBraking();
        LimitPlanarSpeed();
    }

    public void SetDriverActive(bool value)
    {
        driverActive = value;

        if (!driverActive)
        {
            _currentSteer = 0f;
            _currentDrive = 0f;
        }
    }

    private void SetupRigidbody()
    {
        if (rb == null)
            return;

        rb.interpolation = RigidbodyInterpolation.Interpolate;

        if (freezeXRotation || freezeZRotation)
        {
            RigidbodyConstraints constraints = RigidbodyConstraints.None;

            if (freezeXRotation)
                constraints |= RigidbodyConstraints.FreezeRotationX;

            if (freezeZRotation)
                constraints |= RigidbodyConstraints.FreezeRotationZ;

            rb.constraints = constraints;
        }
    }

    private void ReadInputs()
    {
        float targetSteer = inputReader.SteerInput;
        _currentSteer = Mathf.Lerp(_currentSteer, targetSteer, Time.fixedDeltaTime * steeringResponsiveness);

        _currentDrive = inputReader.DriveInput;
    }

    private void HandleAcceleration()
    {
        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);
        float forwardSpeed = localVelocity.z;

        if (_currentDrive > 0.01f)
        {
            if (forwardSpeed < maxForwardSpeed)
            {
                rb.AddForce(transform.forward * (_currentDrive * accelerationForce), ForceMode.Acceleration);
            }
        }
        else if (_currentDrive < -0.01f)
        {
            if (forwardSpeed > -maxReverseSpeed)
            {
                rb.AddForce(transform.forward * (_currentDrive * reverseForce), ForceMode.Acceleration);
            }
        }
    }

    private void HandleSteering()
    {
        float speedFactor = Mathf.Clamp01(GetPlanarSpeed() / maxForwardSpeed);
        float currentTurnSpeed = Mathf.Lerp(turnSpeedAtZeroVelocity, turnSpeed, speedFactor);

        float steerAmount = _currentSteer * currentTurnSpeed * Time.fixedDeltaTime;

        if (Mathf.Abs(_currentSteer) > 0.01f)
        {
            Quaternion deltaRotation = Quaternion.Euler(0f, steerAmount, 0f);
            rb.MoveRotation(rb.rotation * deltaRotation);
        }
    }

    private void HandleBraking()
    {
        if (!inputReader.BrakeHeld)
            return;

        Vector3 planarVelocity = GetPlanarVelocity();
        Vector3 brakeAcceleration = -planarVelocity * brakeForce * Time.fixedDeltaTime;
        rb.AddForce(brakeAcceleration, ForceMode.VelocityChange);
    }

    private void LimitPlanarSpeed()
    {
        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);

        localVelocity.z = Mathf.Clamp(localVelocity.z, -maxReverseSpeed, maxForwardSpeed);

        Vector3 limitedWorldVelocity =
            transform.forward * localVelocity.z +
            transform.right * localVelocity.x +
            Vector3.up * rb.linearVelocity.y;

        rb.linearVelocity = limitedWorldVelocity;
    }

    private void ApplyBaseForces()
    {
        rb.AddForce(Vector3.down * extraDownForce, ForceMode.Acceleration);
    }

    private void ApplyIdleDrag()
    {
        if (GetPlanarSpeed() < 0.15f)
            rb.linearDamping = idleDrag;
        else
            rb.linearDamping = groundDrag;
    }

    private Vector3 GetPlanarVelocity()
    {
        Vector3 velocity = rb.linearVelocity;
        velocity.y = 0f;
        return velocity;
    }

    private float GetPlanarSpeed()
    {
        return GetPlanarVelocity().magnitude;
    }

    private void OnValidate()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();
    }
}