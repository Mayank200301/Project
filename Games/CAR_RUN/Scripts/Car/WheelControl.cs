using UnityEngine;

[RequireComponent(typeof(WheelCollider))]
public class WheelControl : MonoBehaviour
{
    [Header("Wheel Model")]
    public Transform wheelModel;

    [Tooltip("Rotation offset applied to the wheel model to correct mesh orientation (e.g. if the model faces the wrong way, try 0,90,0).")]
    public Vector3 rotationOffset = new Vector3(0f, 90f, 0f);

    [Header("Wheel Settings")]
    public bool steerable = false;
    public bool motorized = false;

    [HideInInspector]
    public WheelCollider WheelCollider;

    private Vector3 wheelPosition;
    private Quaternion wheelRotation;

    private void Awake()
    {
        WheelCollider = GetComponent<WheelCollider>();

        if (wheelModel == null)
        {
            Debug.LogError($"{name}: Wheel Model is not assigned!", this);
        }

        
    }

    private void LateUpdate()
    {
        if (wheelModel == null || WheelCollider == null)
            return;

        // Synchronize the visual wheel with the WheelCollider
        WheelCollider.GetWorldPose(out wheelPosition, out wheelRotation);

        wheelModel.SetPositionAndRotation(wheelPosition, wheelRotation * Quaternion.Euler(rotationOffset));
    }
}