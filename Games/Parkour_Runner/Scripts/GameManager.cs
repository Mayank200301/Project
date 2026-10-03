
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.Collections;

namespace OMG.Parkour
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }


        [Header("Player Mobile Controls")]
        [SerializeField] private CharacterControl playerController;

        [Header("Joystick")]
        [SerializeField] private Joystick joystick;

        [Header("Run Button")]
        [SerializeField] private GameObject runOnIcon;
        [SerializeField] private GameObject runOffIcon;

        private bool runEnabled = false;

        [Header("Player Controller Auto Find")]
        [SerializeField] private bool autoFindPlayerController = true;

        // ============================================================
        // PLAYER
        // ============================================================

        [Header("Player")]
        public Transform player;


        // ============================================================
        // LEVEL START POSITIONS
        // ============================================================

        [Header("Level Start Positions")]
        [Tooltip("Element 0 = Level 1, Element 1 = Level 2, etc.")]
        public Transform[] levelStartPositions;

        // ============================================================
        // RESTART / EXPLORE POSITIONS
        // ============================================================

        [Header("Restart / Explore")]

        [Tooltip("Player will move here when Restart Race is pressed.")]
        [SerializeField] private Transform restartRacePosition;

        [Tooltip("Player will move here when Explore is pressed.")]
        [SerializeField] private Transform explorePosition;


        // ============================================================
        // TIMER
        // ============================================================

        [Header("Timer")]
        public TMP_Text timerText;

        // ============================================================
        // GAME OVER / SCORE UI
        // ============================================================

        [Header("Game Over UI")]

        [SerializeField]
        private GameObject gameOverPanel;

        [SerializeField]
        private TMP_Text currentScoreText;

        [SerializeField]
        private TMP_Text highScoreText;

        // PlayerPrefs key for saving the best race time
        private const string HighScoreKey = "Parkore_HighScore";

        // ============================================================
        // LEVEL MESSAGE UI
        // ============================================================

        [Header("Level Message UI")]
        [SerializeField] private TMP_Text levelMessageText;

        [SerializeField] private float levelMessageDuration = 2f;

        // ============================================================
        // SHOW LEVEL MESSAGE
        // ============================================================

        private Coroutine levelMessageCoroutine;

        [SerializeField] private float fadeInDuration = 0.5f;
        [SerializeField] private float fadeOutDuration = 0.5f;

        // ============================================================
        // PAUSE / SETTINGS UI
        // ============================================================

        [Header("Pause & Settings UI")]
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject settingPanel;

        // ============================================================
        // AUDIO
        // ============================================================

        [Header("Audio")]

        [SerializeField] private AudioSource musicSource;

        [SerializeField] private AudioSource soundSource;


        // ============================================================
        // AUDIO ICONS
        // ============================================================

        [Header("Audio Icons")]

        [SerializeField] private GameObject musicOnIcon;
        [SerializeField] private GameObject musicOffIcon;

        [SerializeField] private GameObject soundOnIcon;
        [SerializeField] private GameObject soundOffIcon;


        // ============================================================
        // AUDIO STATE
        // ============================================================

        private bool musicEnabled = true;
        private bool soundEnabled = true;


        // ============================================================
        // PLAYER FALL DETECTOR
        // ============================================================

        [Header("Player Fall Detector")]
        public PlayerFallDetector fallDetector;


        // ============================================================
        // TELEPORT
        // ============================================================

        [Header("Teleport")]

        [Tooltip(
            "GameObject where the player enters the trigger. " +
            "This GameObject must have a Collider with Is Trigger enabled."
        )]
        [SerializeField]
        private GameObject teleportFrom;

        [Tooltip(
            "GameObject where the player will be teleported."
        )]
        [SerializeField]
        private GameObject teleportDestination;

        [Tooltip(
            "Only objects with this tag can activate the teleport."
        )]
        [SerializeField]
        private string teleportPlayerTag = "Player";


        // ============================================================
        // GAME STATE
        // ============================================================

        public bool RaceActive { get; private set; }

        public float ElapsedTime { get; private set; }

        public int CurrentLevel { get; private set; } = -1;


        // ============================================================
        // CHECKPOINT
        // ============================================================

        private ParkourCheckpoint currentCheckpoint;

        private readonly List<ParkourCheckpoint> checkpoints =
            new List<ParkourCheckpoint>();


        // ============================================================
        // AWAKE
        // ============================================================

        private void Awake()
        {
            Time.timeScale = 1f;
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            SetupTeleportTrigger();
            SetupPlayerController();
        }


        // ============================================================
        // SETUP TELEPORT TRIGGER
        // ============================================================

        private void SetupTeleportTrigger()
        {
            if (teleportFrom == null)
            {
                Debug.LogWarning(
                    "GameManager: Teleport From is not assigned."
                );

                return;
            }

            Collider sourceCollider =
                teleportFrom.GetComponent<Collider>();

            if (sourceCollider == null)
            {
                Debug.LogError(
                    "GameManager: Teleport From '" +
                    teleportFrom.name +
                    "' does not have a Collider!"
                );

                return;
            }

            sourceCollider.isTrigger = true;

            GameManagerTeleportTrigger trigger =
                teleportFrom.GetComponent<GameManagerTeleportTrigger>();

            if (trigger == null)
            {
                trigger =
                    teleportFrom.AddComponent<GameManagerTeleportTrigger>();
            }

            trigger.Initialize(this);
        }


        // ============================================================
        // START
        // ============================================================

        private void Start()
        {
            RaceActive = false;

            ElapsedTime = 0f;

            CurrentLevel = -1;

            currentCheckpoint = null;

            if (fallDetector != null)
                fallDetector.SetActive(false);
            
            if (gameOverPanel != null)
                gameOverPanel.SetActive(false);

            // Mobile controls start in a known state.
            runEnabled = false;
            if (playerController != null)
                playerController.SetRun(false);
            UpdateRunIcons();

            DisableAllCheckpoints();

            UpdateTimer();
            InitializeAudio();
        }


        // ============================================================
        // UPDATE
        // ============================================================

        private void Update()
        {
            if (!RaceActive)
                return;

            ElapsedTime += Time.deltaTime;

            UpdateTimer();
        }


        // ============================================================
        // CHECKPOINT REGISTRATION
        // ============================================================

        public void RegisterCheckpoint(ParkourCheckpoint checkpoint)
        {
            if (checkpoint == null)
                return;

            if (!checkpoints.Contains(checkpoint))
                checkpoints.Add(checkpoint);
        }


        // ============================================================
        // START RACE FROM START POINT
        // ============================================================

        public void StartRaceFromPoint(Transform startPoint)
        {
            // --------------------------------------------------------
            // IMPORTANT:
            // Prevent duplicate trigger calls.
            // --------------------------------------------------------

            if (RaceActive)
                return;


            if (player == null)
            {
                Debug.LogError(
                    "GameManager: Player is not assigned!"
                );

                return;
            }


            if (startPoint == null)
            {
                Debug.LogError(
                    "GameManager: Start Point is missing!"
                );

                return;
            }


            // ========================================================
            // 1. START RACE STATE FIRST
            // ========================================================
            // This prevents the Start Point trigger from firing again
            // while the player is being repositioned.

            RaceActive = true;

            CurrentLevel = 0;

            ElapsedTime = 0f;

            currentCheckpoint = null;


            // ========================================================
            // 2. RESET PLAYER VELOCITY
            // ========================================================

            ResetPlayerVelocity();


            // ========================================================
            // 3. TELEPORT ONLY IF NEEDED
            // ========================================================
            // If the player is already at the start point, don't
            // disable/re-enable the CharacterController unnecessarily.
            //
            // This removes the small physics glitch.

            float distance =
                Vector3.Distance(
                    player.position,
                    startPoint.position
                );

            if (distance > 0.05f)
            {
                TeleportPlayerTo(startPoint);
            }


            // ========================================================
            // 4. ENABLE FALL DETECTOR
            // ========================================================

            if (fallDetector != null)
                fallDetector.SetActive(true);


            // ========================================================
            // 5. ENABLE LEVEL 1 CHECKPOINTS
            // ========================================================

            ActivateCheckpointsForLevel(CurrentLevel);


            // ========================================================
            // 6. UPDATE TIMER
            // ========================================================

            UpdateTimer();


            // ========================================================
            // DEBUG
            // ========================================================

            Debug.Log("================================");
            Debug.Log("PARKOUR RACE STARTED!");
            Debug.Log(
                "START POINT: " +
                startPoint.name
            );
            Debug.Log("LEVEL 1 STARTED!");
            Debug.Log("TIMER STARTED!");
            Debug.Log("================================");
            ShowLevelMessage("LEVEL 1", Color.blue);
        }


        // ============================================================
        // START RACE - OPTIONAL
        // ============================================================

        public void StartRace()
        {
            if (RaceActive)
                return;

            if (player == null)
            {
                Debug.LogError(
                    "GameManager: Player is not assigned!"
                );

                return;
            }

            if (levelStartPositions == null ||
                levelStartPositions.Length == 0)
            {
                Debug.LogError(
                    "GameManager: No level start positions assigned!"
                );

                return;
            }

            RaceActive = true;

            CurrentLevel = 0;

            ElapsedTime = 0f;

            currentCheckpoint = null;

            if (fallDetector != null)
                fallDetector.SetActive(true);

            ActivateLevel(CurrentLevel);

            UpdateTimer();

            Debug.Log("================================");
            Debug.Log("PARKOUR RACE STARTED!");
            Debug.Log("LEVEL 1 STARTED!");
            Debug.Log("================================");
            ShowLevelMessage("LEVEL 1", Color.blue);
        }


        // ============================================================
        // CHECKPOINT
        // ============================================================

        public void SetCheckpoint(ParkourCheckpoint checkpoint)
        {
            if (!RaceActive)
                return;

            if (checkpoint == null)
                return;

            if (checkpoint.levelIndex != CurrentLevel)
                return;

            currentCheckpoint = checkpoint;

            Debug.Log(
                "Checkpoint saved for Level " +
                (CurrentLevel + 1)
            );
        }


        // ============================================================
        // LEVEL COMPLETE
        // ============================================================

        public void CompleteLevel(int level)
        {
            if (!RaceActive)
                return;

            if (level != CurrentLevel)
                return;

            Debug.Log(
                "Level " +
                (CurrentLevel + 1) +
                " Complete!"
            );

            CurrentLevel++;

            currentCheckpoint = null;

            if (levelStartPositions != null &&
                CurrentLevel < levelStartPositions.Length)
            {
                ActivateLevel(CurrentLevel);

                Debug.Log("Level " +(CurrentLevel + 1) +" Started!");
                ShowLevelMessage("LEVEL " + (CurrentLevel + 1), Color.blue);
            }
            else
            {
                FinishRace();
            }
        }


        // ============================================================
        // ACTIVATE LEVEL
        // ============================================================

        private void ActivateLevel(int level)
        {
            currentCheckpoint = null;

            ActivateCheckpointsForLevel(level);

            if (player == null)
            {
                Debug.LogError(
                    "GameManager: Player is not assigned!"
                );

                return;
            }

            if (levelStartPositions == null ||
                levelStartPositions.Length == 0)
            {
                Debug.LogError(
                    "GameManager: No level start positions assigned!"
                );

                return;
            }

            if (level < 0 ||
                level >= levelStartPositions.Length)
            {
                Debug.LogError(
                    "Invalid level start position: " +
                    level
                );

                return;
            }

            Transform start =
                levelStartPositions[level];

            if (start == null)
            {
                Debug.LogError(
                    "Level " +
                    (level + 1) +
                    " start position is empty!"
                );

                return;
            }

            TeleportPlayerTo(start);
        }


        // ============================================================
        // ACTIVATE CHECKPOINTS
        // ============================================================

        private void ActivateCheckpointsForLevel(int level)
        {
            foreach (ParkourCheckpoint checkpoint in checkpoints)
            {
                if (checkpoint == null)
                    continue;

                checkpoint.SetActive(
                    checkpoint.levelIndex == level
                );
            }
        }


        // ============================================================
        // DISABLE ALL CHECKPOINTS
        // ============================================================

        private void DisableAllCheckpoints()
        {
            foreach (ParkourCheckpoint checkpoint in checkpoints)
            {
                if (checkpoint != null)
                    checkpoint.SetActive(false);
            }
        }


        // ============================================================
        // RESPAWN POSITION
        // ============================================================

        public Vector3 GetRespawnPosition()
        {
            if (currentCheckpoint != null)
            {
                return currentCheckpoint.GetRespawnPosition();
            }

            if (levelStartPositions != null &&
                CurrentLevel >= 0 &&
                CurrentLevel < levelStartPositions.Length &&
                levelStartPositions[CurrentLevel] != null)
            {
                return levelStartPositions[CurrentLevel].position;
            }

            return player != null
                ? player.position
                : Vector3.zero;
        }


        // ============================================================
        // RESPAWN ROTATION
        // ============================================================

        public Quaternion GetRespawnRotation()
        {
            if (currentCheckpoint != null)
            {
                return currentCheckpoint.GetRespawnRotation();
            }

            if (levelStartPositions != null &&
                CurrentLevel >= 0 &&
                CurrentLevel < levelStartPositions.Length &&
                levelStartPositions[CurrentLevel] != null)
            {
                return levelStartPositions[CurrentLevel].rotation;
            }

            return player != null
                ? player.rotation
                : Quaternion.identity;
        }


        // ============================================================
        // RESET PLAYER VELOCITY
        // ============================================================

        public void ResetPlayerVelocity()
        {
            if (player == null)
                return;

            Rigidbody rb =
                player.GetComponent<Rigidbody>();

            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }


        // ============================================================
        // TELEPORT PLAYER
        // ============================================================

        public void TeleportPlayerTo(Transform destination)
        {
            if (player == null)
            {
                Debug.LogError(
                    "GameManager: Player is not assigned!"
                );

                return;
            }

            if (destination == null)
            {
                Debug.LogError(
                    "GameManager: Teleport destination is missing!"
                );

                return;
            }


            // --------------------------------------------------------
            // Character Controller
            // --------------------------------------------------------

            CharacterController controller =
                player.GetComponent<CharacterController>();

            if (controller != null)
                controller.enabled = false;


            // --------------------------------------------------------
            // Teleport
            // --------------------------------------------------------

            player.SetPositionAndRotation(
                destination.position,
                destination.rotation
            );


            // --------------------------------------------------------
            // Reset velocity
            // --------------------------------------------------------

            ResetPlayerVelocity();


            // --------------------------------------------------------
            // Enable Character Controller
            // --------------------------------------------------------

            if (controller != null)
                controller.enabled = true;
        }


        // ============================================================
        // TELEPORT FROM → DESTINATION
        // ============================================================

        public void TeleportFromTrigger()
        {
            if (teleportFrom == null)
            {
                Debug.LogError(
                    "GameManager: Teleport From is not assigned!"
                );

                return;
            }

            if (teleportDestination == null)
            {
                Debug.LogError(
                    "GameManager: Teleport Destination is not assigned!"
                );

                return;
            }

            if (player == null)
            {
                Debug.LogError(
                    "GameManager: Player is not assigned!"
                );

                return;
            }

            TeleportPlayerTo(
                teleportDestination.transform
            );

            Debug.Log(
                "PLAYER TELEPORTED: " +
                teleportFrom.name +
                " → " +
                teleportDestination.name
            );
        }

        // ============================================================
        // FINISH RACE
        // ============================================================

        private void FinishRace()
        {
            // --------------------------------------------------------
            // STOP RACE
            // --------------------------------------------------------

            RaceActive = false;

            // --------------------------------------------------------
            // DISABLE FALL DETECTOR
            // --------------------------------------------------------

            if (fallDetector != null)
                fallDetector.SetActive(false);

            // --------------------------------------------------------
            // DISABLE CHECKPOINTS
            // --------------------------------------------------------

            DisableAllCheckpoints();

            // --------------------------------------------------------
            // SAVE FINAL TIME
            // --------------------------------------------------------

            float finalRaceTime = ElapsedTime;

            // --------------------------------------------------------
            // UPDATE HIGH SCORE
            // --------------------------------------------------------

            float highScore = PlayerPrefs.GetFloat(
                HighScoreKey,
                -1f
            );

            bool isNewHighScore = false;

            // No previous score OR current time is faster
            if (highScore < 0f || finalRaceTime < highScore)
            {
                highScore = finalRaceTime;

                PlayerPrefs.SetFloat(
                    HighScoreKey,
                    highScore
                );

                PlayerPrefs.Save();

                isNewHighScore = true;
            }

            // --------------------------------------------------------
            // UPDATE GAME OVER UI
            // --------------------------------------------------------

            ShowGameOverPanel(
                finalRaceTime,
                highScore
            );

            // --------------------------------------------------------
            // SHOW FINISH MESSAGE
            // --------------------------------------------------------

            ShowLevelMessage(
                "Race finish!",
                Color.red
            );

            Debug.Log("================================");
            Debug.Log("PARKOUR COMPLETE!");
            Debug.Log(
                "Final Time: " +
                finalRaceTime.ToString("F2") +
                " seconds"
            );

            Debug.Log(
                "High Score: " +
                highScore.ToString("F2") +
                " seconds"
            );

            if (isNewHighScore)
            {
                Debug.Log("NEW HIGH SCORE!");
            }

            Debug.Log("================================");
        }

        // ============================================================
        // SHOW GAME OVER PANEL
        // ============================================================

        private void ShowGameOverPanel(
            float currentTime,
            float bestTime)
        {
            // --------------------------------------------------------
            // CURRENT SCORE
            // --------------------------------------------------------

            if (currentScoreText != null)
            {
                currentScoreText.text = "Score - " + FormatRaceTime(currentTime);
            }

            // --------------------------------------------------------
            // HIGH SCORE
            // --------------------------------------------------------

            if (highScoreText != null)
            {
                highScoreText.text ="HighScore - " + FormatRaceTime(bestTime);
            }

            // --------------------------------------------------------
            // SHOW PANEL
            // --------------------------------------------------------

            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(true);
            }
        }

        // ============================================================
        // FORMAT RACE TIME
        // ============================================================

        private string FormatRaceTime(float time)
        {
            int hours = Mathf.FloorToInt(time / 3600f);

            int minutes = Mathf.FloorToInt(
                (time % 3600f) / 60f
            );

            float seconds = time % 60f;

            return $"{hours:00}:{minutes:00}:{Mathf.FloorToInt(seconds):00}";
        }
       

        // ============================================================
        // TIMER
        // ============================================================

        private void UpdateTimer()
        {
            if (timerText == null)
                return;

            if (!RaceActive)
            {
                timerText.gameObject.SetActive(false);
                return;
            }

            timerText.gameObject.SetActive(true);

            int hours = Mathf.FloorToInt(ElapsedTime / 3600f);

            int minutes = Mathf.FloorToInt(
                (ElapsedTime % 3600f) / 60f
            );

            float seconds = ElapsedTime % 60f;

            timerText.text =
                $"{hours:00}:{minutes:00}:{seconds:00.00}";
        }



        // ============================================================
        // GETTERS
        // ============================================================

        public GameObject GetTeleportFrom()
        {
            return teleportFrom;
        }

        public GameObject GetTeleportDestination()
        {
            return teleportDestination;
        }


        // ============================================================
        // INNER TELEPORT TRIGGER
        // ============================================================

        private class GameManagerTeleportTrigger : MonoBehaviour
        {
            private GameManager gameManager;

            public void Initialize(GameManager manager)
            {
                gameManager = manager;
            }

            private void OnTriggerEnter(Collider other)
            {
                if (gameManager == null)
                    return;

                if (other == null)
                    return;

                if (!other.CompareTag(
                    gameManager.teleportPlayerTag))
                {
                    return;
                }

                gameManager.TeleportFromTrigger();
            }
        }

        // ============================================================
        // SHOW LEVEL MESSAGE
        // ============================================================

         private void ShowLevelMessage(string message, Color color)
        {
            if (levelMessageText == null)
                return;

            // Stop any previous message transition
            if (levelMessageCoroutine != null)
            {
                StopCoroutine(levelMessageCoroutine);
                levelMessageCoroutine = null;
            }
            levelMessageText.color = color;


            levelMessageCoroutine =
                StartCoroutine(LevelMessageTransition(message));
        }
        


        // ============================================================
        // LEVEL MESSAGE TRANSITION
        // ============================================================

        private IEnumerator LevelMessageTransition(string message)
        {
            levelMessageText.text = message;
            levelMessageText.gameObject.SetActive(true);

            // Start invisible
            levelMessageText.alpha = 0f;

            // --------------------------------------------------------
            // FADE IN
            // --------------------------------------------------------

            float time = 0f;

            while (time < fadeInDuration)
            {
                time += Time.deltaTime;

                levelMessageText.alpha =
                    Mathf.Lerp(0f, 1f, time / fadeInDuration);

                yield return null;
            }

            levelMessageText.alpha = 1f;

            // --------------------------------------------------------
            // WAIT
            // --------------------------------------------------------

            yield return new WaitForSeconds(levelMessageDuration);

            // --------------------------------------------------------
            // FADE OUT
            // --------------------------------------------------------

            time = 0f;

            while (time < fadeOutDuration)
            {
                time += Time.deltaTime;

                levelMessageText.alpha =
                    Mathf.Lerp(1f, 0f, time / fadeOutDuration);

                yield return null;
            }

            levelMessageText.alpha = 0f;
            levelMessageText.gameObject.SetActive(false);

            levelMessageCoroutine = null;
        }
        // ============================================================
        // PAUSE GAME
        // ============================================================

        public void PauseGame()
        {
            if (pausePanel != null)
                pausePanel.SetActive(true);

            Time.timeScale = 0f;

            Debug.Log("GAME PAUSED");
        }


        // ============================================================
        // RESUME GAME
        // ============================================================

        public void ResumeGame()
        {
            if (pausePanel != null)
                pausePanel.SetActive(false);

            Time.timeScale = 1f;

            Debug.Log("GAME RESUMED");
        }


        // ============================================================
        // OPEN SETTINGS
        // ============================================================

        public void OpenSettings()
        {
            if (settingPanel != null)
                settingPanel.SetActive(true);

            Time.timeScale = 0f;

            Debug.Log("SETTINGS OPENED");
        }


        // ============================================================
        // CLOSE SETTINGS
        // ============================================================

        public void CloseSettings()
        {
            if (settingPanel != null)
                settingPanel.SetActive(false);

            Time.timeScale = 1f;

            Debug.Log("SETTINGS CLOSED");
        }


        // ============================================================
        // EXIT GAME
        // ============================================================

        public void ExitGame()
        {
            Time.timeScale = 1f;

        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif

            Debug.Log("GAME EXITED");
        }

        // ============================================================
        // INITIALIZE AUDIO
        // ============================================================
        
        private void InitializeAudio()
        {
            musicEnabled = true;
            soundEnabled = true;
        
            // Start music
            if (musicSource != null)
            {
                musicSource.mute = false;
        
                if (!musicSource.isPlaying)
                    musicSource.Play();
            }
        
            // Enable sound effects
            if (soundSource != null)
                soundSource.mute = false;
        
            UpdateAudioIcons();
        }
        
        
        // ============================================================
        // TOGGLE MUSIC
        // ============================================================
        
        public void ToggleMusic()
        {
            musicEnabled = !musicEnabled;
        
            if (musicSource != null)
            {
                musicSource.mute = !musicEnabled;
            }
        
            UpdateAudioIcons();
        
            Debug.Log(
                "Music: " +
                (musicEnabled ? "ON" : "OFF")
            );
        }
        
        
        // ============================================================
        // TOGGLE SOUND
        // ============================================================
        
        public void ToggleSound()
        {
            soundEnabled = !soundEnabled;
        
            if (soundSource != null)
            {
                soundSource.mute = !soundEnabled;
            }
        
            UpdateAudioIcons();
        
            Debug.Log(
                "Sound: " +
                (soundEnabled ? "ON" : "OFF")
            );
        }
        
        
        // ============================================================
        // UPDATE AUDIO ICONS
        // ============================================================
        
        private void UpdateAudioIcons()
        {
            // --------------------------------------------------------
            // MUSIC
            // --------------------------------------------------------
        
            if (musicOnIcon != null)
                musicOnIcon.SetActive(musicEnabled);
        
            if (musicOffIcon != null)
                musicOffIcon.SetActive(!musicEnabled);
        
        
            // --------------------------------------------------------
            // SOUND
            // --------------------------------------------------------
        
            if (soundOnIcon != null)
                soundOnIcon.SetActive(soundEnabled);
        
            if (soundOffIcon != null)
                soundOffIcon.SetActive(!soundEnabled);
        }


        // ============================================================
        // SETUP PLAYER CONTROLLER
        // ============================================================

        private void SetupPlayerController()
        {
            if (playerController != null)
                return;

            if (player == null)
            {
                Debug.LogWarning(
                    "GameManager: Player is not assigned. " +
                    "Cannot find CharacterControl."
                );

                return;
            }

            // Try directly on Player
            playerController =
                player.GetComponent<CharacterControl>();

            // Try children
            if (playerController == null)
            {
                playerController =
                    player.GetComponentInChildren<CharacterControl>();
            }

            // Try anywhere in scene
            if (playerController == null &&
                autoFindPlayerController)
            {
                playerController =
                    FindFirstObjectByType<CharacterControl>();
            }

            if (playerController == null)
            {
                Debug.LogWarning(
                    "GameManager: CharacterControl not found."
                );
            }
        }


        // ============================================================
        // SET PLAYER CONTROLLER
        // ============================================================

        public void SetPlayerController(CharacterControl controller)
        {
            playerController = controller;

            if (playerController != null)
                playerController.SetRun(runEnabled);
        }


        // ============================================================
        // GET PLAYER CONTROLLER
        // ============================================================

        public CharacterControl GetPlayerController()
        {
            if (playerController == null &&
                autoFindPlayerController)
            {
                SetupPlayerController();
            }

            return playerController;
        }


        // ============================================================
        // JOYSTICK
        // ============================================================

        public void SetJoystickInput(Vector2 input)
        {
            if (playerController == null)
                SetupPlayerController();

            if (playerController == null)
                return;

            playerController.SetJoystickInput(input);
        }


        public void ClearJoystickInput()
        {
            if (playerController == null)
                SetupPlayerController();

            if (playerController == null)
                return;

            playerController.ClearJoystickInput();
        }


        // ============================================================
        // JUMP
        // ============================================================

        public void Jump()
        {
            if (playerController == null)
                SetupPlayerController();

            if (playerController == null)
                return;

            playerController.JumpButton();
        }


        // ============================================================
        // SLIDE
        // ============================================================

        public void Slide()
        {
            if (playerController == null)
                SetupPlayerController();

            if (playerController == null)
                return;

            playerController.SlideButton();
        }


        // ============================================================
        // RESET MOBILE CONTROLS
        // ============================================================

        public void ResetMobileControls()
        {
            if (playerController == null)
                SetupPlayerController();

            if (playerController == null)
                return;

            playerController.ResetMobileControls();

            runEnabled = false;
            UpdateRunIcons();
        }




        public void ToggleRun()
        {
            if (playerController == null)
                SetupPlayerController();

            if (playerController == null)
            {
                Debug.LogWarning(
                    "GameManager: CharacterControl not found. " +
                    "Assign Player and/or Player Controller in the Inspector."
                );
                return;
            }

            runEnabled = !runEnabled;
            playerController.SetRun(runEnabled);
            UpdateRunIcons();
        }


        private void UpdateRunIcons()
        {
            if (runOnIcon != null)
                runOnIcon.SetActive(runEnabled);

            if (runOffIcon != null)
                runOffIcon.SetActive(!runEnabled);
        }


        // ============================================================
        // RESTART RACE BUTTON
        // ============================================================

        public void RestartRace()
        {

            // Hide Game Over panel
            if (gameOverPanel != null)
                gameOverPanel.SetActive(false);

            if(pausePanel != null)
                pausePanel.SetActive(false); 


            if (player == null)
            {
                Debug.LogError(
                    "GameManager: Player is not assigned!"
                );

                return;
            }

            if (restartRacePosition == null)
            {
                Debug.LogError(
                    "GameManager: Restart Race Position is not assigned!"
                );

                return;
            }

            // Make sure game is running
            Time.timeScale = 1f;

            // --------------------------------------------------------
            // RESET RACE STATE
            // --------------------------------------------------------

            RaceActive = true;

            CurrentLevel = 0;

            ElapsedTime = 0f;

            currentCheckpoint = null;

            // --------------------------------------------------------
            // RESET PLAYER
            // --------------------------------------------------------

            ResetMobileControls();

            ResetPlayerVelocity();

            TeleportPlayerTo(restartRacePosition);

            // --------------------------------------------------------
            // ENABLE FALL DETECTOR
            // --------------------------------------------------------

            if (fallDetector != null)
                fallDetector.SetActive(true);

            // --------------------------------------------------------
            // RESET LEVEL / CHECKPOINTS
            // --------------------------------------------------------

            ActivateCheckpointsForLevel(CurrentLevel);

            // --------------------------------------------------------
            // UPDATE TIMER
            // --------------------------------------------------------

            UpdateTimer();

            Debug.Log("================================");
            Debug.Log("RACE RESTARTED!");
            Debug.Log(
                "Restart Position: " +
                restartRacePosition.name
            );
            Debug.Log("LEVEL 1 STARTED!");
            Debug.Log("TIMER RESET!");
            Debug.Log("================================");

            ShowLevelMessage("LEVEL 1", Color.blue);
        }


        // ============================================================
        // EXPLORE BUTTON
        // ============================================================

        public void Explore()
        {
            if (gameOverPanel != null)
                gameOverPanel.SetActive(false);

            if (player == null)
            {
                Debug.LogError(
                    "GameManager: Player is not assigned!"
                );

                return;
            }

            if (explorePosition == null)
            {
                Debug.LogError(
                    "GameManager: Explore Position is not assigned!"
                );

                return;
            }

            // Make sure game is running
            Time.timeScale = 1f;

            // --------------------------------------------------------
            // STOP RACE
            // --------------------------------------------------------

            RaceActive = false;

            ElapsedTime = 0f;

            CurrentLevel = -1;

            currentCheckpoint = null;

            // --------------------------------------------------------
            // DISABLE RACE SYSTEMS
            // --------------------------------------------------------

            if (fallDetector != null)
                fallDetector.SetActive(false);

            DisableAllCheckpoints();

            // --------------------------------------------------------
            // RESET PLAYER
            // --------------------------------------------------------

            ResetMobileControls();

            ResetPlayerVelocity();

            TeleportPlayerTo(explorePosition);

            // --------------------------------------------------------
            // UPDATE TIMER
            // --------------------------------------------------------

            UpdateTimer();

            Debug.Log("================================");
            Debug.Log("EXPLORE MODE");
            Debug.Log(
                "Explore Position: " +
                explorePosition.name
            );
            Debug.Log("TIMER RESET!");
            Debug.Log("RACE STOPPED!");
            Debug.Log("================================");
        }



    }
}

