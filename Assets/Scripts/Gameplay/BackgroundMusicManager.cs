using UnityEngine;

namespace Abyssal.Gameplay
{
    /// <summary>
    /// Manages the background immersion soundtrack ("First Immersion.mp3").
    /// Automatically ensures playback begins as soon as the game becomes active,
    /// loops continuously during exploration and cinematic tension,
    /// and stops precisely when the signal is lost at the final cutscene.
    /// </summary>
    public class BackgroundMusicManager : MonoBehaviour
    {
        public static BackgroundMusicManager Instance { get; private set; }

        [Header("Music Settings")]
        [Tooltip("The background immersion soundtrack clip.")]
        [SerializeField] private AudioClip musicClip;

        [Range(0f, 1f)]
        [Tooltip("Playback volume of the background music.")]
        [SerializeField] private float musicVolume = 0.65f;

        [Tooltip("Whether the music should loop while the game is active.")]
        [SerializeField] private bool loopMusic = true;

        [Header("Audio Component")]
        [SerializeField] private AudioSource audioSource;

        public bool IsPlaying => audioSource != null && audioSource.isPlaying;
        public float Volume => musicVolume;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance == null)
            {
                var existing = FindAnyObjectByType<BackgroundMusicManager>();
                if (existing == null)
                {
                    GameObject musicGO = new GameObject("BackgroundMusicManager");
                    musicGO.AddComponent<BackgroundMusicManager>();
                    Debug.Log("[BackgroundMusicManager] Initialized background music manager on scene load.");
                }
            }
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            SetupAudioSource();
        }

        private void Start()
        {
            EnsureClipLoaded();
            StartMusic();
        }

        private void EnsureClipLoaded()
        {
            if (musicClip == null)
            {
                musicClip = Resources.Load<AudioClip>("Audio/First Immersion");
#if UNITY_EDITOR
                if (musicClip == null)
                {
                    musicClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/First Immersion.mp3");
                }
#endif
            }
        }

        private void SetupAudioSource()
        {
            if (audioSource == null && Camera.main != null)
            {
                var camSource = Camera.main.GetComponent<AudioSource>();
                if (camSource != null)
                {
                    audioSource = camSource;
                }
            }

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            audioSource.loop = loopMusic;
            audioSource.volume = musicVolume;
            audioSource.spatialBlend = 0f; // Pure 2D stereo background immersion soundtrack
            audioSource.bypassEffects = true;
            audioSource.bypassListenerEffects = true;
            audioSource.bypassReverbZones = true;
        }

        /// <summary>
        /// Starts or resumes playback of the background music.
        /// </summary>
        public void StartMusic()
        {
            if (audioSource == null || (Camera.main != null && audioSource.gameObject != Camera.main.gameObject))
            {
                SetupAudioSource();
            }

            EnsureClipLoaded();

            if (musicClip != null)
            {
                if (audioSource.clip != musicClip)
                {
                    audioSource.clip = musicClip;
                }
                audioSource.volume = musicVolume;
                audioSource.loop = loopMusic;
                if (!audioSource.isPlaying)
                {
                    audioSource.Play();
                    Debug.Log("[BackgroundMusicManager] 'First Immersion' background music started.");
                }
            }
            else if (audioSource != null && audioSource.clip != null && !audioSource.isPlaying)
            {
                audioSource.Play();
                Debug.Log("[BackgroundMusicManager] Background audio source playback resumed.");
            }
            else
            {
                Debug.LogWarning("[BackgroundMusicManager] 'First Immersion' audio clip not found!");
            }
        }

        private bool isStoppedByCutscene = false;

        private void Update()
        {
            if (isStoppedByCutscene) return;

            if (audioSource == null || (Camera.main != null && audioSource.gameObject != Camera.main.gameObject))
            {
                SetupAudioSource();
            }

            if (audioSource != null && !audioSource.isPlaying)
            {
                StartMusic();
            }
        }

        /// <summary>
        /// Stops background music playback immediately (e.g. at SIGNAL LOST).
        /// </summary>
        public void StopPlayback()
        {
            isStoppedByCutscene = true;
            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
                Debug.Log("[BackgroundMusicManager] Background music stopped (SIGNAL LOST).");
            }
        }

        /// <summary>
        /// Static convenience method to stop music from any script.
        /// </summary>
        public static void StopMusic()
        {
            if (Instance != null)
            {
                Instance.StopPlayback();
            }
            else
            {
                var mgr = FindAnyObjectByType<BackgroundMusicManager>();
                if (mgr != null)
                {
                    mgr.StopPlayback();
                }
            }
        }

        /// <summary>
        /// Static convenience method to start or resume music from any script.
        /// </summary>
        public static void PlayMusic()
        {
            if (Instance != null)
            {
                Instance.StartMusic();
            }
            else
            {
                var mgr = FindAnyObjectByType<BackgroundMusicManager>();
                if (mgr != null)
                {
                    mgr.StartMusic();
                }
            }
        }

        /// <summary>
        /// Adjusts music volume dynamically.
        /// </summary>
        public void SetVolume(float volume)
        {
            musicVolume = Mathf.Clamp01(volume);
            if (audioSource != null)
            {
                audioSource.volume = musicVolume;
            }
        }
    }
}
