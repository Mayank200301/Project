using UnityEngine;

public class BoxBalance : MonoBehaviour
{
    [SerializeField] private Transform car;
    [SerializeField] private Transform boxHolder;
    [SerializeField] private float fallAngle = 75f;

    private bool boxDropped = false;

    void Update()
    {
        if (boxDropped)
            return;

        if (!MissionManager.Instance.IsCarryingBox)
            return;

        float x = Normalize(car.eulerAngles.x);
        float z = Normalize(car.eulerAngles.z);

        if (Mathf.Abs(x) > fallAngle || Mathf.Abs(z) > fallAngle)
        {
            DropBox();
        }
    }

    float Normalize(float angle)
    {
        if (angle > 180f)
            angle -= 360f;

        return angle;
    }

    void DropBox()
    {
        boxDropped = true;
    
        GameObject box = MissionManager.Instance.currentBox;
    
        if (box == null)
            return;
    
        box.transform.SetParent(null);
    
        Rigidbody rb = box.GetComponent<Rigidbody>();
    
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }
    
        MissionManager.Instance.BoxDropped();
    }

    public void ResetDrop()
    {
        boxDropped = false;
    }
}