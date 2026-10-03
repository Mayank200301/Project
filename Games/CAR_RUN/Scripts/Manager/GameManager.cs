using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("UI Panels")]
    [SerializeField] private GameObject gamePanel;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject gameOverPanel;

    [Header("Game Over Statistics")]
    [SerializeField] private TMP_Text deliveriesText;
    [SerializeField] private TMP_Text distanceText;

    [Header("Audio")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource soundSource;
    [SerializeField] private AudioClip buttonClickSound;

    private bool isPaused = false;
    private bool musicOn = true;
    private bool soundOn = true;


    // =========================================================
    // AWAKE
    // =========================================================

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }


    // =========================================================
    // START
    // =========================================================

    private void Start()
    {
        Time.timeScale = 1f;

        // -------------------------
        // Initial UI State
        // -------------------------

        if (gamePanel != null)
            gamePanel.SetActive(true);

        if (pausePanel != null)
            pausePanel.SetActive(false);

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);


        // -------------------------
        // Load Audio Settings
        // -------------------------

        musicOn = PlayerPrefs.GetInt("Music", 1) == 1;
        soundOn = PlayerPrefs.GetInt("Sound", 1) == 1;

        UpdateAudio();
    }


    // =========================================================
    // PLAY
    // =========================================================

    public void PlayButton()
    {
        Time.timeScale = 1f;
        isPaused = false;

        if (gamePanel != null)
            gamePanel.SetActive(true);

        if (pausePanel != null)
            pausePanel.SetActive(false);

        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }


    // =========================================================
    // PAUSE
    // =========================================================

    public void PauseButton()
    {
        isPaused = true;

        Time.timeScale = 0f;
        
        PlayButtonSound();

        if (gamePanel != null)
            gamePanel.SetActive(false);

        if (pausePanel != null)
            pausePanel.SetActive(true);
    }


    // =========================================================
    // RESUME
    // =========================================================

    public void ResumeButton()
    {
        isPaused = false;

        Time.timeScale = 1f;

        PlayButtonSound();

        if (gamePanel != null)
            gamePanel.SetActive(true);

        if (pausePanel != null)
            pausePanel.SetActive(false);
    }


    // =========================================================
    // SETTINGS
    // =========================================================

    public void SettingsButton()
    {
        PlayButtonSound();

        if (gamePanel != null)
            gamePanel.SetActive(false);

        if (settingsPanel != null)
            settingsPanel.SetActive(true);
    }


    // =========================================================
    // CLOSE SETTINGS
    // =========================================================

    public void CloseSettingsButton()
    {
        PlayButtonSound();

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        if (gamePanel != null)
            gamePanel.SetActive(true);
    }


    // =========================================================
    // MUSIC
    // =========================================================

    public void MusicButton()
    {
        musicOn = !musicOn;

        PlayerPrefs.SetInt("Music", musicOn ? 1 : 0);
        PlayerPrefs.Save();

        UpdateAudio();

        // Play click only if sound is enabled
        PlayButtonSound();
    }


    public void SetMusic(bool enabled)
    {
        musicOn = enabled;

        PlayerPrefs.SetInt("Music", musicOn ? 1 : 0);
        PlayerPrefs.Save();

        UpdateAudio();
    }


    // =========================================================
    // SOUND
    // =========================================================

    public void SoundButton()
    {
        soundOn = !soundOn;

        PlayerPrefs.SetInt("Sound", soundOn ? 1 : 0);
        PlayerPrefs.Save();

        UpdateAudio();

        // Only play click when sound has been turned ON
        if (soundOn)
        {
            PlayButtonSound();
        }
    }


    public void SetSound(bool enabled)
    {
        soundOn = enabled;

        PlayerPrefs.SetInt("Sound", soundOn ? 1 : 0);
        PlayerPrefs.Save();

        UpdateAudio();
    }


    // =========================================================
    // UPDATE AUDIO
    // =========================================================

    private void UpdateAudio()
    {
        // Background music
        if (musicSource != null)
        {
            musicSource.mute = !musicOn;
        }

        // Sound effects
        if (soundSource != null)
        {
            soundSource.mute = !soundOn;
        }
    }


    // =========================================================
    // BUTTON CLICK SOUND
    // =========================================================

    public void PlayButtonSound()
    {
        if (soundSource != null && soundOn && buttonClickSound != null)
        {
            soundSource.PlayOneShot(buttonClickSound);
        }
    }


    // =========================================================
    // RESTART
    // =========================================================

    public void RestartButton()
    {
        Time.timeScale = 1f;

        PlayButtonSound();

        Scene currentScene = SceneManager.GetActiveScene();

        SceneManager.LoadScene(currentScene.buildIndex);
    }


    // =========================================================
    // EXIT
    // =========================================================

    public void ExitButton()
    {
        Time.timeScale = 1f;

        Debug.Log("Quit Game");

        PlayButtonSound();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }


    // =========================================================
    // GAME OVER
    // =========================================================

    public void GameOver()
    {
        // Get statistics BEFORE stopping the game
        UpdateGameOverStats();

        // Stop gameplay
        Time.timeScale = 0f;

        // Hide gameplay UI
        if (gamePanel != null)
            gamePanel.SetActive(false);

        if (pausePanel != null)
            pausePanel.SetActive(false);

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        // Show Game Over
        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);
    }


    // =========================================================
    // GAME OVER STATISTICS
    // =========================================================

    private void UpdateGameOverStats()
    {
        if (MissionManager.Instance == null)
        {
            Debug.LogWarning("MissionManager.Instance is missing!");
            return;
        }


        // -------------------------
        // Deliveries
        // -------------------------

        int deliveries =
            MissionManager.Instance.GetDeliveriesCompleted();


        // -------------------------
        // Distance
        // -------------------------

        float distance =
            MissionManager.Instance.GetTotalDistanceTravelled();


        // -------------------------
        // DELIVERY TEXT
        // -------------------------

        if (deliveriesText != null)
        {
            deliveriesText.text = " " +deliveries.ToString();
        }


        // -------------------------
        // DISTANCE TEXT
        // -------------------------

        if (distanceText != null)
        {
            if (distance >= 1000f)
            {
                float kilometers = distance / 1000f;

                distanceText.text = " " + kilometers.ToString("F2") + " km";
            }
            else
            {
                distanceText.text = " " +  Mathf.RoundToInt(distance) + " m";
            }
        }


        // -------------------------
        // DEBUG
        // -------------------------

        Debug.Log( "GAME OVER | " + "Deliveries: " + deliveries + " | Distance: " + distance.ToString("F1") + " m" );
    }
}