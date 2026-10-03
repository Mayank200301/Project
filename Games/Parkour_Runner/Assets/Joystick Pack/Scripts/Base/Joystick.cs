using UnityEngine;
using UnityEngine.EventSystems;

public class Joystick : MonoBehaviour,
    IPointerDownHandler,
    IDragHandler,
    IPointerUpHandler
{
    public float Horizontal
    {
        get
        {
            return snapX
                ? SnapFloat(input.x, AxisOptions.Horizontal)
                : input.x;
        }
    }

    public float Vertical
    {
        get
        {
            return snapY
                ? SnapFloat(input.y, AxisOptions.Vertical)
                : input.y;
        }
    }

    public Vector2 Direction
    {
        get
        {
            return new Vector2(Horizontal, Vertical);
        }
    }

    public float HandleRange
    {
        get { return handleRange; }
        set { handleRange = Mathf.Abs(value); }
    }

    public float DeadZone
    {
        get { return deadZone; }
        set { deadZone = Mathf.Abs(value); }
    }

    // FIXED
    public AxisOptions AxisOptions
    {
        get { return axisOptions; }
        set { axisOptions = value; }
    }

    public bool SnapX
    {
        get { return snapX; }
        set { snapX = value; }
    }

    public bool SnapY
    {
        get { return snapY; }
        set { snapY = value; }
    }


    // ============================================================
    // JOYSTICK SETTINGS
    // ============================================================

    [Header("Joystick Settings")]

    [SerializeField]
    private float handleRange = 1f;

    [SerializeField]
    private float deadZone = 0f;

    [SerializeField]
    private AxisOptions axisOptions = AxisOptions.Both;

    [SerializeField]
    private bool snapX = false;

    [SerializeField]
    private bool snapY = false;


    // ============================================================
    // REFERENCES
    // ============================================================

    [Header("References")]

    [SerializeField]
    protected RectTransform background = null;

    [SerializeField]
    private RectTransform handle = null;


    // ============================================================
    // PLAYER CONNECTION
    // ============================================================

    [Header("Player Connection")]

    [SerializeField]
    private CharacterControl playerController;


    // ============================================================
    // INTERNAL VARIABLES
    // ============================================================

    private RectTransform baseRect;
    private Canvas canvas;
    private Camera cam;

    private Vector2 input = Vector2.zero;


    // ============================================================
    // START
    // ============================================================

    protected virtual void Start()
    {
        HandleRange = handleRange;
        DeadZone = deadZone;

        baseRect = GetComponent<RectTransform>();

        canvas = GetComponentInParent<Canvas>();

        if (canvas == null)
        {
            Debug.LogError(
                "Joystick: The Joystick is not placed inside a Canvas."
            );

            return;
        }

        if (background == null)
        {
            Debug.LogError(
                "Joystick: Background is not assigned."
            );

            return;
        }

        if (handle == null)
        {
            Debug.LogError(
                "Joystick: Handle is not assigned."
            );

            return;
        }


        Vector2 center = new Vector2(0.5f, 0.5f);

        background.pivot = center;

        handle.anchorMin = center;
        handle.anchorMax = center;
        handle.pivot = center;

        handle.anchoredPosition = Vector2.zero;


        // ------------------------------------------------------------
        // AUTO FIND PLAYER CONTROLLER
        // ------------------------------------------------------------

        if (playerController == null)
        {
            playerController =
                FindFirstObjectByType<CharacterControl>();
        }

        if (playerController == null)
        {
            Debug.LogWarning(
                "Joystick: CharacterControl was not found. " +
                "Assign Player Controller manually in the Inspector."
            );
        }
    }


    // ============================================================
    // POINTER DOWN
    // ============================================================

    public virtual void OnPointerDown(
        PointerEventData eventData)
    {
        OnDrag(eventData);
    }


        public void OnDrag(PointerEventData eventData)
        {
            if (canvas == null || background == null || handle == null)
                return;

            // --------------------------------------------------------
            // CAMERA
            // --------------------------------------------------------

            cam = null;

            if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
                cam = canvas.worldCamera;

            // --------------------------------------------------------
            // GET JOYSTICK CENTER
            // --------------------------------------------------------

            Vector2 joystickCenter =
                RectTransformUtility.WorldToScreenPoint(
                    cam,
                    background.position
                );

            // --------------------------------------------------------
            // CALCULATE RADIUS
            // --------------------------------------------------------

            float radius =
                background.rect.width * canvas.scaleFactor * 0.5f;

            if (radius <= 0f)
                return;

            // --------------------------------------------------------
            // CALCULATE INPUT
            // --------------------------------------------------------

            Vector2 offset =
                eventData.position - joystickCenter;

            input = offset / radius;

            // IMPORTANT:
            // DOWN = NEGATIVE Y
            // UP   = POSITIVE Y
            //
            // S key equivalent:
            // joystick down -> input.y < 0
            // --------------------------------------------------------

            input = Vector2.ClampMagnitude(input, 1f);

            // --------------------------------------------------------
            // AXIS
            // --------------------------------------------------------

            FormatInput();

            // --------------------------------------------------------
            // DEAD ZONE
            // --------------------------------------------------------

            if (input.magnitude < deadZone)
            {
                input = Vector2.zero;
            }

            // --------------------------------------------------------
            // MOVE HANDLE
            // --------------------------------------------------------

            handle.anchoredPosition =
                input * background.rect.width * 0.5f * handleRange;

            // --------------------------------------------------------
            // SEND TO PLAYER
            // --------------------------------------------------------

            if (playerController != null)
            {
                playerController.SetJoystickInput(input);
            }
        }


    // ============================================================
    // HANDLE INPUT
    // ============================================================

    protected virtual void HandleInput(
        float magnitude,
        Vector2 normalised,
        Vector2 radius,
        Camera cam)
    {
        if (magnitude > deadZone)
        {
            if (magnitude > 1f)
            {
                input = normalised;
            }
        }
        else
        {
            input = Vector2.zero;
        }
    }


    // ============================================================
    // FORMAT INPUT
    // ============================================================

    private void FormatInput()
    {
        if (axisOptions == AxisOptions.Horizontal)
        {
            input = new Vector2(
                input.x,
                0f
            );
        }
        else if (axisOptions == AxisOptions.Vertical)
        {
            input = new Vector2(
                0f,
                input.y
            );
        }
    }


    // ============================================================
    // SNAP FLOAT
    // ============================================================

    private float SnapFloat(
        float value,
        AxisOptions snapAxis)
    {
        if (value == 0)
            return value;


        if (axisOptions == AxisOptions.Both)
        {
            float angle =
                Vector2.Angle(
                    input,
                    Vector2.up
                );


            if (snapAxis == AxisOptions.Horizontal)
            {
                if (angle < 22.5f ||
                    angle > 157.5f)
                {
                    return 0;
                }
                else
                {
                    return value > 0 ? 1 : -1;
                }
            }


            if (snapAxis == AxisOptions.Vertical)
            {
                if (angle > 67.5f &&
                    angle < 112.5f)
                {
                    return 0;
                }
                else
                {
                    return value > 0 ? 1 : -1;
                }
            }


            return value;
        }


        if (value > 0)
            return 1;

        if (value < 0)
            return -1;


        return 0;
    }


    // ============================================================
    // POINTER UP
    // ============================================================

    public virtual void OnPointerUp(
        PointerEventData eventData)
    {
        // Reset joystick input
        input = Vector2.zero;


        // Reset handle position
        if (handle != null)
        {
            handle.anchoredPosition =
                Vector2.zero;
        }


        // ------------------------------------------------------------
        // TELL PLAYER TO STOP JOYSTICK INPUT
        // ------------------------------------------------------------

        if (playerController != null)
        {
            playerController.ClearJoystickInput();
        }
    }


    // ============================================================
    // SCREEN POINT TO ANCHORED POSITION
    // ============================================================

    protected Vector2 ScreenPointToAnchoredPosition(
        Vector2 screenPosition)
    {
        Vector2 localPoint = Vector2.zero;


        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            baseRect,
            screenPosition,
            cam,
            out localPoint))
        {
            Vector2 pivotOffset =
                baseRect.pivot *
                baseRect.sizeDelta;


            return localPoint
                - (background.anchorMax *
                   baseRect.sizeDelta)
                + pivotOffset;
        }


        return Vector2.zero;
    }
}


// ================================================================
// AXIS OPTIONS
// ================================================================

public enum AxisOptions
{
    Both,
    Horizontal,
    Vertical
}