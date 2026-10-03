using UnityEngine;

public class MobileInput : MonoBehaviour
{
    public static MobileInput Instance;

    [Header("Driving")]
    [HideInInspector] public float moveInput;
    [HideInInspector] public float steerInput;
    [HideInInspector] public bool handBrake;

    [Header("Reverse")]
    [HideInInspector] public bool reverseMode;


    private void Awake()
    {
        Instance = this;

        reverseMode = false;
    }

    // ================= STEERING =================

    public void LeftDown()
    {
        steerInput = -1f;
    }

    public void RightDown()
    {
        steerInput = 1f;
    }

    public void SteerUp()
    {
        steerInput = 0f;
    }

    // ================= ACCELERATION =================

    public void AcceleratePress()
    {
        moveInput = reverseMode ? -1f : 1f;
    }

    public void AccelerateRelease()
    {
        moveInput = 0f;
    }

    // ================= BRAKE =================

    public void BrakePress()
    {
        handBrake = true;
    }

    public void BrakeRelease()
    {
        handBrake = false;
    }

    // ================= REVERSE =================

    public void ToggleReverse()
    {
        reverseMode = !reverseMode;

        Debug.Log("Reverse: " + reverseMode);

        // If accelerator is already pressed
        if (moveInput != 0f)
        {
            moveInput = reverseMode ? -1f : 1f;
        }
    }

    

    
}