using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace OMG.ArrowGo
{
    public class GameManager : MonoBehaviour
    {
        // ============================================================
        // GAME REFERENCES
        // ============================================================

        [Header("Game References")]
        [SerializeField] private GridManager gridManager;

        [Tooltip("Runtime generator that supplies all playable LevelData.")]
        [SerializeField] private RuntimeLevelGenerator levelGenerator;

        // Only the current level is generated. Levels are created on demand,
        // so the game has no maximum level count.


        // ============================================================
        // LEVEL UI
        // ============================================================

        [Header("Level UI")]
        [SerializeField] private TMP_Text levelText;


        // ============================================================
        // MENU PANELS
        // ============================================================

        [Header("Menu Panels")]
        [SerializeField] private GameObject settingPanel;
        [SerializeField] private GameObject playClickPanel;
        [SerializeField] private GameObject mainGamePanel;


        // ============================================================
        // LEVEL COMPLETE PANEL
        // ============================================================

        [Header("Level Complete Panel")]
        [SerializeField] private GameObject levelCompletePanel;


        // ============================================================
        // AUDIO
        // ============================================================

        [Header("Audio")]
        [SerializeField] private AudioManager audioManager;


        // ============================================================
        // AUDIO TOGGLES
        // ============================================================

        [Header("Audio Toggles")]
        [SerializeField] private SlideToggle musicToggle;
        [SerializeField] private SlideToggle soundToggle;


        // ============================================================
        // BUTTON SOUND
        // ============================================================

        [Header("Button Sound")]
        [SerializeField] private AudioClip buttonClickSound;


        // ============================================================
        // SAVE SYSTEM
        // ============================================================

        [Header("Save System")]
        [Tooltip("PlayerPrefs key used to save the current/unlocked level.")]
        [SerializeField] private string saveKey = "ArrowGo_CurrentLevel";


        // ============================================================
        // PRIVATE
        // ============================================================

        private int currentLevelIndex = 0;
        private LevelData currentGeneratedLevel;

        [SerializeField] private float levelCompletePanelDelay = 1f;


        // ============================================================
        // UNITY - AWAKE
        // ============================================================

        private void Awake()
        {
            FindAudioManager();
        }


        // ============================================================
        // UNITY - ON DESTROY
        // ============================================================

        private void OnDestroy()
        {
            Time.timeScale = 1f;

            if (currentGeneratedLevel != null)
            {
                Destroy(currentGeneratedLevel);
                currentGeneratedLevel = null;
            }
        }


        // ============================================================
        // UNITY - ON ENABLE
        // ============================================================

        private void OnEnable()
        {
            SubscribeToGridManager();
        }


        // ============================================================
        // UNITY - ON DISABLE
        // ============================================================

        private void OnDisable()
        {
            UnsubscribeFromGridManager();
        }


        // ============================================================
        // START
        // ============================================================

        private void Start()
        {
            FindAudioManager();

            if (levelGenerator == null)
                levelGenerator = RuntimeLevelGenerator.Instance;

            if (levelGenerator == null)
                levelGenerator = FindFirstObjectByType<RuntimeLevelGenerator>();

            if (levelGenerator == null)
            {
                Debug.LogError("GameManager: RuntimeLevelGenerator not found.");
                CloseAllPanels();
                UpdateAudioToggles();
                return;
            }

            CloseAllPanels();
            currentLevelIndex = Mathf.Max(0, GetSavedLevel());
            LoadLevel(currentLevelIndex);
            ShowPlayPanel();
            UpdateAudioToggles();
        }


        // ============================================================
        // FIND AUDIO MANAGER
        // ============================================================

        private void FindAudioManager()
        {
            if (audioManager != null)
                return;

            audioManager = AudioManager.Instance;

            if (audioManager == null)
            {
                audioManager = FindFirstObjectByType<AudioManager>();
            }
        }


        // ============================================================
        // GRID EVENTS
        // ============================================================

        private void SubscribeToGridManager()
        {
            if (gridManager == null)
                return;

            gridManager.OnBoardCleared -= HandleBoardCleared;
            gridManager.OnBoardCleared += HandleBoardCleared;
        }


        private void UnsubscribeFromGridManager()
        {
            if (gridManager == null)
                return;

            gridManager.OnBoardCleared -= HandleBoardCleared;
        }


        // ============================================================
        // LOAD LEVEL
        // ============================================================

        public void LoadLevel(int index)
        {
            if (levelGenerator == null)
                levelGenerator = RuntimeLevelGenerator.Instance;

            if (levelGenerator == null)
            {
                Debug.LogError("GameManager: RuntimeLevelGenerator not found.");
                return;
            }

            if (gridManager == null)
            {
                Debug.LogError("GameManager: GridManager is not assigned.");
                return;
            }

            currentLevelIndex = Mathf.Max(0, index);
            UpdateLevelText();

            LevelData level = levelGenerator.GenerateLevel(currentLevelIndex);

            if (level == null)
            {
                Debug.LogError("GameManager: Failed to generate Level " + (currentLevelIndex + 1) + ".");
                return;
            }

            gridManager.LoadLevel(level);

            // The GridManager has copied/loaded the level data. Release the
            // previous runtime ScriptableObject so memory does not grow
            // forever as the player reaches more levels.
            if (currentGeneratedLevel != null &&
                currentGeneratedLevel != level)
            {
                Destroy(currentGeneratedLevel);
            }

            currentGeneratedLevel = level;

            SaveCurrentLevel();

            if (settingPanel != null)
                settingPanel.SetActive(false);

            if (playClickPanel != null)
                playClickPanel.SetActive(false);

            Time.timeScale = 1f;
        }


        // ============================================================
        // UPDATE LEVEL TEXT
        // ============================================================

        private void UpdateLevelText()
        {
            if (levelText != null)
            {
                levelText.text = "Level " + (currentLevelIndex + 1);
            }
        }


        // ============================================================
        // SAVE CURRENT LEVEL
        // ============================================================

        private void SaveCurrentLevel()
        {
            PlayerPrefs.SetInt(
                saveKey,
                currentLevelIndex
            );

            PlayerPrefs.Save();
        }


        // ============================================================
        // GET SAVED LEVEL
        // ============================================================

        private int GetSavedLevel()
        {
            if (!PlayerPrefs.HasKey(saveKey))
            {
                // First time playing
                return 0;
            }

            return PlayerPrefs.GetInt(
                saveKey,
                0
            );
        }


        // ============================================================
        // OPEN LEVEL COMPLETE PANEL
        // ============================================================

        public void OpenLevelCompletePanel()
        {
            if (levelCompletePanel != null)
            {
                levelCompletePanel.SetActive(true);
            }

            // Keep game visible behind completion panel
            if (mainGamePanel != null)
            {
                mainGamePanel.SetActive(true);
            }
        }


        // ============================================================
        // NEXT LEVEL BUTTON
        // ============================================================

        public void NextLevelButton()
        {
            PlayButtonSound();

            if (levelCompletePanel != null)
                levelCompletePanel.SetActive(false);

            Time.timeScale = 1f;

            int nextLevel = currentLevelIndex + 1;
            LoadLevel(nextLevel);

            if (mainGamePanel != null)
                mainGamePanel.SetActive(true);
        }

        // ============================================================
        // RESTART LEVEL
        // ============================================================
        
        public void RestartLevel()
        {
            PlayButtonSound();
        
            // --------------------------------------------------------
            // CLOSE COMPLETE PANEL
            // --------------------------------------------------------
        
            if (levelCompletePanel != null)
            {
                levelCompletePanel.SetActive(false);
            }
        
            // --------------------------------------------------------
            // CLOSE SETTINGS PANEL
            // --------------------------------------------------------
        
            if (settingPanel != null)
            {
                settingPanel.SetActive(false);
            }
        
            Time.timeScale = 1f;
        
            // --------------------------------------------------------
            // RESTART CURRENT LEVEL
            // --------------------------------------------------------
            // Reuse the exact LevelData already generated.
            // This prevents the puzzle pattern from changing.
        
            if (gridManager == null)
            {
                Debug.LogError("GameManager: GridManager is not assigned.");
                return;
            }
        
            if (currentGeneratedLevel != null)
            {
                gridManager.LoadLevel(currentGeneratedLevel);
            }
            else
            {
                // Safety fallback.
                LoadLevel(currentLevelIndex);
            }
        
            // --------------------------------------------------------
            // SHOW GAME PANEL
            // --------------------------------------------------------
        
            if (mainGamePanel != null)
            {
                mainGamePanel.SetActive(true);
            }
        }
               
        
        
        

        // ============================================================
        // BOARD CLEARED
        // ============================================================

        private void HandleBoardCleared()
        {
            // --------------------------------------------------------
            // SAVE PROGRESS
            // --------------------------------------------------------
            // Unlock the next level by saving its index.

            SaveNextLevel();


            // --------------------------------------------------------
            // OPEN COMPLETE PANEL AFTER DELAY
            // --------------------------------------------------------

            StartCoroutine(OpenLevelCompletePanelWithDelay());
        }


        // ============================================================
        // LEVEL COMPLETE PANEL DELAY
        // ============================================================

        private IEnumerator OpenLevelCompletePanelWithDelay()
        {
            yield return new WaitForSeconds(levelCompletePanelDelay);

            OpenLevelCompletePanel();
        }


        // ============================================================
        // SAVE NEXT LEVEL
        // ============================================================

        private void SaveNextLevel()
        {
            int nextLevel = currentLevelIndex + 1;
            int savedLevel = GetSavedLevel();

            if (nextLevel > savedLevel)
            {
                PlayerPrefs.SetInt(saveKey, nextLevel);
                PlayerPrefs.Save();

                Debug.Log("GameManager: Level " + (nextLevel + 1) + " unlocked.");
            }
        }


        // ============================================================
        // PLAY GAME
        // ============================================================

        public void PlayGame()
        {
            PlayButtonSound();
            Time.timeScale = 1f;

            // --------------------------------------------------------
            // DISABLE PLAY PANEL
            // --------------------------------------------------------

            if (playClickPanel != null)
            {
                playClickPanel.SetActive(false);
            }


            // --------------------------------------------------------
            // ENABLE MAIN GAME PANEL
            // --------------------------------------------------------

            if (mainGamePanel != null)
            {
                mainGamePanel.SetActive(true);
            }


            // --------------------------------------------------------
            // CLOSE SETTINGS
            // --------------------------------------------------------

            if (settingPanel != null)
            {
                settingPanel.SetActive(false);
            }


            // --------------------------------------------------------
            // CLOSE COMPLETE PANEL
            // --------------------------------------------------------

            if (levelCompletePanel != null)
            {
                levelCompletePanel.SetActive(false);
            }
        }


        // ============================================================
        // QUIT GAME
        // ============================================================

        public void QuitGame()
        {
            PlayButtonSound();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }


        // ============================================================
        // OPEN PLAY CLICK PANEL
        // ============================================================

        public void OpenPlayClickPanel()
        {
            PlayButtonSound();

            ShowPlayPanel();
        }


        // ============================================================
        // SHOW PLAY PANEL
        // ============================================================

        private void ShowPlayPanel()
        {
            // --------------------------------------------------------
            // PLAY PANEL ON
            // --------------------------------------------------------

            if (playClickPanel != null)
            {
                playClickPanel.SetActive(true);
            }


            // --------------------------------------------------------
            // GAME PANEL OFF
            // --------------------------------------------------------

            if (mainGamePanel != null)
            {
                mainGamePanel.SetActive(false);
            }


            // --------------------------------------------------------
            // SETTINGS OFF
            // --------------------------------------------------------

            if (settingPanel != null)
            {
                settingPanel.SetActive(false);
            }


            // --------------------------------------------------------
            // COMPLETE PANEL OFF
            // --------------------------------------------------------

            if (levelCompletePanel != null)
            {
                levelCompletePanel.SetActive(false);
            }
        }


        // ============================================================
        // CLOSE PLAY CLICK PANEL
        // ============================================================

        public void ClosePlayClickPanel()
        {
            PlayButtonSound();

            if (playClickPanel != null)
            {
                playClickPanel.SetActive(false);
            }
        }


        // ============================================================
        // OPEN SETTINGS PANEL
        // ============================================================

        public void OpenSettingPanel()
        {
            PlayButtonSound();

            if (settingPanel != null)
                settingPanel.SetActive(true);

            if (playClickPanel != null)
                playClickPanel.SetActive(false);

            Time.timeScale = 0f;
            UpdateAudioToggles();
        }


        // ============================================================
        // CLOSE SETTINGS PANEL
        // ============================================================

        public void CloseSettingPanel()
        {
            PlayButtonSound();

            if (settingPanel != null)
                settingPanel.SetActive(false);

            Time.timeScale = 1f;
        }


        // ============================================================
        // CLOSE ALL PANELS
        // ============================================================

        private void CloseAllPanels()
        {
            Time.timeScale = 1f;

            if (settingPanel != null)
            {
                settingPanel.SetActive(false);
            }

            if (playClickPanel != null)
            {
                playClickPanel.SetActive(false);
            }

            if (mainGamePanel != null)
            {
                mainGamePanel.SetActive(false);
            }

            if (levelCompletePanel != null)
            {
                levelCompletePanel.SetActive(false);
            }
        }


        // ============================================================
        // MUSIC TOGGLE
        // ============================================================

        public void OnMusicToggleChanged(bool value)
        {
            FindAudioManager();

            if (audioManager == null)
            {
                Debug.LogWarning(
                    "GameManager: AudioManager not found."
                );

                return;
            }

            audioManager.ToggleMusic(value);
        }


        // ============================================================
        // SOUND TOGGLE
        // ============================================================

        public void OnSoundToggleChanged(bool value)
        {
            FindAudioManager();

            if (audioManager == null)
            {
                Debug.LogWarning(
                    "GameManager: AudioManager not found."
                );

                return;
            }

            audioManager.ToggleSound(value);
        }


        // ============================================================
        // UPDATE AUDIO TOGGLES
        // ============================================================

        private void UpdateAudioToggles()
        {
            FindAudioManager();

            if (audioManager == null)
            {
                Debug.LogWarning(
                    "GameManager: Cannot update audio toggles. " +
                    "AudioManager not found."
                );

                return;
            }


            // --------------------------------------------------------
            // MUSIC
            // --------------------------------------------------------

            if (musicToggle != null)
            {
                musicToggle.SetValue(
                    audioManager.IsMusicOn(),
                    true
                );
            }


            // --------------------------------------------------------
            // SOUND
            // --------------------------------------------------------

            if (soundToggle != null)
            {
                soundToggle.SetValue(
                    audioManager.IsSoundOn(),
                    true
                );
            }
        }


        // ============================================================
        // BUTTON CLICK SOUND
        // ============================================================

        private void PlayButtonSound()
        {
            FindAudioManager();

            if (audioManager == null)
                return;

            if (buttonClickSound == null)
                return;

            audioManager.PlaySound(buttonClickSound);
        }


        // ============================================================
        // RESET LEVEL PROGRESS
        // ============================================================
        // OPTIONAL:
        // You can connect this to a "Reset Progress" button.
        //
        // It will make the game start from Level 1 again.

        public void ResetLevelProgress()
        {
            PlayButtonSound();

            PlayerPrefs.DeleteKey(saveKey);
            PlayerPrefs.Save();

            currentLevelIndex = 0;

            Debug.Log(
                "GameManager: Level progress reset. " +
                "Starting from Level 1."
            );

            LoadLevel(0);

            ShowPlayPanel();
        }


        // ============================================================
        // GET CURRENT LEVEL
        // ============================================================

        public int GetCurrentLevel()
        {
            return currentLevelIndex;
        }


        // ============================================================
        // GET CURRENT LEVEL NUMBER
        // ============================================================
        // Returns human-readable level number:
        // Level 1 = 1
        // Level 2 = 2
        // etc.

        public int GetCurrentLevelNumber()
        {
            return currentLevelIndex + 1;
        }
    }


    // ============================================================
    // CUSTOM EDITOR (compiled out of player builds automatically)
    // ============================================================
#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(GameManager))]
    public class GameManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // Draw normal GameManager Inspector
            DrawDefaultInspector();

            UnityEditor.EditorGUILayout.Space(15);

            // ============================================================
            // SAVE / PROGRESS TOOLS
            // ============================================================

            UnityEditor.EditorGUILayout.LabelField(
                "Save / Progress Tools",
                UnityEditor.EditorStyles.boldLabel
            );

            UnityEditor.EditorGUILayout.HelpBox(
                "This will delete the saved PlayerPrefs level. " +
                "The next time the game starts, it will begin from Level 1.",
                UnityEditor.MessageType.Warning
            );

            UnityEditor.EditorGUILayout.Space(5);

            // ============================================================
            // RESET SAVE
            // ============================================================

            GUI.backgroundColor = new Color(1f, 0.65f, 0.25f);

            if (GUILayout.Button(
                "RESET SAVE → LEVEL 1",
                GUILayout.Height(40)))
            {
                bool confirm = UnityEditor.EditorUtility.DisplayDialog(
                    "Reset Game Save",
                    "Are you sure you want to reset the saved progress?\n\n" +
                    "The next time the game starts, it will begin from Level 1.\n\n" +
                    "This cannot be undone.",
                    "RESET",
                    "CANCEL"
                );

                if (confirm)
                {
                    // Get the GameManager
                    GameManager gameManager =
                        (GameManager)target;

                    // ====================================================
                    // DELETE SAVED LEVEL
                    // ====================================================

                    // Use SerializedObject to access the private saveKey
                    UnityEditor.SerializedObject serializedObject =
                        new UnityEditor.SerializedObject(gameManager);

                    UnityEditor.SerializedProperty saveKeyProperty =
                        serializedObject.FindProperty("saveKey");

                    // NOTE: fallback string here duplicates GameManager's
                    // default value. If you ever rename that default,
                    // update it here too (or read it via reflection).
                    string saveKey = "ArrowGo_CurrentLevel";

                    if (saveKeyProperty != null &&
                        !string.IsNullOrEmpty(saveKeyProperty.stringValue))
                    {
                        saveKey = saveKeyProperty.stringValue;
                    }

                    // Delete the saved level
                    PlayerPrefs.DeleteKey(saveKey);
                    PlayerPrefs.Save();

                    // ====================================================
                    // CONFIRM
                    // ====================================================

                    Debug.Log(
                        "ArrowGo: Save reset successfully.\n" +
                        "Deleted PlayerPrefs key: " + saveKey + "\n" +
                        "Next game start will begin from Level 1."
                    );

                    UnityEditor.EditorUtility.DisplayDialog(
                        "Save Reset",
                        "Game save has been reset successfully.\n\n" +
                        "The next time you start the game, " +
                        "it will begin from Level 1.",
                        "OK"
                    );
                }
            }

            GUI.backgroundColor = Color.white;
        }
    }
#endif
}