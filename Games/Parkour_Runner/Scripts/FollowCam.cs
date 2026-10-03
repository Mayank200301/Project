using UnityEngine;

public class FollowCam : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Camera Position")]
    public Vector3 offset = new Vector3(0f, 5f, -8f);

    [Header("Follow Settings")]
    public float followSpeed = 8f;
    public float rotationSpeed = 8f;

    [Header("Look")]
    public float lookHeight = 1.5f;

    private Vector3 positionVelocity;
    private float rotationVelocity;

    private void LateUpdate()
    {
        if (target == null)
            return;

        // Camera position relative to the player's rotation.
        Vector3 desiredPosition =
            target.position +
            target.TransformDirection(offset);

        // Smooth camera movement.
        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref positionVelocity,
            1f / followSpeed
        );

        // Look at the player.
        Vector3 lookTarget =
            target.position +
            Vector3.up * lookHeight;

        Vector3 direction =
            lookTarget - transform.position;

        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion desiredRotation =
                Quaternion.LookRotation(direction);

            float desiredY =
                desiredRotation.eulerAngles.y;

            float currentY =
                transform.eulerAngles.y;

            float smoothY =
                Mathf.SmoothDampAngle(
                    currentY,
                    desiredY,
                    ref rotationVelocity,
                    1f / rotationSpeed
                );

            transform.rotation =
                Quaternion.Euler(
                    desiredRotation.eulerAngles.x,
                    smoothY,
                    0f
                );
        }
    }
}
