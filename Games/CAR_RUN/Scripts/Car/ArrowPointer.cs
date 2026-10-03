using UnityEngine;

public class ArrowPointer : MonoBehaviour
{
    public Transform target;
    public Transform car;

    [Header("Rotation Offset")]
    public float rotationOffset = 0f;

    private void LateUpdate()
    {
        if (target == null || car == null)
            return;

        Vector3 direction = target.position - car.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f)
            return;

        Quaternion lookRotation = Quaternion.LookRotation(direction);
        transform.rotation = lookRotation * Quaternion.Euler(rotationOffset, rotationOffset, 0);
    }
}