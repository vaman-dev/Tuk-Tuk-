using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class TukTukController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private PlayerInputReader inputReader;

    [Header("Driver State")]
    [SerializeField] private bool driverActive;

    [Header("Center Of Mass")]
    [SerializeField] private bool overrideCenterOfMass = true;
    [SerializeField] private Vector3 centerOfMassOffset = new Vector3(0f, -0.45f, 0f);

    [Header("Forward / Reverse")]
    [SerializeField] private float forwardAcceleration = 22f;
    [SerializeField] private float reverseAcceleration = 12f;
    [SerializeField] private float maxForwardSpeed = 18f;
    [SerializeField] private float maxReverseSpeed = 7f;
    [SerializeField] private float throttleResponsiveness = 7f;

    [Header("Steering")]
    [SerializeField] private float maxSteerTorque = 14f;
    [SerializeField] private float lowSpeedSteerMultiplier = 1.2f;
    [SerializeField] private float highSpeedSteerMultiplier = 0.45f;
    [SerializeField] private float steeringResponsiveness = 8f;
    [SerializeField] private bool invertSteerWhenReversing = true;

    [Header("Braking / Coasting")]
    [SerializeField] private float brakeStrength = 18f;
    [SerializeField] private float coastDrag = 1.4f;
    [SerializeField] private float idleDrag = 3.5f;
    [SerializeField] private float hardBrakeDrag = 0.5f;

    [Header("Grip / Stability")]
    [SerializeField] private float lateralGrip = 8f;
    [SerializeField] private float yawStability = 2.5f;
    [SerializeField] private float angularDampingWhenDriving = 2f;
    [SerializeField] private float angularDampingWhenIdle = 4.5f;

    [Header("Extra Downforce")]
    [SerializeField] private float baseDownforce = 4f;
    [SerializeField] private float speedDownforce = 0.9f;

    [Header("Grounding")]
    [SerializeField] private Transform groundCheckPoint;
    [SerializeField] private float groundCheckRadius = 0.35f;
    [SerializeField] private float groundCheckDistance = 0.25f;
    [SerializeField] private LayerMask groundLayers = ~0;

    [Header("Rigidbody Setup")]
    [SerializeField] private bool freezeXRotation = true;
    [SerializeField] private bool freezeZRotation = true;
    [SerializeField] private RigidbodyInterpolation interpolation = RigidbodyInterpolation.Interpolate;
    [SerializeField] private CollisionDetectionMode collisionDetection = CollisionDetectionMode.ContinuousDynamic;

    private float _smoothedDriveInput;
    private float _smoothedSteerInput;
    private bool _isGrounded;

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
        UpdateGroundedState();

        if (!driverActive || inputReader == null)
        {
            ApplyIdleState();
            return;
        }

        ReadInputs();
        ApplyDragProfile();
        ApplyDownforce();
        ApplyDriveForce();
        ApplySteering();
        ApplyLateralGrip();
        ApplyYawStability();
        ApplyBraking();
        ClampForwardSpeed();
    }

    public void SetDriverActive(bool value)
    {
        driverActive = value;

        if (!driverActive)
        {
            _smoothedDriveInput = 0f;
            _smoothedSteerInput = 0f;
        }
    }

    private void SetupRigidbody()
    {
        if (rb == null)
            return;

        rb.interpolation = interpolation;
        rb.collisionDetectionMode = collisionDetection;

        RigidbodyConstraints constraints = RigidbodyConstraints.None;

        if (freezeXRotation)
            constraints |= RigidbodyConstraints.FreezeRotationX;

        if (freezeZRotation)
            constraints |= RigidbodyConstraints.FreezeRotationZ;

        rb.constraints = constraints;

        if (overrideCenterOfMass)
            rb.centerOfMass = centerOfMassOffset;
    }

    private void UpdateGroundedState()
    {
        Vector3 origin;

        if (groundCheckPoint != null)
            origin = groundCheckPoint.position;
        else
            origin = transform.position + Vector3.up * 0.2f;

        _isGrounded = Physics.SphereCast(
            origin,
            groundCheckRadius,
            Vector3.down,
            out _,
            groundCheckDistance,
            groundLayers,
            QueryTriggerInteraction.Ignore
        );
    }

    private void ReadInputs()
    {
        float targetDrive = inputReader.DriveInput;
        float targetSteer = inputReader.SteerInput;

        _smoothedDriveInput = Mathf.Lerp(
            _smoothedDriveInput,
            targetDrive,
            Time.fixedDeltaTime * throttleResponsiveness
        );

        _smoothedSteerInput = Mathf.Lerp(
            _smoothedSteerInput,
            targetSteer,
            Time.fixedDeltaTime * steeringResponsiveness
        );
    }

    private void ApplyIdleState()
    {
        rb.linearDamping = idleDrag;
        rb.angularDamping = angularDampingWhenIdle;

        ApplyDownforce();
        ApplyLateralGrip();
        ApplyYawStability();
    }

    private void ApplyDragProfile()
    {
        bool braking = inputReader.BrakeHeld;
        bool throttleAlmostZero = Mathf.Abs(_smoothedDriveInput) < 0.05f;

        rb.linearDamping = braking
            ? hardBrakeDrag
            : (throttleAlmostZero ? coastDrag : 0.2f);

        rb.angularDamping = braking ? angularDampingWhenIdle : angularDampingWhenDriving;
    }

    private void ApplyDriveForce()
    {
        if (!_isGrounded)
            return;

        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);
        float forwardSpeed = localVelocity.z;

        if (_smoothedDriveInput > 0.01f)
        {
            float speedRatio = Mathf.Clamp01(Mathf.Max(0f, forwardSpeed) / maxForwardSpeed);
            float engineFactor = 1f - speedRatio;

            if (forwardSpeed < maxForwardSpeed)
            {
                rb.AddForce(
                    transform.forward * (_smoothedDriveInput * forwardAcceleration * engineFactor),
                    ForceMode.Acceleration
                );
            }
        }
        else if (_smoothedDriveInput < -0.01f)
        {
            float reverseRatio = Mathf.Clamp01(Mathf.Abs(Mathf.Min(0f, forwardSpeed)) / maxReverseSpeed);
            float reverseFactor = 1f - reverseRatio;

            if (forwardSpeed > -maxReverseSpeed)
            {
                rb.AddForce(
                    transform.forward * (_smoothedDriveInput * reverseAcceleration * reverseFactor),
                    ForceMode.Acceleration
                );
            }
        }
    }

    private void ApplySteering()
    {
        if (!_isGrounded)
            return;

        float speed = GetPlanarSpeed();
        float speedRatio = Mathf.Clamp01(speed / maxForwardSpeed);
        float steerAuthority = Mathf.Lerp(lowSpeedSteerMultiplier, highSpeedSteerMultiplier, speedRatio);

        float steerInput = _smoothedSteerInput;

        if (invertSteerWhenReversing && GetLocalForwardSpeed() < -0.1f)
            steerInput *= -1f;

        float steerTorque = steerInput * maxSteerTorque * steerAuthority;

        rb.AddTorque(Vector3.up * steerTorque, ForceMode.Acceleration);
    }

    private void ApplyLateralGrip()
    {
        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);
        float sidewaysSpeed = localVelocity.x;

        Vector3 antiSlipForce = -transform.right * sidewaysSpeed * lateralGrip;
        rb.AddForce(antiSlipForce, ForceMode.Acceleration);
    }

    private void ApplyYawStability()
    {
        float yawRate = rb.angularVelocity.y;
        rb.AddTorque(Vector3.up * (-yawRate * yawStability), ForceMode.Acceleration);
    }

    private void ApplyBraking()
    {
        if (!inputReader.BrakeHeld)
            return;

        Vector3 planarVelocity = GetPlanarVelocity();
        if (planarVelocity.sqrMagnitude < 0.0001f)
            return;

        Vector3 brakeDelta = -planarVelocity.normalized * brakeStrength * Time.fixedDeltaTime;
        Vector3 limitedBrakeDelta = Vector3.ClampMagnitude(brakeDelta, planarVelocity.magnitude);

        rb.AddForce(limitedBrakeDelta, ForceMode.VelocityChange);
    }

    private void ApplyDownforce()
    {
        float speed = GetPlanarSpeed();
        float totalDownforce = baseDownforce + (speed * speedDownforce);
        rb.AddForce(Vector3.down * totalDownforce, ForceMode.Acceleration);
    }

    private void ClampForwardSpeed()
    {
        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);

        localVelocity.z = Mathf.Clamp(localVelocity.z, -maxReverseSpeed, maxForwardSpeed);

        Vector3 clampedWorldVelocity =
            transform.forward * localVelocity.z +
            transform.right * localVelocity.x +
            Vector3.up * rb.linearVelocity.y;

        rb.linearVelocity = clampedWorldVelocity;
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

    private float GetLocalForwardSpeed()
    {
        return transform.InverseTransformDirection(rb.linearVelocity).z;
    }

    private void OnValidate()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 origin = groundCheckPoint != null ? groundCheckPoint.position : transform.position + Vector3.up * 0.2f;

        Gizmos.color = _isGrounded ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(origin, groundCheckRadius);
        Gizmos.DrawLine(origin, origin + Vector3.down * groundCheckDistance);
    }
}