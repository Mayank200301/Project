
using UnityEngine;

namespace OMG.Parkour
{
    public class ParkourStartPoint : MonoBehaviour
    {
        private Collider triggerCollider;

        // Prevent the same trigger from starting the race twice.
        private bool triggered;


        // ============================================================
        // AWAKE
        // ============================================================

        private void Awake()
        {
            triggerCollider = GetComponent<Collider>();

            if (triggerCollider != null)
            {
                triggerCollider.isTrigger = true;
            }
            else
            {
                Debug.LogError(
                    "ParkourStartPoint: No Collider found on " +
                    gameObject.name
                );
            }
        }


        // ============================================================
        // TRIGGER ENTER
        // ============================================================

        private void OnTriggerEnter(Collider other)
        {
            if (triggered)
                return;

            if (other == null)
                return;

            if (!other.CompareTag("Player"))
                return;

            if (GameManager.Instance == null)
            {
                Debug.LogError(
                    "ParkourStartPoint: GameManager not found!"
                );

                return;
            }


            // --------------------------------------------------------
            // If race has already started, ignore this trigger.
            // --------------------------------------------------------

            if (GameManager.Instance.RaceActive)
                return;


            // --------------------------------------------------------
            // Lock this Start Point immediately.
            // --------------------------------------------------------

            triggered = true;


            // --------------------------------------------------------
            // Start race.
            // --------------------------------------------------------

            GameManager.Instance.StartRaceFromPoint(transform);
        }
    }
}
