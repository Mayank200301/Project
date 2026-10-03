using UnityEngine;

public class FollowCamera : MonoBehaviour
{
    [Header("Normal Follow Camera")]
    public Transform target;

    public Vector3 offset = new Vector3(0f, 5f, -8f);
    public float smoothSpeed = 5f;
    public bool lookAtTarget = true;

    [Header("Camera Button")]
    [SerializeField] private GameObject cameraViewIcon;

    [Header("Car View")]
    public Transform carViewPivot;

    public float carViewDistance = 6f;
    public float carViewRotationSpeed = 0.3f;

    public float minVerticalAngle = -10f;
    public float maxVerticalAngle = 60f;

    [Header("Car View Smoothness")]
    public float carViewSmoothSpeed = 8f;

    // Camera mode
    private bool viewingCar = false;

    // Car view rotation
    private float horizontalAngle;
    private float verticalAngle = 15f;


    private void Start()
    {
        // Start in normal camera mode
        viewingCar = false;

        // Hide second icon
        if (cameraViewIcon != null)
            cameraViewIcon.SetActive(false);
    }


    private void LateUpdate()
    {
        if (target == null)
            return;

        if (viewingCar)
        {
            CarViewCamera();
        }
        else
        {
            FollowCar();
        }
    }


    // =========================================================
    // NORMAL FOLLOW CAMERA
    // =========================================================

    private void FollowCar()
    {
        Vector3 desiredPosition =
            target.position + target.TransformDirection(offset);

        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            smoothSpeed * Time.deltaTime
        );

        if (lookAtTarget)
        {
            transform.LookAt(target);
        }
    }


    // =========================================================
    // CAR VIEW CAMERA
    // =========================================================

    private void CarViewCamera()
    {
        if (carViewPivot == null)
            return;

        RotateInput();

        Quaternion rotation = Quaternion.Euler(
            verticalAngle,
            horizontalAngle,
            0f
        );

        Vector3 desiredPosition =
            carViewPivot.position +
            rotation * Vector3.back * carViewDistance;

        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            carViewSmoothSpeed * Time.deltaTime
        );

        transform.LookAt(carViewPivot.position);
    }


    // =========================================================
    // MOUSE + TOUCH INPUT
    // =========================================================

    private void RotateInput()
    {
        // -------------------------
        // PC MOUSE
        // -------------------------

        if (Input.GetMouseButton(0))
        {
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");

            horizontalAngle +=
                mouseX * carViewRotationSpeed * 10f;

            verticalAngle -=
                mouseY * carViewRotationSpeed * 10f;
        }


        // -------------------------
        // MOBILE TOUCH
        // -------------------------

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Moved)
            {
                Vector2 delta = touch.deltaPosition;

                horizontalAngle +=
                    delta.x * carViewRotationSpeed;

                verticalAngle -=
                    delta.y * carViewRotationSpeed;
            }
        }


        verticalAngle = Mathf.Clamp(
            verticalAngle,
            minVerticalAngle,
            maxVerticalAngle
        );
    }


    // =========================================================
    // CAMERA BUTTON
    // =========================================================

    public void ToggleCarView()
    {
        if (carViewPivot == null)
            return;

        // Switch camera mode
        viewingCar = !viewingCar;


        if (viewingCar)
        {
            // Start behind the car
            horizontalAngle =
                target.eulerAngles.y + 180f;

            verticalAngle = 15f;

            // Show second camera icon
            if (cameraViewIcon != null)
                cameraViewIcon.SetActive(true);
        }
        else
        {
            // Return to normal follow camera

            // Hide second camera icon
            if (cameraViewIcon != null)
                cameraViewIcon.SetActive(false);
        }
    }
}