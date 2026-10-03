using UnityEngine;
using UnityEngine.InputSystem;

public class CarControl : MonoBehaviour
{
    [Header("Car Settings")]
    public float motorTorque = 2000f;
    public float brakeTorque = 3000f;
    public float handBrakeTorque = 6000f;
    public float maxSpeed = 20f;
    public float steeringRange = 30f;
    public float steeringRangeAtMaxSpeed = 10f;
    public float centreOfGravityOffset = -1f;

    private WheelControl[] wheels;
    private Rigidbody rb;

    private float moveInput;
    private float steerInput;
    private bool handBrake;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();

        rb.centerOfMass += Vector3.up * centreOfGravityOffset;

        wheels = GetComponentsInChildren<WheelControl>();
    }

    private void Update()
    {
        ReadInput();
    }

    private void FixedUpdate()
    {
        Drive();
    }

    private void ReadInput()
    {
        moveInput = 0f;
        steerInput = 0f;
        handBrake = false;
    
        // Mobile input
        if (MobileInput.Instance != null)
        {
            moveInput = MobileInput.Instance.moveInput;
            steerInput = MobileInput.Instance.steerInput;
            handBrake = MobileInput.Instance.handBrake;
        }
    
        // Keyboard input (also works in Editor)
        Keyboard keyboard = Keyboard.current;
    
        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                moveInput = 1f;
            else if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                moveInput = -1f;
    
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                steerInput = -1f;
            else if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                steerInput = 1f;
    
            if (keyboard.spaceKey.isPressed)
                handBrake = true;
        }
    }

    private void Drive()
    {
        float forwardSpeed = Vector3.Dot(transform.forward, rb.linearVelocity);

        float speedFactor = Mathf.InverseLerp(0, maxSpeed, Mathf.Abs(forwardSpeed));

        float currentMotorTorque = Mathf.Lerp(motorTorque, 0, speedFactor);

        float currentSteerRange = Mathf.Lerp(steeringRange,steeringRangeAtMaxSpeed,speedFactor);

        bool isAccelerating = Mathf.Approximately(forwardSpeed, 0f) || Mathf.Sign(moveInput) == Mathf.Sign(forwardSpeed);

        foreach (WheelControl wheel in wheels)
        {
            // Steering
            if (wheel.steerable)
            {
                wheel.WheelCollider.steerAngle = steerInput * currentSteerRange;
            }

            // Hand Brake
            if (handBrake)
            {
                wheel.WheelCollider.motorTorque = 0f;
                wheel.WheelCollider.brakeTorque = handBrakeTorque;
                continue;
            }

            // Drive / Brake
            if (isAccelerating)
            {
                if (wheel.motorized)
                    wheel.WheelCollider.motorTorque = moveInput * currentMotorTorque;

                wheel.WheelCollider.brakeTorque = 0f;
            }
            else
            {
                wheel.WheelCollider.motorTorque = 0f; 
                wheel.WheelCollider.brakeTorque = Mathf.Abs(moveInput) * brakeTorque;
            }
        }
        
    }
    
}