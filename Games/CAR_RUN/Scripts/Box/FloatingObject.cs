using UnityEngine;

public class FloatingObject : MonoBehaviour
{
    [Header("Floating")]
    public float floatHeight = 0.3f;
    public float floatSpeed = 2f;

    [Header("Rotation")]
    public float rotationSpeed = 40f;

    private Vector3 startPosition;
    private bool canFloat = true;

    private void Start()
    {
        startPosition = transform.position;
    }

    private void Update()
    {
        if (!canFloat)
            return;

        // Floating
        float yOffset = Mathf.Sin(Time.time * floatSpeed) * floatHeight;
        transform.position = startPosition + Vector3.up * yOffset;

        // Rotation
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
    }

    public void StopFloating()
    {
        canFloat = false;
    }
}