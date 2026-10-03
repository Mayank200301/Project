using UnityEngine;

namespace OMG.Parkour
{
    public class ParkourLevelEnd : MonoBehaviour
    {
        [Header("Level")]
        [Tooltip("0 = Level 1, 1 = Level 2, etc.")]
        public int levelIndex = 0;


        private void Awake()
        {
            Collider col = GetComponent<Collider>();

            if (col != null)
                col.isTrigger = true;
        }


        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player"))
                return;

            if (GameManager.Instance == null)
                return;

            GameManager.Instance.CompleteLevel(levelIndex);
        }
    }
}