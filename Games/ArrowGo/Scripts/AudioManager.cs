using UnityEngine;

namespace OMG.ArrowGo
{
    public class AudioManager : MonoBehaviour
    {
        // ============================================================
        // SINGLETON
        // ============================================================

        public static AudioManager Instance { get; private set; }

        // ============================================================
        // AUDIO SOURCES
        // ============================================================

        [Header("Audio Sources")]
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource soundSource;

        // ============================================================
        // SETTINGS
        // ============================================================

        [Header("Settings")]
        [SerializeField] private bool musicOn = true;
        [SerializeField] private bool soundOn = true;

        // ============================================================
        // PLAYER PREFS KEYS
        // ============================================================

        private const string MusicKey = "MusicOn";
        private const string SoundKey = "SoundOn";

        // ============================================================
        // UNITY - AWAKE
        // ============================================================

        private void Awake()
        {
            // --------------------------------------------------------
            // SINGLETON
            // --------------------------------------------------------

            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            // --------------------------------------------------------
            // MAKE SURE AUDIO MANAGER IS ROOT
            // --------------------------------------------------------

            if (transform.parent != null)
            {
                transform.SetParent(null);
            }

            // --------------------------------------------------------
            // PERSIST BETWEEN SCENES
            // --------------------------------------------------------

            DontDestroyOnLoad(gameObject);

            // --------------------------------------------------------
            // LOAD SETTINGS
            // --------------------------------------------------------

            LoadSettings();

            // --------------------------------------------------------
            // APPLY SETTINGS
            // --------------------------------------------------------

            ApplySettings();
        }

        // ============================================================
        // LOAD SETTINGS
        // ============================================================

        private void LoadSettings()
        {
            musicOn = PlayerPrefs.GetInt(MusicKey, 1) == 1;
            soundOn = PlayerPrefs.GetInt(SoundKey, 1) == 1;
        }

        // ============================================================
        // SAVE SETTINGS
        // ============================================================

        private void SaveSettings()
        {
            PlayerPrefs.SetInt(
                MusicKey,
                musicOn ? 1 : 0
            );

            PlayerPrefs.SetInt(
                SoundKey,
                soundOn ? 1 : 0
            );

            PlayerPrefs.Save();
        }

        // ============================================================
        // APPLY SETTINGS
        // ============================================================

        private void ApplySettings()
        {
            ApplyMusic();
        }

        // ============================================================
        // MUSIC
        // ============================================================

        public void ToggleMusic(bool value)
        {
            musicOn = value;

            SaveSettings();

            ApplyMusic();
        }

        private void ApplyMusic()
        {
            if (musicSource == null)
                return;

            musicSource.mute = !musicOn;
        }

        // ============================================================
        // SOUND
        // ============================================================

        public void ToggleSound(bool value)
        {
            soundOn = value;

            SaveSettings();
        }

        // ============================================================
        // PLAY SOUND
        // ============================================================

        public void PlaySound(AudioClip clip)
        {
            if (!soundOn)
                return;

            if (soundSource == null)
                return;

            if (clip == null)
                return;

            soundSource.PlayOneShot(clip);
        }

        // ============================================================
        // GET MUSIC STATE
        // ============================================================

        public bool IsMusicOn()
        {
            return musicOn;
        }

        // ============================================================
        // GET SOUND STATE
        // ============================================================

        public bool IsSoundOn()
        {
            return soundOn;
        }

        // ============================================================
        // PUBLIC AUDIO SOURCES
        // ============================================================

        public AudioSource GetMusicSource()
        {
            return musicSource;
        }

        public AudioSource GetSoundSource()
        {
            return soundSource;
        }
    }
}