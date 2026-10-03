
using System.Collections;
using UnityEngine;

namespace OMG.Parkour
{
    public class PlayerFallDetector : MonoBehaviour
    {
        [Header("Death Ground")]
        [Tooltip("Layer assigned to the Terrain that should cause respawn.")]
        [SerializeField] private LayerMask groundLayer;

        [Tooltip("Maximum distance below the player to detect the death Terrain.")]
        [SerializeField] private float detectionDistance = 3f;

        [Header("Respawn")]
        [Tooltip("Time to wait before teleporting the player back.")]
        [SerializeField] private float respawnDelay = 2f;

        [Header("Debug")]
        [SerializeField] private bool showDebugRay = true;

        public bool IsActive { get; private set; }

        private CharacterController controller;

        private bool isRespawning;


        // ============================================================
        // AWAKE
        // ============================================================

        private void Awake()
        {
            controller = GetComponent<CharacterController>();

            if (controller == null)
            {
                Debug.LogWarning(
                    "PlayerFallDetector: CharacterController not found on " +
                    gameObject.name
                );
            }
        }


        // ============================================================
        // ACTIVE STATE
        // ============================================================

        public void SetActive(bool active)
        {
            IsActive = active;

            Debug.Log(
                "PlayerFallDetector: " +
                (active ? "ACTIVATED" : "DEACTIVATED")
            );
        }


        // ============================================================
        // UPDATE
        // ============================================================

        private void Update()
        {
            if (!IsActive)
                return;

            if (isRespawning)
                return;

            CheckDeathGround();
        }


        // ============================================================
        // CHECK DEATH TERRAIN
        // ============================================================

        private void CheckDeathGround()
        {
            Vector3 origin = transform.position;

            if (controller != null)
            {
                origin.y += controller.center.y;
            }

            Ray ray = new Ray(origin, Vector3.down);

            if (showDebugRay)
            {
                Debug.DrawRay(
                    origin,
                    Vector3.down * detectionDistance,
                    Color.red
                );
            }

            if (Physics.Raycast(
                ray,
                out RaycastHit hit,
                detectionDistance,
                groundLayer,
                QueryTriggerInteraction.Ignore))
            {
                Debug.Log(
                    "PlayerFallDetector: Death Ground detected -> " +
                    hit.collider.gameObject.name
                );

                StartRespawn();
            }
        }


        // ============================================================
        // CHARACTER CONTROLLER COLLISION
        // ============================================================

        private void OnControllerColliderHit(
            ControllerColliderHit hit)
        {
            if (!IsActive)
                return;

            if (isRespawning)
                return;

            if (hit == null)
                return;

            if (!IsGroundLayer(hit.gameObject))
                return;

            Debug.Log(
                "PlayerFallDetector: Terrain collision detected -> " +
                hit.gameObject.name
            );

            StartRespawn();
        }


        // ============================================================
        // GROUND LAYER CHECK
        // ============================================================

        private bool IsGroundLayer(GameObject objectToCheck)
        {
            if (objectToCheck == null)
                return false;

            return
                (groundLayer.value &
                (1 << objectToCheck.layer)) != 0;
        }


        // ============================================================
        // START RESPAWN
        // ============================================================

        private void StartRespawn()
        {
            // Prevent multiple respawn calls.
            if (isRespawning)
                return;

            if (GameManager.Instance == null)
                return;

            if (!GameManager.Instance.RaceActive)
                return;

            StartCoroutine(RespawnCoroutine());
        }


        // ============================================================
        // RESPAWN COROUTINE
        // ============================================================

        private IEnumerator RespawnCoroutine()
        {
            isRespawning = true;

            Debug.Log(
                "PLAYER WILL RESPAWN IN " +
                respawnDelay +
                " SECONDS"
            );


            // Wait before respawning.
            yield return new WaitForSeconds(respawnDelay);


            // Check again after the delay.
            if (GameManager.Instance == null)
            {
                isRespawning = false;
                yield break;
            }

            if (!GameManager.Instance.RaceActive)
            {
                isRespawning = false;
                yield break;
            }


            // --------------------------------------------------------
            // GET RESPAWN POSITION
            // --------------------------------------------------------

            Vector3 respawnPosition =
                GameManager.Instance.GetRespawnPosition();

            Quaternion respawnRotation =
                GameManager.Instance.GetRespawnRotation();


            // --------------------------------------------------------
            // DISABLE CHARACTER CONTROLLER
            // --------------------------------------------------------

            if (controller != null)
                controller.enabled = false;


            // --------------------------------------------------------
            // TELEPORT
            // --------------------------------------------------------

            transform.SetPositionAndRotation(
                respawnPosition,
                respawnRotation
            );


            // --------------------------------------------------------
            // RESET VELOCITY
            // --------------------------------------------------------

            GameManager.Instance.ResetPlayerVelocity();


            // --------------------------------------------------------
            // ENABLE CHARACTER CONTROLLER
            // --------------------------------------------------------

            if (controller != null)
                controller.enabled = true;


            Debug.Log(
                "PLAYER RESPAWNED AT: " +
                respawnPosition
            );


            isRespawning = false;
        }
    }
}

