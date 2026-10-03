
using UnityEngine;

namespace OMG.Parkour
{
    public class ParkourCheckpoint : MonoBehaviour
    {
        [Header("Checkpoint")]
        [Tooltip("0 = Level 1, 1 = Level 2, etc.")]
        public int levelIndex = 0;

        [Tooltip("Optional. Leave empty to use this checkpoint's position.")]
        public Transform respawnPoint;

        private Collider triggerCollider;


        private void Awake()
        {
            triggerCollider = GetComponent<Collider>();

            if (triggerCollider != null)
                triggerCollider.isTrigger = true;
        }


        private void Start()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RegisterCheckpoint(this);
            }

            // Disabled until its level starts.
            SetActive(false);
        }


        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player"))
                return;

            if (GameManager.Instance == null)
                return;

            GameManager.Instance.SetCheckpoint(this);
        }


        public void SetActive(bool active)
        {
            if (triggerCollider != null)
                triggerCollider.enabled = active;
        }


        public Vector3 GetRespawnPosition()
        {
            if (respawnPoint != null)
                return respawnPoint.position;

            return transform.position;
        }


        public Quaternion GetRespawnRotation()
        {
            if (respawnPoint != null)
                return respawnPoint.rotation;

            return transform.rotation;
        }
    }
}

