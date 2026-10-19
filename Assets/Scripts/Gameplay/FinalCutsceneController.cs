using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Abyssal.Player;
using Abyssal.Scanner;
using Abyssal.UI;

namespace Abyssal.Gameplay
{
    /// <summary>
    /// Master coordinator for the ABYSSAL Final Horror Cutscene.
    /// Manages the state machine, total player control lock, procedural camera choreography,
    /// audio cues, egg nest reaction, Ghost Leviathan reveal and high-speed attack charge,
    /// deterministic lethal impact and player death, and cinematic fade to ending screen.
    /// </summary>
    public class FinalCutsceneController : MonoBehaviour
    {
        public static FinalCutsceneController Instance { get; private set; }

        public enum CutsceneState
        {
            NotStarted,
            PhaseA_ApproachCrystal,
            PhaseB_EggsReact,
            PhaseC_DarknessMovement,
            PhaseD_LeviathanReveal,
            PhaseE_Charge,
            PhaseF_ImpactDeath,
            PhaseG_EndingScreen
        }

        [Header("State")]
        [SerializeField] private CutsceneState currentState = CutsceneState.NotStarted;
        [SerializeField] private bool isCutsceneActive = false;

        [Header("Player References")]
        [SerializeField] private Transform diverTransform;
        [SerializeField] private DiverMovement diverMovement;
        [SerializeField] private FirstPersonCamera diverCamera;
        [SerializeField] private DiverHealth diverHealth;
        [SerializeField] private ScannerHUD scannerHUD;

        [Header("Environment References")]
        [SerializeField] private Transform crystalTransform;
        [SerializeField] private Light crystalLight;
        [SerializeField] private Transform eggNestTransform;
        [SerializeField] private List<Transform> eggTransforms = new List<Transform>();

        [Header("Ghost Leviathan References")]
        [SerializeField] private GameObject ghostLeviathanObject;
        [SerializeField] private Animator ghostLeviathanAnimator;
        [SerializeField] private Vector3 spawnPosition = new Vector3(0f, -18f, 195f);
        [SerializeField] private Vector3 revealPosition = new Vector3(0f, -19f, 175f);

        [Header("Audio Sources & Clips")]
        [SerializeField] private AudioSource cutsceneAudioSource;
        [SerializeField] private AudioSource ambientAudioSource;
        [SerializeField] private AudioClip roarClip;
        [SerializeField] private AudioClip rumbleClip;
        [SerializeField] private AudioClip impactClip;
        [SerializeField] private AudioClip glitchClip;
        [SerializeField] private AudioClip rushClip;

        [Header("Phase Timing Configuration (Seconds)")]
        [Tooltip("Phase A: Diver approaches crystal, controls lock, scanner malfunctions (0-3s).")]
        [SerializeField] private float phaseADuration = 3.0f;

        [Tooltip("Phase B: Eggs react, subtle vibration, cavern rumble (3-6s).")]
        [SerializeField] private float phaseBDuration = 3.0f;

        [Tooltip("Phase C: Camera turns to darkness ahead, silhouette stirs in fog (6-10s).")]
        [SerializeField] private float phaseCDuration = 4.0f;

        [Tooltip("Phase D: Ghost Leviathan reveal through fog, roar, dread pause (10-15s).")]
        [SerializeField] private float phaseDDuration = 5.0f;

        [Tooltip("Phase E: Sudden acceleration charge directly toward diver (15-18s).")]
        [SerializeField] private float phaseEDuration = 3.2f;

        [Tooltip("Phase F: Violent camera jolt and lethal impact (18-21s).")]
        [SerializeField] private float impactShakeDuration = 1.2f;
        [SerializeField] private float impactShakeIntensity = 0.5f;

        [Tooltip("Phase G: Screen fade to black duration.")]
        [SerializeField] private float fadeDuration = 2.0f;

        [Tooltip("Phase G: Delay in pure darkness before showing SIGNAL LOST.")]
        [SerializeField] private float endingTextDelay = 1.0f;

        // UI & Fade State
        private float screenFadeAlpha = 0f;
        private bool showEndingScreen = false;
        private Texture2D blackTexture;
        private GUIStyle titleStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle promptStyle;

        // Internal tracking
        private Vector3[] eggBasePositions;
        private Quaternion cameraOriginalRot;
        private float originalAmbientVolume = 0.55f;

        public CutsceneState CurrentState => currentState;
        public bool IsCutsceneActive => isCutsceneActive;

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

            CreateTextures();
            SetupAudioSources();
        }

        private void Start()
        {
            ResolveSceneReferences();
            CacheEggPositions();
            PrepareProceduralAudio();
        }

        private void Update()
        {
            if (showEndingScreen)
            {
                // Allow restarting only when final ending screen is displayed
                if (Input.GetKeyDown(KeyCode.R))
                {
                    Scene activeScene = SceneManager.GetActiveScene();
                    SceneManager.LoadScene(activeScene.buildIndex);
                }
            }
        }

        public void StartCutscene()
        {
            if (isCutsceneActive) return;
            isCutsceneActive = true;

            ResolveSceneReferences();
            StartCoroutine(ExecuteCutsceneSequence());
        }

        private IEnumerator ExecuteCutsceneSequence()
        {
            Debug.Log("[FinalCutsceneController] === STARTING FINAL HORROR CINEMATIC SEQUENCE ===");

            // -------------------------------------------------------------------
            // PHASE A: The Diver Approaches the Crystal (0 - 3s)
            // Lock controls immediately, freeze scanner, fade underwater ambience
            // -------------------------------------------------------------------
            currentState = CutsceneState.PhaseA_ApproachCrystal;

            // 1. Strict Player Control Lock
            if (diverMovement != null) diverMovement.SetControlsEnabled(false);
            if (diverCamera != null) diverCamera.SetControlsEnabled(false);

            // Hide normal gameplay HUD prompts
            var hud = FindAnyObjectByType<GameHUDManager>();
            if (hud != null) hud.enabled = false;

            // 2. Scanner Malfunction
            if (scannerHUD != null) scannerHUD.TriggerMalfunction();
            PlaySound(glitchClip, 0.75f);

            // 3. Ambient Audio Fade Down
            if (ambientAudioSource != null)
            {
                originalAmbientVolume = ambientAudioSource.volume;
                StartCoroutine(FadeAudioVolume(ambientAudioSource, 0.15f, 2.0f));
            }

            // 4. Smooth Camera Framing towards Crystal
            Vector3 crystalLookTarget = crystalTransform != null
                ? crystalTransform.position + Vector3.up * 1.5f
                : (diverTransform != null ? diverTransform.position + Vector3.forward * 8f : Vector3.zero);

            float elapsed = 0f;
            while (elapsed < phaseADuration)
            {
                elapsed += Time.deltaTime;
                if (diverCamera != null)
                {
                    diverCamera.SmoothLookToward(crystalLookTarget, 2.2f);
                }
                yield return null;
            }

            // -------------------------------------------------------------------
            // PHASE B: The Eggs React (3 - 6s)
            // Camera tilts down to eggs, subtle vibration, crystal light pulse, rumble
            // -------------------------------------------------------------------
            currentState = CutsceneState.PhaseB_EggsReact;

            PlaySound(rumbleClip, 0.7f);

            Vector3 eggLookTarget = eggNestTransform != null
                ? eggNestTransform.position
                : (crystalTransform != null ? crystalTransform.position + Vector3.down * 1.5f : Vector3.zero);

            Color originalCrystalColor = crystalLight != null ? crystalLight.color : Color.cyan;
            float originalCrystalIntensity = crystalLight != null ? crystalLight.intensity : 6f;

            elapsed = 0f;
            while (elapsed < phaseBDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / phaseBDuration;

                // Frame eggs
                if (diverCamera != null)
                {
                    diverCamera.SmoothLookToward(eggLookTarget, 1.8f);
                }

                // Subtle egg jitter / vibration
                JitterEggs(0.025f * Mathf.Sin(t * Mathf.PI * 4f));

                // Subtle crystal light pulse
                if (crystalLight != null)
                {
                    crystalLight.intensity = originalCrystalIntensity + Mathf.Sin(t * Mathf.PI * 3f) * 3.5f;
                    crystalLight.color = Color.Lerp(originalCrystalColor, new Color(0.85f, 0.2f, 1.0f), Mathf.PingPong(t * 2f, 0.6f));
                }

                yield return null;
            }

            ResetEggPositions();

            // -------------------------------------------------------------------
            // PHASE C: Something Moves in the Darkness (6 - 10s)
            // Camera turns smoothly toward the dark cavern passage ahead (Z = 195)
            // Ambient drops further, deep rumble swells, silhouette stirs in fog
            // -------------------------------------------------------------------
            currentState = CutsceneState.PhaseC_DarknessMovement;

            if (ambientAudioSource != null)
            {
                StartCoroutine(FadeAudioVolume(ambientAudioSource, 0.05f, 2.0f));
            }

            PlaySound(rumbleClip, 0.85f);

            // Dark passage is directly along the canyon ahead (+Z towards 195)
            Vector3 darkPassageLookTarget = new Vector3(0f, -18f, 195f);
            if (ghostLeviathanObject != null)
            {
                ghostLeviathanObject.SetActive(true);
                ghostLeviathanObject.transform.position = spawnPosition;
                ghostLeviathanObject.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            }

            elapsed = 0f;
            while (elapsed < phaseCDuration)
            {
                elapsed += Time.deltaTime;
                if (diverCamera != null)
                {
                    diverCamera.SmoothLookToward(darkPassageLookTarget, 1.5f);
                }

                // Silhouette subtle stirring motion in distant darkness
                if (ghostLeviathanObject != null)
                {
                    float driftY = Mathf.Sin(Time.time * 0.8f) * 0.5f;
                    ghostLeviathanObject.transform.position = spawnPosition + new Vector3(0f, driftY, 0f);
                }

                yield return null;
            }

            // -------------------------------------------------------------------
            // PHASE D: Ghost Leviathan Reveal (10 - 15s)
            // Creature emerges from fog, internal bioluminescent glow, mighty roar, dread pause
            // -------------------------------------------------------------------
            currentState = CutsceneState.PhaseD_LeviathanReveal;

            // Enable Leviathan Animator swimming motion
            if (ghostLeviathanAnimator != null)
            {
                ghostLeviathanAnimator.enabled = true;
                ghostLeviathanAnimator.speed = 1.0f;
            }

            // Move Leviathan from deep darkness (Z = 195) forward into fog boundary (Z = 175)
            float moveDuration = phaseDDuration * 0.65f;
            elapsed = 0f;

            // Play the iconic Leviathan roar
            PlaySound(roarClip, 1.0f);

            while (elapsed < moveDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / moveDuration);

                if (ghostLeviathanObject != null)
                {
                    ghostLeviathanObject.transform.position = Vector3.Lerp(spawnPosition, revealPosition, t);
                    // Ensure Leviathan faces the diver's camera
                    if (diverCamera != null)
                    {
                        Vector3 dirToPlayer = (diverCamera.transform.position - ghostLeviathanObject.transform.position).normalized;
                        if (dirToPlayer.sqrMagnitude > 0.001f)
                        {
                            ghostLeviathanObject.transform.rotation = Quaternion.LookRotation(dirToPlayer);
                        }
                    }
                }

                if (diverCamera != null && ghostLeviathanObject != null)
                {
                    // Focus camera lock onto the Leviathan's head/eyes
                    Vector3 creatureHeadPos = ghostLeviathanObject.transform.position + Vector3.down * 0.5f;
                    diverCamera.SmoothLookToward(creatureHeadPos, 2.5f);
                }

                yield return null;
            }

            // Dread Pause: Leviathan halts position, looming gigantically in tense silence
            float pauseDuration = phaseDDuration - moveDuration;
            elapsed = 0f;
            while (elapsed < pauseDuration)
            {
                elapsed += Time.deltaTime;
                if (diverCamera != null && ghostLeviathanObject != null)
                {
                    Vector3 creatureHeadPos = ghostLeviathanObject.transform.position + Vector3.down * 0.5f;
                    diverCamera.SmoothLookToward(creatureHeadPos, 3.0f);
                }
                yield return null;
            }

            // -------------------------------------------------------------------
            // PHASE E: The High-Speed Attack Charge (15 - 18s)
            // Accelerates exponentially directly toward the diver's camera
            // -------------------------------------------------------------------
            currentState = CutsceneState.PhaseE_Charge;

            PlaySound(rushClip, 0.95f);

            Vector3 chargeStartPos = ghostLeviathanObject != null ? ghostLeviathanObject.transform.position : revealPosition;
            Vector3 chargeTargetPos = diverCamera != null ? diverCamera.transform.position : new Vector3(0f, -22f, 150f);

            // Increase swimming animation speed during charge
            if (ghostLeviathanAnimator != null)
            {
                ghostLeviathanAnimator.speed = 1.8f;
            }

            elapsed = 0f;
            while (elapsed < phaseEDuration)
            {
                elapsed += Time.deltaTime;
                float normalizedTime = Mathf.Clamp01(elapsed / phaseEDuration);

                // Non-linear acceleration curve: starts deliberate, accelerates to extreme velocity
                float curveT = Mathf.Pow(normalizedTime, 2.2f);

                if (ghostLeviathanObject != null)
                {
                    ghostLeviathanObject.transform.position = Vector3.Lerp(chargeStartPos, chargeTargetPos, curveT);

                    Vector3 dir = (chargeTargetPos - ghostLeviathanObject.transform.position).normalized;
                    if (dir.sqrMagnitude > 0.001f)
                    {
                        ghostLeviathanObject.transform.rotation = Quaternion.LookRotation(dir);
                    }

                    // Check if creature has reached close proximity to trigger impact
                    float dist = Vector3.Distance(ghostLeviathanObject.transform.position, chargeTargetPos);
                    if (dist < 2.0f && normalizedTime > 0.75f)
                    {
                        break;
                    }
                }

                if (diverCamera != null && ghostLeviathanObject != null)
                {
                    diverCamera.SmoothLookToward(ghostLeviathanObject.transform.position, 4.5f);
                }

                yield return null;
            }

            // -------------------------------------------------------------------
            // PHASE F: Impact and Deterministic Death (18 - 21s)
            // Violent camera jolt/shake, impact crunch, diver health zeroed & death invoked
            // -------------------------------------------------------------------
            currentState = CutsceneState.PhaseF_ImpactDeath;

            PlaySound(impactClip, 1.0f);

            // Detach Main Camera from Diver player so it survives player destruction!
            if (diverCamera != null && diverCamera.transform.parent != null)
            {
                diverCamera.transform.SetParent(this.transform, worldPositionStays: true);
            }

            // Deterministic Lethal Damage: Diver suit compromised, died as required
            if (diverHealth != null)
            {
                diverHealth.TriggerCinematicDeath(2.0f);
            }

            // Violent Camera Jolt & Shake
            yield return StartCoroutine(CameraShakeRoutine(impactShakeDuration, impactShakeIntensity));

            // -------------------------------------------------------------------
            // PHASE G: Final Fade to Black and Ending Screen (21 - 30s)
            // Fade screen completely, pause in silence, display ABYSSAL — SIGNAL LOST
            // -------------------------------------------------------------------
            currentState = CutsceneState.PhaseG_EndingScreen;

            // Fade to complete black
            elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                screenFadeAlpha = Mathf.Clamp01(elapsed / fadeDuration);
                yield return null;
            }
            screenFadeAlpha = 1.0f;

            // Silence all ambient sound
            if (ambientAudioSource != null) ambientAudioSource.Stop();
            if (cutsceneAudioSource != null) cutsceneAudioSource.Stop();

            // Pause in pitch darkness
            yield return new WaitForSeconds(endingTextDelay);

            // Show clean, restrained ending screen
            showEndingScreen = true;
            Debug.Log("[FinalCutsceneController] === CINEMATIC SEQUENCE COMPLETE: SIGNAL LOST ===");
        }

        private IEnumerator CameraShakeRoutine(float duration, float intensity)
        {
            if (diverCamera == null) yield break;

            Quaternion originalRot = diverCamera.transform.rotation;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float strength = Mathf.Lerp(intensity, 0f, elapsed / duration);

                float pitchOffset = UnityEngine.Random.Range(-1f, 1f) * strength * 20f;
                float yawOffset = UnityEngine.Random.Range(-1f, 1f) * strength * 20f;
                float rollOffset = UnityEngine.Random.Range(-1f, 1f) * strength * 25f;

                diverCamera.transform.rotation = originalRot * Quaternion.Euler(pitchOffset, yawOffset, rollOffset);
                yield return null;
            }

            diverCamera.transform.rotation = originalRot;
        }

        private IEnumerator FadeAudioVolume(AudioSource source, float targetVol, float duration)
        {
            if (source == null) yield break;
            float startVol = source.volume;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                source.volume = Mathf.Lerp(startVol, targetVol, elapsed / duration);
                yield return null;
            }
            source.volume = targetVol;
        }

        private void JitterEggs(float magnitude)
        {
            if (eggTransforms == null || eggBasePositions == null) return;
            for (int i = 0; i < eggTransforms.Count && i < eggBasePositions.Length; i++)
            {
                if (eggTransforms[i] != null)
                {
                    Vector3 jitter = UnityEngine.Random.insideUnitSphere * magnitude;
                    eggTransforms[i].position = eggBasePositions[i] + jitter;
                }
            }
        }

        private void ResetEggPositions()
        {
            if (eggTransforms == null || eggBasePositions == null) return;
            for (int i = 0; i < eggTransforms.Count && i < eggBasePositions.Length; i++)
            {
                if (eggTransforms[i] != null)
                {
                    eggTransforms[i].position = eggBasePositions[i];
                }
            }
        }

        private void PlaySound(AudioClip clip, float volume = 1.0f)
        {
            if (clip == null || cutsceneAudioSource == null) return;
            cutsceneAudioSource.PlayOneShot(clip, volume);
        }

        private void ResolveSceneReferences()
        {
            if (diverTransform == null)
            {
                GameObject diverGO = GameObject.FindWithTag("Player");
                if (diverGO == null) diverGO = GameObject.Find("Diver");
                if (diverGO != null) diverTransform = diverGO.transform;
            }

            if (diverTransform != null)
            {
                if (diverMovement == null) diverMovement = diverTransform.GetComponent<DiverMovement>();
                if (diverCamera == null) diverCamera = diverTransform.GetComponentInChildren<FirstPersonCamera>();
                if (diverHealth == null) diverHealth = diverTransform.GetComponent<DiverHealth>();
                if (scannerHUD == null) scannerHUD = diverTransform.GetComponent<ScannerHUD>();
            }

            if (diverCamera == null)
            {
                diverCamera = FindAnyObjectByType<FirstPersonCamera>();
            }

            if (crystalTransform == null)
            {
                GameObject crystalGO = GameObject.Find("MagicCrystal");
                if (crystalGO != null) crystalTransform = crystalGO.transform;
            }

            if (crystalTransform != null && crystalLight == null)
            {
                crystalLight = crystalTransform.GetComponentInChildren<Light>();
            }

            if (ghostLeviathanObject == null)
            {
                ghostLeviathanObject = GameObject.Find("Ghost_Leviathan");
                if (ghostLeviathanObject == null) ghostLeviathanObject = GameObject.Find("GhostLeviathan");
            }

            if (ghostLeviathanObject != null && ghostLeviathanAnimator == null)
            {
                ghostLeviathanAnimator = ghostLeviathanObject.GetComponentInChildren<Animator>();
            }

            if (ambientAudioSource == null && Camera.main != null)
            {
                ambientAudioSource = Camera.main.GetComponent<AudioSource>();
            }
        }

        private void CacheEggPositions()
        {
            if (eggNestTransform == null)
            {
                GameObject nestGO = GameObject.Find("Leviathan_Egg_Nest");
                if (nestGO != null) eggNestTransform = nestGO.transform;
            }

            if (eggNestTransform != null && eggTransforms.Count == 0)
            {
                foreach (Transform child in eggNestTransform)
                {
                    if (child.name.Contains("Egg"))
                    {
                        eggTransforms.Add(child);
                    }
                }
            }

            if (eggTransforms.Count > 0)
            {
                eggBasePositions = new Vector3[eggTransforms.Count];
                for (int i = 0; i < eggTransforms.Count; i++)
                {
                    eggBasePositions[i] = eggTransforms[i].position;
                }
            }
        }

        private void SetupAudioSources()
        {
            if (cutsceneAudioSource == null)
            {
                cutsceneAudioSource = GetComponent<AudioSource>();
                if (cutsceneAudioSource == null) cutsceneAudioSource = gameObject.AddComponent<AudioSource>();
                cutsceneAudioSource.playOnAwake = false;
                cutsceneAudioSource.spatialBlend = 0f; // 2D direct cinematic mix
            }
        }

        private void PrepareProceduralAudio()
        {
            // Load official roar clip if not assigned
            if (roarClip == null)
            {
                roarClip = Resources.Load<AudioClip>("Audio/Leviathan_Roar");
            }

            // Synthesize high-quality procedural sound effects if asset clips are null
            if (glitchClip == null) glitchClip = SynthesizeGlitchSound();
            if (rumbleClip == null) rumbleClip = SynthesizeSubRumbleSound();
            if (rushClip == null) rushClip = SynthesizeRushingWaterSound();
            if (impactClip == null) impactClip = SynthesizeImpactCrunchSound();
        }

        // =======================================================================
        // PROCEDURAL AUDIO SYNTHESIZERS (Guarantees zero missing sound dependencies)
        // =======================================================================
        private AudioClip SynthesizeGlitchSound()
        {
            int sampleRate = 44100;
            float duration = 0.45f;
            int numSamples = (int)(sampleRate * duration);
            float[] samples = new float[numSamples];

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float freq = UnityEngine.Random.Range(200f, 1800f);
                float noise = (UnityEngine.Random.value * 2f - 1f) * 0.4f;
                float square = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * freq * t)) * 0.3f;
                float env = Mathf.Exp(-t * 8f);
                samples[i] = (noise + square) * env;
            }

            AudioClip clip = AudioClip.Create("Synthesized_Glitch", numSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip SynthesizeSubRumbleSound()
        {
            int sampleRate = 44100;
            float duration = 3.5f;
            int numSamples = (int)(sampleRate * duration);
            float[] samples = new float[numSamples];

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float sub = Mathf.Sin(2f * Mathf.PI * 38f * t) * 0.5f;
                float sub2 = Mathf.Sin(2f * Mathf.PI * 24f * t) * 0.35f;
                float noise = (UnityEngine.Random.value * 2f - 1f) * 0.15f;
                float env = Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI);
                samples[i] = (sub + sub2 + noise) * env;
            }

            AudioClip clip = AudioClip.Create("Synthesized_Rumble", numSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip SynthesizeRushingWaterSound()
        {
            int sampleRate = 44100;
            float duration = 3.2f;
            int numSamples = (int)(sampleRate * duration);
            float[] samples = new float[numSamples];

            float lastSample = 0f;
            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float white = UnityEngine.Random.value * 2f - 1f;
                // Simple low-pass filter
                lastSample = Mathf.Lerp(lastSample, white, 0.18f);
                float accelEnv = Mathf.Pow(t / duration, 1.8f);
                samples[i] = lastSample * accelEnv * 0.75f;
            }

            AudioClip clip = AudioClip.Create("Synthesized_RushingWater", numSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip SynthesizeImpactCrunchSound()
        {
            int sampleRate = 44100;
            float duration = 1.4f;
            int numSamples = (int)(sampleRate * duration);
            float[] samples = new float[numSamples];

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float thud = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(85f, 28f, t) * t) * 0.7f;
                float crack = (UnityEngine.Random.value * 2f - 1f) * Mathf.Exp(-t * 22f) * 0.8f;
                float env = Mathf.Exp(-t * 4.5f);
                samples[i] = Mathf.Clamp((thud + crack) * env, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Synthesized_Impact", numSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        // =======================================================================
        // CINEMATIC FADE & ENDING UI RENDERING
        // =======================================================================
        private void CreateTextures()
        {
            blackTexture = new Texture2D(2, 2);
            Color[] pix = new Color[4] { Color.black, Color.black, Color.black, Color.black };
            blackTexture.SetPixels(pix);
            blackTexture.Apply();
        }

        private void OnGUI()
        {
            if (screenFadeAlpha > 0.001f && blackTexture != null)
            {
                Color prevCol = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, screenFadeAlpha);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), blackTexture);
                GUI.color = prevCol;
            }

            if (showEndingScreen)
            {
                DrawEndingTypography();
            }
        }

        private void DrawEndingTypography()
        {
            float screenW = Screen.width;
            float screenH = Screen.height;

            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 42,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.85f, 0.92f, 0.98f) }
                };

                subtitleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 20,
                    fontStyle = FontStyle.Italic,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.9f, 0.25f, 0.25f) }
                };

                promptStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.45f, 0.55f, 0.65f) }
                };
            }

            // Title: ABYSSAL
            Rect titleRect = new Rect(0, screenH * 0.42f - 40f, screenW, 55f);
            GUI.Label(titleRect, "A B Y S S A L", titleStyle);

            // Subtitle: SIGNAL LOST
            Rect subRect = new Rect(0, screenH * 0.42f + 25f, screenW, 35f);
            GUI.Label(subRect, "— SIGNAL LOST —", subtitleStyle);

            // Subtle instruction
            Rect promptRect = new Rect(0, screenH * 0.78f, screenW, 25f);
            GUI.Label(promptRect, "Press [R] to retry expedition", promptStyle);
        }
    }
}
