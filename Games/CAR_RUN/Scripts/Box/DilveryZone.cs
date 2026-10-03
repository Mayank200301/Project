using UnityEngine;

public class DeliveryZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        MissionManager manager = MissionManager.Instance;

        if (!manager.IsCarryingBox)
            return;

        if (manager.currentDeliveryPoint != transform)
            return;

        if (manager.currentBox != null)
        {
            GameObject deliveredBox = manager.currentBox;
        
            manager.currentBox = null;
        
            deliveredBox.transform.SetParent(null);
        
            Destroy(deliveredBox);
        }

        manager.DeliveryComplete();
    }
}