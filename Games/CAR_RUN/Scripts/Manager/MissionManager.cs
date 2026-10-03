using UnityEngine;

public class MissionManager : MonoBehaviour
{
    public static MissionManager Instance;

    [Header("References")]
    public GameObject boxPrefab;
    public ArrowPointer arrow;

    [Header("Car")]
    [SerializeField] private Transform car;

    [Header("Spawn Points")]
    public Transform[] boxSpawnPoints;

    [Header("Delivery Points")]
    public Transform[] deliveryPoints;

    [Header("Mission Timer")]
    [SerializeField] private float missionTime = 30f;

    [HideInInspector] public GameObject currentBox;
    [HideInInspector] public Transform currentDeliveryPoint;

    private bool carryingBox = false;
    public bool IsCarryingBox => carryingBox;

    private float currentTime;
    private bool missionTimerRunning = false;

    // ==========================
    // GAME STATISTICS
    // ==========================

    private int deliveriesCompleted = 0;
    private float totalDistanceTravelled = 0f;

    private Vector3 lastCarPosition;
    private bool trackingDistance = false;


    private void Awake()
    {
        Instance = this;
    }


    private void Start()
    {
        // Hide all delivery points
        foreach (Transform point in deliveryPoints)
        {
            point.gameObject.SetActive(false);
        }

        // Hide timer when game starts
        MissionUI.Instance.HideTimer();

        // Start distance tracking
        if (car != null)
        {
            lastCarPosition = car.position;
            trackingDistance = true;
        }
        else
        {
            Debug.LogWarning(
                "MissionManager: Car reference is not assigned!"
            );
        }

        // Spawn first box
        SpawnBox();
    }


    private void Update()
    {
        // ==========================
        // MISSION TIMER
        // ==========================

        if (!missionTimerRunning)
            return;

        // Countdown
        currentTime -= Time.deltaTime;

        // Prevent negative value
        if (currentTime < 0f)
            currentTime = 0f;

        // Update timer UI
        MissionUI.Instance.ShowTimer(currentTime);

        // Time finished
        if (currentTime <= 0f)
        {
            MissionFailed();
        }
    }


    private void FixedUpdate()
    {
        // ==========================
        // TRACK ACTUAL CAR DISTANCE
        // ==========================

        if (!trackingDistance || car == null)
            return;

        // Calculate movement since last physics frame
        float distance = Vector3.Distance(
            lastCarPosition,
            car.position
        );

        // Add to total distance
        totalDistanceTravelled += distance;

        // Save current position
        lastCarPosition = car.position;
    }


    public void SpawnBox()
    {
        // Cancel any previous timer
        missionTimerRunning = false;
        currentTime = 0f;
        carryingBox = false;

        // Hide timer
        MissionUI.Instance.HideTimer();

        // Destroy old box
        if (currentBox != null)
        {
            Destroy(currentBox);
            currentBox = null;
        }

        // Show pickup message
        MissionUI.Instance.ShowMessage(
            "Pickup The Box",
            MissionUI.Instance.goColor
        );

        // Select random spawn point
        int randomIndex = Random.Range(
            0,
            boxSpawnPoints.Length
        );

        Transform spawnPoint = boxSpawnPoints[randomIndex];

        // Spawn box
        currentBox = Instantiate(
            boxPrefab,
            spawnPoint.position,
            spawnPoint.rotation
        );

        // Point arrow to box
        if (arrow != null)
        {
            arrow.target = currentBox.transform;
        }

        Debug.Log("Box Spawned at: " + spawnPoint.name);
    }


    public void BoxPickedUp()
    {
        if (currentBox == null)
            return;

        if (carryingBox)
            return;

        carryingBox = true;

        // ==========================
        // START TIMER
        // ==========================

        currentTime = missionTime;
        missionTimerRunning = true;

        // Immediately show timer
        MissionUI.Instance.ShowTimer(currentTime);

        // Show mission message
        MissionUI.Instance.ShowMessage( "Reach To Destination", MissionUI.Instance.pickupColor);

        // Make box kinematic
        Rigidbody rb = currentBox.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // Hide all delivery points
        foreach (Transform point in deliveryPoints)
        {
            point.gameObject.SetActive(false);
        }

        // Select random delivery point
        int randomIndex = Random.Range(
            0,
            deliveryPoints.Length
        );

        currentDeliveryPoint = deliveryPoints[randomIndex];

        // Activate destination
        currentDeliveryPoint.gameObject.SetActive(true);

        // Point arrow to destination
        if (arrow != null)
        {
            arrow.target = currentDeliveryPoint;
        }

        Debug.Log("Deliver To: " + currentDeliveryPoint.name);

        Debug.Log("Mission Timer Started: " + missionTime + " seconds");
    }


    public void DeliveryComplete()
    {
        if (!carryingBox)
            return;

        // ==========================
        // COUNT DELIVERY
        // ==========================

        deliveriesCompleted++;

        // ==========================
        // STOP TIMER
        // ==========================

        missionTimerRunning = false;
        carryingBox = false;

        // Hide timer
        MissionUI.Instance.HideTimer();

        // Show success message
        MissionUI.Instance.ShowMessage("Delivery Complete!", MissionUI.Instance.deliveryColor);

        // Disable delivery point
        if (currentDeliveryPoint != null)
        {
            currentDeliveryPoint.gameObject.SetActive(false);
            currentDeliveryPoint = null;
        }

        // Remove arrow
        if (arrow != null)
        {
            arrow.target = null;
        }

        // Destroy delivered box
        if (currentBox != null)
        {
            Destroy(currentBox);
            currentBox = null;
        }

        Debug.Log("Delivery Complete! | " + "Deliveries: " + deliveriesCompleted + " | Total Distance: " + totalDistanceTravelled.ToString("F1") + " m"
        );

        // Wait before next mission
        Invoke(
            nameof(StartNextMission),
            2.5f
        );
    }


    private void MissionFailed()
    {
        if (!carryingBox)
            return;

        // ==========================
        // STOP TIMER
        // ==========================

        missionTimerRunning = false;
        carryingBox = false;
        currentTime = 0f;

        // Hide timer
        MissionUI.Instance.HideTimer();

        // Show failed message
        MissionUI.Instance.ShowMessage("MISSION FAILED!", Color.red);

        // Disable delivery point
        if (currentDeliveryPoint != null)
        {
            currentDeliveryPoint.gameObject.SetActive(false);
            currentDeliveryPoint = null;
        }

        // Remove arrow
        if (arrow != null)
        {
            arrow.target = null;
        }

        // Destroy box
        if (currentBox != null)
        {
            Destroy(currentBox);
            currentBox = null;
        }

        Debug.Log(
            "Mission Failed | " + "Deliveries: " + deliveriesCompleted + " | Total Distance: " + totalDistanceTravelled.ToString("F1") + " m");

        // Game Over Panel
        Invoke(nameof(Over), 2f);
    }


    private void Over()
    {
        GameManager.Instance.GameOver();
    }


    private void StartNextMission()
    {
        SpawnBox();
    }


    public void BoxDropped()
    {
        if (!carryingBox)
            return;

        // ==========================
        // STOP TIMER
        // ==========================

        missionTimerRunning = false;
        carryingBox = false;
        currentTime = 0f;

        // Hide timer
        MissionUI.Instance.HideTimer();

        // Show dropped message
        MissionUI.Instance.ShowMessage("Box Dropped!",Color.red);

        // Disable delivery point
        if (currentDeliveryPoint != null)
        {
            currentDeliveryPoint.gameObject.SetActive(false);
            currentDeliveryPoint = null;
        }

        // Remove arrow
        if (arrow != null)
        {
            arrow.target = null;
        }

        Debug.Log( "Mission Failed - Box Dropped" );

        // Respawn mission
        Invoke(
            nameof(RespawnMission),
            2f
        );
    }


    private void RespawnMission()
    {
        if (currentBox != null)
        {
            Destroy(currentBox);
            currentBox = null;
        }

        SpawnBox();
    }


    // ==========================
    // GETTERS
    // ==========================

    public float GetRemainingTime()
    {
        return currentTime;
    }


    public int GetDeliveriesCompleted()
    {
        return deliveriesCompleted;
    }


    public float GetTotalDistanceTravelled()
    {
        return totalDistanceTravelled;
    }
}