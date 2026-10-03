using UnityEngine;

public class BoxPickup : MonoBehaviour
{
    private bool pickedUp = false;

    private Rigidbody rb;
    private Collider col;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("BOX TRIGGER: " + other.name + " | Tag: " + other.tag);

  
        if (pickedUp)
            return;

        if (!other.CompareTag("Player"))
            return;

        // Find LoadPoint FIRST
        Transform loadPoint = other.transform.Find("LoadPoint");

        if (loadPoint == null)
        {
            Debug.LogError(
                "LoadPoint not found on Player/Car. " +
                "Make sure LoadPoint is a child of the object tagged Player."
            );
            return;
        }

        // Stop floating
        FloatingObject floating = GetComponent<FloatingObject>();

        if (floating != null)
        {
            floating.StopFloating();
        }

        // Mark as picked up only after LoadPoint is confirmed
        pickedUp = true;

        // Set current box
        if (MissionManager.Instance != null)
        {
            MissionManager.Instance.currentBox = gameObject;
        }

        // Stop physics
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            rb.useGravity = false;
            rb.isKinematic = true;
        }

        // Ignore collision with the entire car
        if (col != null)
        {
            Collider[] carColliders =
                other.GetComponentsInChildren<Collider>();

            foreach (Collider carCollider in carColliders)
            {
                if (carCollider != col)
                {
                    Physics.IgnoreCollision(col, carCollider, true);
                }
            }

            // Disable box collider after pickup
            col.enabled = false;
        }

        // Parent box to LoadPoint
        transform.SetParent(loadPoint);

        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        // Notify MissionManager
        if (MissionManager.Instance != null)
        {
            MissionManager.Instance.BoxPickedUp();
        }

        Debug.Log("Box Picked Up");
    }
}