using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class CharacterControl : MonoBehaviour
{
    #region Inspector

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float runSpeed = 9f;
    [SerializeField] private float rotationSpeed = 10f;

    [Header("Air Movement")]
    [SerializeField] private float airControl = 0.35f;

    [Header("Jump")]
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float forwardJumpSpeed = 5f;

    [Header("Gravity")]
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float groundedForce = -2f;

    [Header("Animation")]
    [SerializeField] private float animationSmoothTime = 0.1f;

    [Header("Vault")]
    [SerializeField] private float vaultCheckDistance = 1.2f;
    [SerializeField] private float minVaultHeight = 0.3f;
    [SerializeField] private float maxVaultHeight = 1.2f;
    [SerializeField] private float vaultDuration = 0.7f;
    [SerializeField] private float vaultForwardDistance = 1.5f;
    [SerializeField] private float vaultArcHeight = 0.8f;
    [SerializeField] private LayerMask vaultLayer;

    [Header("Hang / Climb")]
    [SerializeField] private float climbCheckDistance = 1.0f;
    [SerializeField] private float climbMinHeight = 1.0f;
    [SerializeField] private float climbMaxHeight = 2.2f;
    [SerializeField] private float hangMoveSpeed = 1.5f;
    [SerializeField] private float climbDuration = 1.0f;
    [SerializeField] private float hangForwardOffset = 0.35f;
    [SerializeField] private float hangVerticalOffset = 1.0f;
    [SerializeField] private float climbForwardOffset = 0.5f;
    [SerializeField] private LayerMask climbLayer;

    [Header("Hang Validation")]
    [SerializeField] private float hangWallCheckHeight = 0.8f;
    [SerializeField] private float hangWallCheckDistance = 0.8f;
    [SerializeField] private float hangTopCheckHeight = 2.2f;
    [SerializeField] private float hangTopCheckForward = 0.15f;

    [Header("Hang Cooldown")]
    [SerializeField] private float hangCooldown = 0.25f;

    [Header("Slide")]
    [SerializeField] private float slideSpeed = 8f;
    [SerializeField] private float slideEndSpeed = 2f;
    [SerializeField] private float slideDuration = 1.0f;
    [SerializeField] private float slideHeight = 1.0f;
    [SerializeField] private float minimumSlideSpeed = 0.5f;
    [SerializeField] private float slideStartCooldown = 0.15f;

    #endregion


    #region State

    private CharacterController controller;
    private Animator animator;

    // ============================================================
    // INPUT
    // ============================================================

    private float moveX;
    private float moveY;

    // Joystick input
    private Vector2 joystickInput;

    // Mobile run toggle
    private bool mobileRun;

    // Mobile action buttons
    private bool jumpButtonPressed;
    private bool slideButtonPressed;


    // ============================================================
    // MOVEMENT
    // ============================================================

    private float verticalVelocity;
    private Vector3 horizontalVelocity;


    // ============================================================
    // GROUND
    // ============================================================

    private bool isGrounded;


    // ============================================================
    // ACTION FLAGS
    // ============================================================

    private bool isJumping;
    private bool isHanging;
    private bool isClimbing;
    private bool isVaulting;
    private bool isSliding;


    // ============================================================
    // COOLDOWNS
    // ============================================================

    private float hangCooldownTimer;
    private float slideCooldownTimer;


    // ============================================================
    // VAULT
    // ============================================================

    private float vaultTimer;
    private Vector3 vaultStartPosition;
    private Vector3 vaultEndPosition;


    // ============================================================
    // HANG
    // ============================================================

    private Vector3 hangPosition;
    private Vector3 hangWallNormal;
    private Vector3 hangLedgePosition;


    // ============================================================
    // CLIMB
    // ============================================================

    private float climbTimer;
    private Vector3 climbStartPosition;
    private Vector3 climbTargetPosition;
    private Vector3 climbClearancePosition;


    // ============================================================
    // SLIDE
    // ============================================================

    private float slideTimer;
    private float currentSlideSpeed;
    private float normalControllerHeight;
    private Vector3 normalControllerCenter;

    #endregion


    #region Unity Lifecycle

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        animator = GetComponentInChildren<Animator>();

        if (animator == null)
        {
            Debug.LogError(
                "Animator not found on Player or its children."
            );
        }

        normalControllerHeight = controller.height;
        normalControllerCenter = controller.center;
    }


    private void Update()
    {
        GetInput();

        UpdateHangCooldown();
        UpdateSlideCooldown();

        CheckGround();


        // ========================================================
        // HANG
        // ========================================================

        if (isHanging)
        {
            HandleHang();
        }


        // ========================================================
        // CLIMB
        // ========================================================

        else if (isClimbing)
        {
            HandleClimb();
        }


        // ========================================================
        // VAULT
        // ========================================================

        else if (isVaulting)
        {
            HandleVault();
        }


        // ========================================================
        // SLIDE
        // ========================================================

        else if (isSliding)
        {
            HandleSlide();
        }


        // ========================================================
        // NORMAL MOVEMENT
        // ========================================================

        else
        {
            HandleJump();

            CheckForHang();

            TryVault();

            CheckForSlide();

            HandleMovement();

            ApplyGravity();

            MoveCharacter();
        }


        UpdateAnimation();
    }

    #endregion


    #region Cooldowns

    private void UpdateHangCooldown()
    {
        if (hangCooldownTimer > 0f)
        {
            hangCooldownTimer -= Time.deltaTime;
        }
    }


    private void UpdateSlideCooldown()
    {
        if (slideCooldownTimer > 0f)
        {
            slideCooldownTimer -= Time.deltaTime;
        }
    }

    #endregion


    #region Input

    private void GetInput()
    {
        float keyboardX = 0f;
        float keyboardY = 0f;


        // ========================================================
        // KEYBOARD
        // ========================================================

        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed)
            {
                keyboardY += 1f;
            }

            if (Keyboard.current.sKey.isPressed)
            {
                keyboardY -= 1f;
            }

            if (Keyboard.current.aKey.isPressed)
            {
                keyboardX -= 1f;
            }

            if (Keyboard.current.dKey.isPressed)
            {
                keyboardX += 1f;
            }
        }


        Vector2 keyboardInput =
            new Vector2(
                keyboardX,
                keyboardY
            );


        if (keyboardInput.sqrMagnitude > 1f)
        {
            keyboardInput.Normalize();
        }


        // ========================================================
        // JOYSTICK / KEYBOARD
        // ========================================================

        if (joystickInput.sqrMagnitude > 0.001f)
        {
            // Joystick is being used
            moveX = joystickInput.x;
            moveY = joystickInput.y;
        }
        else
        {
            // No joystick movement
            // Use keyboard
            moveX = keyboardInput.x;
            moveY = keyboardInput.y;
        }
    }

    #endregion


    #region Mobile / UI Controls

    /// <summary>
    /// Set joystick movement.
    /// Call this from your joystick.
    /// </summary>
    public void SetJoystickInput(Vector2 input)
    {
        input = Vector2.ClampMagnitude(input, 1f);

        // Remove tiny joystick noise
        if (input.magnitude < 0.1f)
        {
            input = Vector2.zero;
        }

        joystickInput = input;
        
    }


    /// <summary>
    /// Clears joystick input.
    /// Call this when joystick is released.
    /// </summary>
    public void ClearJoystickInput()
    {
        joystickInput = Vector2.zero;
    }


    /// <summary>
    /// Toggle mobile Run ON/OFF.
    /// Use this with your Run button.
    /// </summary>
    public void ToggleRun()
    {
        mobileRun = !mobileRun;

        Debug.Log(
            "Mobile Run: " +
            (mobileRun ? "ON" : "OFF")
        );
    }


    /// <summary>
    /// Directly enable/disable Run.
    /// </summary>
    public void SetRun(bool enabled)
    {
        mobileRun = enabled;
    }


    /// <summary>
    /// Returns whether mobile Run is enabled.
    /// </summary>
    public bool IsRunEnabled()
    {
        return mobileRun;
    }


    /// <summary>
    /// Jump button.
    /// </summary>
    public void JumpButton()
    {
        jumpButtonPressed = true;
    }


    /// <summary>
    /// Slide button.
    /// </summary>
    public void SlideButton()
    {
        slideButtonPressed = true;
    }


    /// <summary>
    /// Optional reset for mobile controls.
    /// </summary>
    public void ResetMobileControls()
    {
        joystickInput = Vector2.zero;

        mobileRun = false;

        jumpButtonPressed = false;

        slideButtonPressed = false;
    }

    #endregion


    #region Ground Check

    private void CheckGround()
    {
        bool previousGrounded =
            isGrounded;


        // CharacterController handles
        // ground detection.
        isGrounded =
            controller.isGrounded;


        if (!previousGrounded &&
            isGrounded)
        {
            isJumping = false;

            verticalVelocity =
                groundedForce;
        }


        if (isGrounded &&
            verticalVelocity < 0f)
        {
            verticalVelocity =
                groundedForce;
        }
    }

    #endregion


    #region Movement

    private void HandleMovement()
    {
        Vector3 movementDirection =
            Vector3.zero;


        // ========================================================
        // CAMERA RELATIVE MOVEMENT
        // ========================================================

        if (Camera.main != null)
        {
            Vector3 cameraForward =
                Camera.main.transform.forward;

            Vector3 cameraRight =
                Camera.main.transform.right;


            cameraForward.y = 0f;

            cameraRight.y = 0f;


            cameraForward.Normalize();

            cameraRight.Normalize();


            movementDirection =
                cameraForward * moveY +
                cameraRight * moveX;
        }
        else
        {
            movementDirection =
                transform.forward * moveY +
                transform.right * moveX;
        }


        movementDirection.y = 0f;


        bool hasMovement =
            movementDirection.sqrMagnitude >
            0.001f;


        if (hasMovement)
        {
            movementDirection.Normalize();

            // ====================================================
            // ROTATION
            // ====================================================

            // FORWARD
            // W / Joystick UP
            if (moveY > 0.01f)
            {
                Quaternion targetRotation =
                    Quaternion.LookRotation(
                        movementDirection
                    );

                transform.rotation =
                    Quaternion.Slerp(
                        transform.rotation,
                        targetRotation,
                        rotationSpeed *
                        Time.deltaTime
                    );
            }

            // LEFT / RIGHT ONLY
            // A / D or Joystick LEFT / RIGHT
            //
            // IMPORTANT:
            // Only rotate for X movement when Y is almost ZERO.
            // This prevents joystick DOWN from rotating the player.
            else if (
                Mathf.Abs(moveY) <= 0.01f &&
                Mathf.Abs(moveX) > 0.01f
            )
            {
                Quaternion targetRotation =
                    Quaternion.LookRotation(
                        movementDirection
                    );

                transform.rotation =
                    Quaternion.Slerp(
                        transform.rotation,
                        targetRotation,
                        rotationSpeed *
                        Time.deltaTime
                    );
            }

            // BACKWARD
            // S / Joystick DOWN
            //
            // DO NOT ROTATE.
            // The character keeps facing forward
            // and moves backward.

        }


        // ========================================================
        // RUN
        // ========================================================

        bool keyboardRun = false;


        if (Keyboard.current != null)
        {
            keyboardRun =
                Keyboard.current.leftShiftKey.isPressed ||
                Keyboard.current.rightShiftKey.isPressed;
        }


        bool isRunning =
            (keyboardRun || mobileRun) &&
            hasMovement &&
            moveY > 0.01f;


        float currentSpeed =
            isRunning
                ? runSpeed
                : moveSpeed;


        Vector3 targetVelocity =
            movementDirection *
            currentSpeed;


        // ========================================================
        // GROUND / AIR CONTROL
        // ========================================================

        if (isGrounded)
        {
            horizontalVelocity =
                targetVelocity;
        }
        else
        {
            horizontalVelocity =
                Vector3.Lerp(
                    horizontalVelocity,
                    targetVelocity,
                    airControl *
                    Time.deltaTime
                );
        }
    }

    #endregion


    #region Jump

    private void HandleJump()
    {
        // ========================================================
        // KEYBOARD JUMP
        // ========================================================

        bool keyboardJump =
            Keyboard.current != null &&
            Keyboard.current.spaceKey
                .wasPressedThisFrame;


        // ========================================================
        // MOBILE JUMP
        // ========================================================

        bool jumpPressed =
            keyboardJump ||
            jumpButtonPressed;


        // Consume mobile jump input
        jumpButtonPressed = false;


        if (!jumpPressed)
        {
            return;
        }


        if (!isGrounded ||
            isJumping)
        {
            return;
        }


        // ========================================================
        // JUMP FORCE
        // ========================================================

        verticalVelocity =
            Mathf.Sqrt(
                jumpHeight *
                -2f *
                gravity
            );


        isJumping = true;


        // ========================================================
        // JUMP DIRECTION
        // ========================================================

        bool moving =
            Mathf.Abs(moveX) > 0.01f ||
            Mathf.Abs(moveY) > 0.01f;


        Vector3 jumpDirection;


        if (moving)
        {
            jumpDirection =
                GetCameraRelativeDirection(
                    moveX,
                    moveY
                );
        }
        else
        {
            jumpDirection =
                transform.forward;
        }


        horizontalVelocity =
            jumpDirection *
            forwardJumpSpeed;


        animator?.SetTrigger("Jump");
    }

    #endregion


    #region Camera Relative Direction

    private Vector3 GetCameraRelativeDirection(
        float x,
        float y)
    {
        if (Camera.main == null)
        {
            Vector3 fallback =
                new Vector3(
                    x,
                    0f,
                    y
                );


            return fallback.sqrMagnitude >
                   0.001f
                ? fallback.normalized
                : transform.forward;
        }


        Vector3 forward =
            Camera.main.transform.forward;

        Vector3 right =
            Camera.main.transform.right;


        forward.y = 0f;

        right.y = 0f;


        forward.Normalize();

        right.Normalize();


        Vector3 direction =
            forward * y +
            right * x;


        direction.y = 0f;


        if (direction.sqrMagnitude <
            0.001f)
        {
            return transform.forward;
        }


        return direction.normalized;
    }

    #endregion


    #region Hang Detection & Entry

    private void CheckForHang()
    {
        if (isGrounded ||
            isHanging ||
            isClimbing ||
            isVaulting ||
            isSliding)
        {
            return;
        }


        if (hangCooldownTimer > 0f)
        {
            return;
        }


        Vector3 direction =
            transform.forward;


        Vector3 wallCheckOrigin =
            transform.position +
            Vector3.up * 1.0f;


        if (!Physics.Raycast(
                wallCheckOrigin,
                direction,
                out RaycastHit wallHit,
                climbCheckDistance,
                climbLayer,
                QueryTriggerInteraction.Ignore))
        {
            return;
        }


        Vector3 topCheckOrigin =
            wallHit.point +
            direction * 0.3f +
            Vector3.up *
            climbMaxHeight;


        if (!Physics.Raycast(
                topCheckOrigin,
                Vector3.down,
                out RaycastHit topHit,
                climbMaxHeight + 0.5f,
                climbLayer,
                QueryTriggerInteraction.Ignore))
        {
            return;
        }


        float ledgeHeight =
            topHit.point.y -
            transform.position.y;


        if (ledgeHeight < climbMinHeight ||
            ledgeHeight > climbMaxHeight)
        {
            return;
        }


        StartHang(
            topHit.point,
            wallHit.normal
        );
    }


    private void StartHang(
        Vector3 ledgePosition,
        Vector3 wallNormal)
    {
        isHanging = true;


        hangLedgePosition =
            ledgePosition;


        hangWallNormal =
            wallNormal;


        horizontalVelocity =
            Vector3.zero;


        verticalVelocity = 0f;

        isJumping = false;


        Vector3 facingDirection =
            -wallNormal;


        facingDirection.y = 0f;


        if (facingDirection.sqrMagnitude >
            0.01f)
        {
            transform.rotation =
                Quaternion.LookRotation(
                    facingDirection
                );
        }


        hangPosition =
            ledgePosition
            - transform.forward *
              hangForwardOffset
            - Vector3.up *
              hangVerticalOffset;


        controller.Move(
            hangPosition -
            transform.position
        );


        if (animator != null)
        {
            animator.SetFloat(
                "HangMove",
                0f
            );

            animator.SetTrigger(
                "Hang"
            );
        }
    }

    #endregion


    #region Hang Update

    private void HandleHang()
    {
        horizontalVelocity =
            Vector3.zero;


        verticalVelocity = 0f;


        if (!IsHangStillValid())
        {
            DropFromHang();

            return;
        }


        // ========================================================
        // DROP
        // ========================================================

        if (Keyboard.current != null &&
            Keyboard.current.sKey
                .wasPressedThisFrame)
        {
            DropFromHang();

            return;
        }


        // ========================================================
        // CLIMB
        // ========================================================

        if (Keyboard.current != null &&
            Keyboard.current.wKey
                .wasPressedThisFrame)
        {
            StartClimb();

            return;
        }


        // ========================================================
        // HANG MOVEMENT
        // ========================================================

        float hangMove =
            moveX;


        animator?.SetFloat(
            "HangMove",
            hangMove,
            animationSmoothTime,
            Time.deltaTime
        );


        if (Mathf.Abs(hangMove) >
            0.01f)
        {
            MoveAlongLedge(
                hangMove
            );
        }
    }


    private bool IsHangStillValid()
    {
        Vector3 direction =
            transform.forward;


        Vector3 wallOrigin =
            transform.position +
            Vector3.up *
            hangWallCheckHeight;


        if (!Physics.Raycast(
                wallOrigin,
                direction,
                out RaycastHit wallHit,
                hangWallCheckDistance,
                climbLayer,
                QueryTriggerInteraction.Ignore))
        {
            return false;
        }


        Vector3 topOrigin =
            wallHit.point +
            direction *
            hangTopCheckForward +
            Vector3.up *
            hangTopCheckHeight;


        if (!Physics.Raycast(
                topOrigin,
                Vector3.down,
                out RaycastHit topHit,
                hangTopCheckHeight + 0.5f,
                climbLayer,
                QueryTriggerInteraction.Ignore))
        {
            return false;
        }


        float ledgeHeight =
            topHit.point.y -
            transform.position.y;


        if (ledgeHeight < climbMinHeight ||
            ledgeHeight > climbMaxHeight)
        {
            return false;
        }


        hangLedgePosition =
            topHit.point;


        hangWallNormal =
            wallHit.normal;


        return true;
    }


    private void MoveAlongLedge(
        float direction)
    {
        Vector3 wallRight =
            Vector3.Cross(
                hangWallNormal,
                Vector3.up
            ).normalized;


        Vector3 movement =
            wallRight *
            direction *
            hangMoveSpeed *
            Time.deltaTime;


        Vector3 newPosition =
            transform.position +
            movement;


        if (!CanHangAtPosition(
                newPosition))
        {
            DropFromHang();

            return;
        }


        controller.Move(
            movement
        );
    }


    private bool CanHangAtPosition(
        Vector3 position)
    {
        Vector3 direction =
            transform.forward;


        Vector3 wallOrigin =
            position +
            Vector3.up *
            hangWallCheckHeight;


        if (!Physics.Raycast(
                wallOrigin,
                direction,
                out RaycastHit wallHit,
                hangWallCheckDistance,
                climbLayer,
                QueryTriggerInteraction.Ignore))
        {
            return false;
        }


        Vector3 topOrigin =
            wallHit.point +
            direction *
            hangTopCheckForward +
            Vector3.up *
            hangTopCheckHeight;


        if (!Physics.Raycast(
                topOrigin,
                Vector3.down,
                out RaycastHit topHit,
                hangTopCheckHeight + 0.5f,
                climbLayer,
                QueryTriggerInteraction.Ignore))
        {
            return false;
        }


        float ledgeHeight =
            topHit.point.y -
            position.y;


        return ledgeHeight >=
               climbMinHeight &&
               ledgeHeight <=
               climbMaxHeight;
    }


    private void DropFromHang()
    {
        isHanging = false;


        hangCooldownTimer =
            hangCooldown;


        verticalVelocity = 0f;

        isJumping = true;


        if (animator != null)
        {
            animator.ResetTrigger(
                "Climb"
            );

            animator.ResetTrigger(
                "Hang"
            );

            animator.SetTrigger(
                "Drop"
            );
        }
    }

    #endregion


    #region Climb

    private void StartClimb()
    {
        isHanging = false;

        isClimbing = true;


        climbTimer = 0f;


        climbStartPosition =
            transform.position;


        Vector3 forward =
            -hangWallNormal;


        forward.y = 0f;


        if (forward.sqrMagnitude <
            0.001f)
        {
            forward =
                transform.forward;

            forward.y = 0f;
        }


        forward.Normalize();


        float controllerBottomOffset =
            (controller.height * 0.5f) -
            controller.center.y;


        climbTargetPosition =
            hangLedgePosition +
            forward *
            climbForwardOffset;


        climbTargetPosition.y =
            hangLedgePosition.y +
            controllerBottomOffset;


        climbClearancePosition =
            climbTargetPosition;


        climbClearancePosition.y +=
            0.08f;


        horizontalVelocity =
            Vector3.zero;


        verticalVelocity = 0f;


        if (animator != null)
        {
            animator.ResetTrigger(
                "Hang"
            );

            animator.SetTrigger(
                "Climb"
            );
        }
    }


    private void HandleClimb()
    {
        climbTimer +=
            Time.deltaTime;


        float t =
            Mathf.Clamp01(
                climbTimer /
                climbDuration
            );


        float upT =
            Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(
                    0f,
                    0.6f,
                    t
                )
            );


        float forwardT =
            Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(
                    0.25f,
                    1f,
                    t
                )
            );


        float x =
            Mathf.Lerp(
                climbStartPosition.x,
                climbTargetPosition.x,
                forwardT
            );


        float z =
            Mathf.Lerp(
                climbStartPosition.z,
                climbTargetPosition.z,
                forwardT
            );


        float risingY =
            Mathf.Lerp(
                climbStartPosition.y,
                climbClearancePosition.y,
                upT
            );


        float y =
            Mathf.Lerp(
                risingY,
                climbTargetPosition.y,
                forwardT
            );


        Vector3 desiredPosition =
            new Vector3(
                x,
                y,
                z
            );


        controller.Move(
            desiredPosition -
            transform.position
        );


        if (t >= 1f)
        {
            FinishClimb();
        }
    }


    private void FinishClimb()
    {
        controller.enabled = false;


        transform.position =
            climbTargetPosition;


        controller.enabled = true;


        isClimbing = false;


        climbTimer = 0f;


        horizontalVelocity =
            Vector3.zero;


        verticalVelocity =
            groundedForce;


        isJumping = false;

        isGrounded = true;


        animator?.ResetTrigger(
            "Climb"
        );
    }

    #endregion


    #region Vault

    private void TryVault()
    {
        if (!isGrounded ||
            isVaulting ||
            isClimbing ||
            isHanging ||
            isSliding)
        {
            return;
        }


        if (moveY <= 0.1f)
        {
            return;
        }


        Vector3 direction =
            transform.forward;


        Vector3 origin =
            transform.position +
            Vector3.up * 0.1f;


        if (!Physics.Raycast(
                origin,
                direction,
                out RaycastHit obstacleHit,
                vaultCheckDistance,
                vaultLayer,
                QueryTriggerInteraction.Ignore))
        {
            return;
        }


        Vector3 topCheckStart =
            obstacleHit.point +
            Vector3.up *
            maxVaultHeight;


        if (!Physics.Raycast(
                topCheckStart,
                Vector3.down,
                out RaycastHit topHit,
                maxVaultHeight + 0.5f,
                vaultLayer,
                QueryTriggerInteraction.Ignore))
        {
            return;
        }


        float obstacleHeight =
            topHit.point.y -
            transform.position.y;


        if (obstacleHeight <
                minVaultHeight ||
            obstacleHeight >
                maxVaultHeight)
        {
            return;
        }


        Vector3 landingPosition =
            topHit.point +
            direction *
            vaultForwardDistance;


        Vector3 checkPosition =
            landingPosition +
            Vector3.up * 0.5f;


        if (Physics.CheckSphere(
                checkPosition,
                0.3f,
                vaultLayer,
                QueryTriggerInteraction.Ignore))
        {
            return;
        }


        StartVault(
            landingPosition
        );
    }


    private void StartVault(
        Vector3 landingPosition)
    {
        isVaulting = true;


        vaultTimer = 0f;


        vaultStartPosition =
            transform.position;


        vaultEndPosition =
            landingPosition;


        horizontalVelocity =
            Vector3.zero;


        verticalVelocity = 0f;


        animator?.SetTrigger(
            "Vault"
        );
    }


    private void HandleVault()
    {
        vaultTimer +=
            Time.deltaTime;


        float t =
            Mathf.Clamp01(
                vaultTimer /
                vaultDuration
            );


        float smoothT =
            Mathf.SmoothStep(
                0f,
                1f,
                t
            );


        Vector3 desiredPosition =
            Vector3.Lerp(
                vaultStartPosition,
                vaultEndPosition,
                smoothT
            );


        desiredPosition.y +=
            Mathf.Sin(
                t * Mathf.PI
            ) *
            vaultArcHeight;


        controller.Move(
            desiredPosition -
            transform.position
        );


        if (t >= 1f)
        {
            FinishVault();
        }
    }


    private void FinishVault()
    {
        isVaulting = false;


        vaultTimer = 0f;


        horizontalVelocity =
            Vector3.zero;


        verticalVelocity =
            groundedForce;


        isJumping = false;
    }

    #endregion


    #region Slide

    private void CheckForSlide()
    {
        if (isSliding ||
            isHanging ||
            isClimbing ||
            isVaulting)
        {
            return;
        }


        if (!isGrounded)
        {
            return;
        }


        if (slideCooldownTimer > 0f)
        {
            return;
        }


        if (horizontalVelocity.magnitude <
            minimumSlideSpeed)
        {
            return;
        }


        // ========================================================
        // KEYBOARD SLIDE
        // ========================================================

        bool keyboardSlide = false;


        if (Keyboard.current != null)
        {
            keyboardSlide =
                Keyboard.current.leftCtrlKey
                    .wasPressedThisFrame ||
                Keyboard.current.rightCtrlKey
                    .wasPressedThisFrame;
        }


        // ========================================================
        // MOBILE SLIDE
        // ========================================================

        bool slidePressed =
            keyboardSlide ||
            slideButtonPressed;


        // Consume mobile input
        slideButtonPressed = false;


        if (!slidePressed)
        {
            return;
        }


        StartSlide();
    }


    private void StartSlide()
    {
        if (isSliding)
        {
            return;
        }


        float minimumValidHeight =
            controller.radius * 2f +
            0.05f;


        float actualSlideHeight =
            Mathf.Max(
                slideHeight,
                minimumValidHeight
            );


        isSliding = true;


        slideTimer = 0f;


        currentSlideSpeed =
            Mathf.Max(
                horizontalVelocity.magnitude,
                slideSpeed
            );


        Vector3 slideCenter =
            normalControllerCenter;


        slideCenter.y +=
            (actualSlideHeight -
             normalControllerHeight) *
            0.5f;


        controller.height =
            actualSlideHeight;


        controller.center =
            slideCenter;


        Vector3 slideDirection =
            transform.forward;


        slideDirection.y = 0f;


        if (slideDirection.sqrMagnitude <
            0.001f)
        {
            slideDirection =
                Vector3.forward;
        }


        slideDirection.Normalize();


        horizontalVelocity =
            slideDirection *
            currentSlideSpeed;


        verticalVelocity =
            groundedForce;


        animator?.SetBool(
            "IsSliding",
            true
        );
    }


    private void HandleSlide()
    {
        slideTimer +=
            Time.deltaTime;


        float t =
            Mathf.Clamp01(
                slideTimer /
                slideDuration
            );


        currentSlideSpeed =
            Mathf.Lerp(
                slideSpeed,
                slideEndSpeed,
                t
            );


        Vector3 direction =
            transform.forward;


        direction.y = 0f;


        if (direction.sqrMagnitude >
            0.001f)
        {
            direction.Normalize();
        }


        horizontalVelocity =
            direction *
            currentSlideSpeed;


        verticalVelocity =
            groundedForce;


        Vector3 movement =
            horizontalVelocity;


        movement.y =
            verticalVelocity;


        controller.Move(
            movement *
            Time.deltaTime
        );


        if (slideTimer >=
            slideDuration)
        {
            StopSlide();
        }
    }


    private void StopSlide()
    {
        if (!isSliding)
        {
            return;
        }


        controller.height =
            normalControllerHeight;


        controller.center =
            normalControllerCenter;


        isSliding = false;


        slideTimer = 0f;

        currentSlideSpeed = 0f;


        horizontalVelocity *=
            0.5f;


        isJumping = false;

        isGrounded = true;


        verticalVelocity =
            groundedForce;


        slideCooldownTimer =
            slideStartCooldown;


        animator?.SetBool(
            "IsSliding",
            false
        );
    }


    [ContextMenu("Reset Controller To Normal")]
    private void ResetControllerToNormal()
    {
        if (controller == null)
        {
            controller =
                GetComponent<CharacterController>();
        }


        if (controller == null)
        {
            return;
        }


        controller.height =
            normalControllerHeight;


        controller.center =
            normalControllerCenter;


        isSliding = false;


        slideTimer = 0f;

        currentSlideSpeed = 0f;


        isJumping = false;

        isGrounded = true;


        verticalVelocity =
            groundedForce;


        animator?.SetBool(
            "IsSliding",
            false
        );
    }

    #endregion


    #region Gravity & Movement

    private void ApplyGravity()
    {
        if (!isGrounded)
        {
            verticalVelocity +=
                gravity *
                Time.deltaTime;
        }
    }


    private void MoveCharacter()
    {
        Vector3 movement =
            horizontalVelocity;


        movement.y =
            verticalVelocity;


        controller.Move(
            movement *
            Time.deltaTime
        );
    }

    #endregion


    #region Animation

    private void UpdateAnimation()
    {
        if (animator == null)
        {
            return;
        }


        float normalizedSpeed =
            Mathf.Clamp01(
                horizontalVelocity.magnitude /
                runSpeed
            );


        animator.SetFloat(
            "MoveSpeed",
            normalizedSpeed,
            animationSmoothTime,
            Time.deltaTime
        );


        animator.SetBool(
            "IsGrounded",
            isGrounded
        );


        animator.SetBool(
            "IsSliding",
            isSliding
        );
    }

    #endregion
}