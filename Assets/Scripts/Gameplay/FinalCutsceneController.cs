using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Abyssal.Player;
using Abyssal.Scanner;
using Abyssal.UI;

namespace Abyssal.Gameplay
{
    /// <summary>
    /// Master coordinator for the ABYSSAL Final Horror Cutscene.
    /// Manages the state machine, total player control lock, procedural camera choreography,
    /// audio cues, egg nest reaction, Ghost Leviathan dynamic reveal and high-speed attack charge,
    /// deterministic lethal impact and player death, camera rendering preservation, and cinematic ending screen.
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
        [SerializeField] private Transform ghostHeadTransform;
        [Tooltip("Distance directly in front of the diver's camera where the Leviathan emerges from the dark fog.")]
        [SerializeField] private float spawnDistance = 28.0f;

        [Header("Audio Sources & Clips")]
        [SerializeField] private AudioSource cutsceneAudioSource;
        [SerializeField] private AudioSource ambientAudioSource;
        [SerializeField] private AudioClip roarClip;
        [SerializeField] private AudioClip rumbleClip;
        [SerializeField] private AudioClip impactClip;
        [SerializeField] private AudioClip glitchClip;
        [SerializeField] private AudioClip rushClip;

        [Header("Phase Timing Configuration (Seconds)")]
        [Tooltip("Menacing reveal and roar right in front of the diver (emerging from fog).")]
        [SerializeField] private float revealDuration = 1.2f;

        [Tooltip("Rapid predatory charge covering the distance directly into the camera lens.")]
        [SerializeField] private float chargeDuration = 2.0f;

        [Tooltip("Violent camera jolt and lethal impact duration.")]
        [SerializeField] private float impactShakeDuration = 1.2f;
        [SerializeField] private float impactShakeIntensity = 0.85f;

        [Tooltip("Screen fade to black duration.")]
        [SerializeField] private float fadeDuration = 1.2f;

        [Tooltip("Delay in pure darkness before showing SIGNAL LOST.")]
        [SerializeField] private float endingTextDelay = 0.8f;

        // Camera Safety Fallback
        private Camera fallbackCamera;

        // UI & Fade State
        private float screenFadeAlpha = 0f;
        private bool showEndingScreen = false;
        private Texture2D blackTexture;
        private GUIStyle titleStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle promptStyle;

        // Internal tracking
        private Vector3[] eggBasePositions;
        private float originalAmbientVolume = 0.55f;

        public CutsceneState CurrentState => currentState;
        public bool IsCutsceneActive => isCutsceneActive;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitializeCutsceneSystem()
        {
            if (Instance == null)
            {
                var existing = FindAnyObjectByType<FinalCutsceneController>();
                if (existing == null)
                {
                    GameObject managerGO = new GameObject("FinalCutsceneManager");
                    managerGO.AddComponent<FinalCutsceneController>();
                    Debug.Log("[FinalCutsceneController] Automatically initialized FinalCutsceneManager on scene load.");
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

            EnsureFallbackCamera();
            CreateTextures();
            SetupAudioSources();
        }

        private void Start()
        {
            ResolveSceneReferences();
            CacheEggPositions();
            PrepareProceduralAudio();
        }

        [Header("Debug")]
        [SerializeField] private bool debugTriggerInPlayMode = false;

        private void Update()
        {
            EnsureCameraSafety();

            if (debugTriggerInPlayMode && !isCutsceneActive)
            {
                debugTriggerInPlayMode = false;
                StartCutscene();
            }

#if UNITY_EDITOR
            bool tPressed = false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame) tPressed = true;
#endif
            try { if (!tPressed && Input.GetKeyDown(KeyCode.T)) tPressed = true; } catch { }
            if (!isCutsceneActive && tPressed)
            {
                Debug.Log("[FinalCutsceneController] Debug T key pressed: Starting cutscene!");
                StartCutscene();
            }
#endif

            if (showEndingScreen)
            {
                if (CheckRestartInput())
                {
                    Scene activeScene = SceneManager.GetActiveScene();
                    SceneManager.LoadScene(activeScene.buildIndex);
                }
            }
        }

        /// <summary>
        /// Guarantees that at least one active Camera is rendering to Display 1 at all times,
        /// completely preventing the Unity 'Display 1 No cameras rendering' message.
        /// </summary>
        private void EnsureCameraSafety()
        {
            bool hasValidRenderingCamera = (diverCamera != null && diverCamera.gameObject.activeInHierarchy) ||
                                           (Camera.main != null && Camera.main.gameObject.activeInHierarchy);

            if (!hasValidRenderingCamera)
            {
                if (fallbackCamera == null)
                {
                    EnsureFallbackCamera();
                }

                if (fallbackCamera != null && !fallbackCamera.enabled)
                {
                    Debug.LogWarning("[FinalCutsceneController] Active camera lost; activating fallback camera.");
                    fallbackCamera.enabled = true;
                }
            }
            else
            {
                // Deactivate fallback camera if primary camera is actively rendering
                if (fallbackCamera != null && fallbackCamera.enabled)
                {
                    fallbackCamera.enabled = false;
                }
            }
        }

        private void EnsureFallbackCamera()
        {
            fallbackCamera = GetComponent<Camera>();
            if (fallbackCamera == null)
            {
                fallbackCamera = gameObject.AddComponent<Camera>();
            }

            fallbackCamera.clearFlags = CameraClearFlags.SolidColor;
            fallbackCamera.backgroundColor = Color.black;
            fallbackCamera.cullingMask = 0; // Pure solid background rendering for ending screen
            fallbackCamera.depth = -100;
            fallbackCamera.enabled = false;
        }

        private bool CheckRestartInput()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                return true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                if (Input.GetKeyDown(KeyCode.R))
                {
                    return true;
                }
            }
            catch (InvalidOperationException)
            {
                // Suppress exception when active Input Handling is set to new Input System
            }
#endif
            return false;
        }

        public void StartCutscene()
        {
            if (isCutsceneActive) return;
            isCutsceneActive = true;

            ResolveSceneReferences();

            // Lock controls, but keep camera firmly attached to Diver for immersive First-Person horror!
            if (diverCamera != null)
            {
                diverCamera.SetControlsEnabled(false);
            }
            if (diverMovement != null)
            {
                diverMovement.SetControlsEnabled(false);
            }

            StartCoroutine(ExecuteCutsceneSequence());
        }

        private IEnumerator ExecuteCutsceneSequence()
        {
            Debug.Log("[FinalCutsceneController] === STARTING FINAL HORROR CINEMATIC SEQUENCE ===");

            // 1. Strict Player Control Lock
            if (diverMovement != null) diverMovement.SetControlsEnabled(false);
            if (diverCamera != null) diverCamera.SetControlsEnabled(false);

            // Hide normal gameplay HUD prompts
            var hud = FindAnyObjectByType<GameHUDManager>();
            if (hud != null) hud.enabled = false;

            // 2. Ensure Ghost Leviathan instance exists, is active and configured
            EnsureLeviathanInstance();

            Vector3 diverPos = diverTransform != null ? diverTransform.position : new Vector3(0f, -22f, 125f);
            Vector3 camPos = diverCamera != null ? diverCamera.transform.position : diverPos + Vector3.up * 1.6f;

            // 3. Dynamic Spawn Calculation: Directly in front of the user's camera at a distance
            Vector3 lookDir = Vector3.forward;
            if (diverCamera != null)
            {
                lookDir = diverCamera.transform.forward;
            }
            else if (diverTransform != null)
            {
                lookDir = diverTransform.forward;
            }

            Vector3 horizontal = new Vector3(lookDir.x, 0f, lookDir.z);
            if (horizontal.sqrMagnitude < 0.01f)
            {
                horizontal = Vector3.forward;
            }
            else
            {
                horizontal.Normalize();
            }

            // Blend user forward with subtle vertical pitch to match their direct line of sight
            Vector3 spawnDir = (horizontal * 0.88f + Vector3.up * Mathf.Clamp(lookDir.y, -0.22f, 0.22f)).normalized;
            Vector3 spawnPos = camPos + spawnDir * spawnDistance + Vector3.up * 1.2f;

            if (ghostLeviathanObject != null)
            {
                ghostLeviathanObject.SetActive(true);
                ghostLeviathanObject.transform.position = spawnPos;
                OrientLeviathanToward(camPos);
            }

            LocateHeadBone();

            // 4. Scanner Glitch & Ambient Sound Drop
            if (scannerHUD != null) scannerHUD.TriggerMalfunction();
            PlaySound(glitchClip, 0.85f);

            if (ambientAudioSource != null)
            {
                originalAmbientVolume = ambientAudioSource.volume;
                StartCoroutine(FadeAudioVolume(ambientAudioSource, 0.05f, 1.5f));
            }

            // -------------------------------------------------------------------
            // PHASE 1: Terrifying Roar & Menacing Reveal in Front of User (1.2s)
            // Emerging from the dark murky fog directly in front of the diver
            // -------------------------------------------------------------------
            currentState = CutsceneState.PhaseD_LeviathanReveal;

            // Enable Leviathan swimming animation
            if (ghostLeviathanAnimator != null)
            {
                ghostLeviathanAnimator.enabled = true;
                ghostLeviathanAnimator.speed = 1.3f;
            }

            // Blood-curdling Roar & Cavern Rumble
            PlaySound(roarClip, 1.0f);
            PlaySound(rumbleClip, 0.85f);

            float revealElapsed = 0f;
            while (revealElapsed < revealDuration)
            {
                revealElapsed += Time.deltaTime;

                // Focus camera directly onto Leviathan's glowing head
                if (diverCamera != null)
                {
                    Vector3 headPos = GetHeadPosition();
                    diverCamera.SmoothLookToward(headPos, 6.0f);
                }

                OrientLeviathanToward(camPos);
                yield return null;
            }

            // -------------------------------------------------------------------
            // PHASE 2: Rapid Predatory Attack Charge Directly into Diver (2.2s)
            // High-speed aggressive acceleration straight into the camera lens
            // -------------------------------------------------------------------
            currentState = CutsceneState.PhaseE_Charge;

            PlaySound(rushClip, 1.0f);

            if (ghostLeviathanAnimator != null)
            {
                ghostLeviathanAnimator.applyRootMotion = false;
                ghostLeviathanAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                ghostLeviathanAnimator.speed = 2.8f;
            }

            Vector3 chargeStartPos = ghostLeviathanObject != null ? ghostLeviathanObject.transform.position : spawnPos;
            Vector3 finalCamPos = diverCamera != null ? diverCamera.transform.position : camPos;
            Vector3 rushDir = (finalCamPos - chargeStartPos).normalized;
            // Overshoot target by 5m so creature charges cleanly through the player's view
            Vector3 rushTarget = finalCamPos + rushDir * 5.0f;
            float chargeElapsed = 0f;

            while (chargeElapsed < chargeDuration)
            {
                chargeElapsed += Time.deltaTime;
                float normalizedT = Mathf.Clamp01(chargeElapsed / chargeDuration);

                // Smooth predatory surge curve: clearly visible rapid approach
                float curveT = Mathf.SmoothStep(0f, 1f, normalizedT);

                Vector3 currentCamPos = diverCamera != null ? diverCamera.transform.position : finalCamPos;

                if (ghostLeviathanObject != null)
                {
                    // Move the Leviathan along the charge path
                    ghostLeviathanObject.transform.position = Vector3.Lerp(chargeStartPos, rushTarget, curveT);

                    // Orient directly at camera
                    OrientLeviathanToward(currentCamPos);

                    // Check head distance to camera: trigger lethal impact upon collision
                    Vector3 headPos = GetHeadPosition();
                    float distToCam = Vector3.Distance(headPos, currentCamPos);
                    if (distToCam < 3.2f && normalizedT > 0.35f)
                    {
                        break; // Lethal bite impact!
                    }
                }

                if (diverCamera != null)
                {
                    Vector3 headPos = GetHeadPosition();
                    diverCamera.SmoothLookToward(headPos, 16.0f);
                }

                yield return null;
            }

            // -------------------------------------------------------------------
            // PHASE 3: Lethal Impact, Violent Shake & Suit Destruction (1.2s)
            // Massive jaws crush into camera, suit breached, health zeroed
            // -------------------------------------------------------------------
            currentState = CutsceneState.PhaseF_ImpactDeath;

            PlaySound(impactClip, 1.0f);

            // Double guarantee camera is detached and parented to FinalCutsceneManager
            if (diverCamera != null && diverCamera.transform.parent != this.transform)
            {
                diverCamera.transform.SetParent(this.transform, worldPositionStays: true);
            }

            // Fatal Damage: Suit breached, health zeroed
            if (diverHealth != null)
            {
                diverHealth.TriggerCinematicDeath(1.5f);
            }

            // Violent Camera Jolt & Shake
            yield return StartCoroutine(CameraShakeRoutine(impactShakeDuration, impactShakeIntensity));

            // -------------------------------------------------------------------
            // PHASE 4: Fade to Black and Restrained Ending Screen
            // -------------------------------------------------------------------
            currentState = CutsceneState.PhaseG_EndingScreen;

            float fadeElapsed = 0f;
            while (fadeElapsed < fadeDuration)
            {
                fadeElapsed += Time.deltaTime;
                screenFadeAlpha = Mathf.Clamp01(fadeElapsed / fadeDuration);
                yield return null;
            }
            screenFadeAlpha = 1.0f;

            if (ambientAudioSource != null) ambientAudioSource.Stop();
            if (cutsceneAudioSource != null) cutsceneAudioSource.Stop();

            yield return new WaitForSeconds(endingTextDelay);
            showEndingScreen = true;
            Debug.Log("[FinalCutsceneController] === CINEMATIC SEQUENCE COMPLETE: SIGNAL LOST ===");
        }

        private void EnsureLeviathanInstance()
        {
            if (ghostLeviathanObject == null)
            {
                ghostLeviathanObject = GameObject.Find("Ghost_Leviathan");
                if (ghostLeviathanObject == null) ghostLeviathanObject = GameObject.Find("GhostLeviathan");
            }

            if (ghostLeviathanObject == null)
            {
#if UNITY_EDITOR
                GameObject fbx = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ghost-leviathan/source/Ghost_Leviathan.fbx");
                if (fbx != null)
                {
                    ghostLeviathanObject = Instantiate(fbx);
                    ghostLeviathanObject.name = "Ghost_Leviathan";
                }
#endif
            }

            if (ghostLeviathanObject != null)
            {
                ghostLeviathanObject.transform.localScale = Vector3.one * 35.0f;

                if (ghostLeviathanAnimator == null)
                {
                    ghostLeviathanAnimator = ghostLeviathanObject.GetComponentInChildren<Animator>();
                    if (ghostLeviathanAnimator == null)
                    {
                        ghostLeviathanAnimator = ghostLeviathanObject.AddComponent<Animator>();
                    }
                }

                if (ghostLeviathanAnimator != null)
                {
                    ghostLeviathanAnimator.applyRootMotion = false;
                    ghostLeviathanAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                }

                foreach (var col in ghostLeviathanObject.GetComponentsInChildren<Collider>(true))
                {
                    col.isTrigger = true;
                }

#if UNITY_EDITOR
                Material ghostMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/ghost-leviathan/Materials/Ghost_Leviathan_shell_spec.mat");
                if (ghostMat != null)
                {
                    ghostMat.EnableKeyword("_EMISSION");
                    ghostMat.SetColor("_EmissionColor", new Color(0.22f, 0.88f, 1.0f) * 2.8f);
                    foreach (var r in ghostLeviathanObject.GetComponentsInChildren<Renderer>(true))
                    {
                        r.sharedMaterial = ghostMat;
                    }
                }

                var controller = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/ghost-leviathan/GhostLeviathanController.controller");
                if (controller != null && ghostLeviathanAnimator != null)
                {
                    ghostLeviathanAnimator.runtimeAnimatorController = controller;
                }
#endif
                LocateHeadBone();

                // Ensure head glow light
                Transform lightParent = ghostHeadTransform != null ? ghostHeadTransform : ghostLeviathanObject.transform;
                Light headLight = lightParent.GetComponentInChildren<Light>();
                if (headLight == null)
                {
                    GameObject lg = new GameObject("LeviathanHeadGlow");
                    lg.transform.SetParent(lightParent, false);
                    headLight = lg.AddComponent<Light>();
                }
                headLight.type = LightType.Point;
                headLight.range = 50f;
                headLight.intensity = 6.5f;
                headLight.color = new Color(0.2f, 0.9f, 1.0f);
            }
        }

        private void OrientLeviathanToward(Vector3 targetPos)
        {
            if (ghostLeviathanObject == null) return;
            Vector3 dir = (targetPos - ghostLeviathanObject.transform.position).normalized;
            if (dir.sqrMagnitude < 0.001f) return;

            ghostLeviathanObject.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }

        private Vector3 GetHeadPosition()
        {
            if (ghostHeadTransform != null)
            {
                return ghostHeadTransform.position;
            }

            if (ghostLeviathanObject != null)
            {
                return ghostLeviathanObject.transform.position + ghostLeviathanObject.transform.forward * 3.5f;
            }

            return Vector3.zero;
        }

        private void LocateHeadBone()
        {
            if (ghostHeadTransform != null) return;

            if (ghostLeviathanObject != null)
            {
                ghostHeadTransform = ghostLeviathanObject.transform.Find("Armature/Ghost_Leviathan_anim/root/neck1/neck2/neck3/head");
                if (ghostHeadTransform == null)
                {
                    foreach (Transform child in ghostLeviathanObject.GetComponentsInChildren<Transform>())
                    {
                        if (child.name.Equals("head", StringComparison.OrdinalIgnoreCase))
                        {
                            ghostHeadTransform = child;
                            break;
                        }
                    }
                }
            }
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

                float pitchOffset = UnityEngine.Random.Range(-1f, 1f) * strength * 22f;
                float yawOffset = UnityEngine.Random.Range(-1f, 1f) * strength * 22f;
                float rollOffset = UnityEngine.Random.Range(-1f, 1f) * strength * 28f;

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
            if (cutsceneAudioSource == null) return;
            if (clip != null)
            {
                cutsceneAudioSource.PlayOneShot(clip, volume);
            }
            else if (roarClip != null)
            {
                cutsceneAudioSource.PlayOneShot(roarClip, volume * 0.75f);
            }
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

            if (crystalTransform != null)
            {
                var trigger = crystalTransform.GetComponent<FinalCutsceneTrigger>();
                if (trigger == null)
                {
                    crystalTransform.gameObject.AddComponent<FinalCutsceneTrigger>();
                }
            }

            if (crystalTransform != null && crystalLight == null)
            {
                crystalLight = crystalTransform.GetComponentInChildren<Light>();
            }

            EnsureLeviathanInstance();

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
            if (roarClip == null)
            {
                roarClip = Resources.Load<AudioClip>("Audio/Leviathan_Roar");
            }

            if (glitchClip == null) glitchClip = SynthesizeGlitchSound();
            if (rumbleClip == null) rumbleClip = SynthesizeSubRumbleSound();
            if (rushClip == null) rushClip = SynthesizeRushingWaterSound();
            if (impactClip == null) impactClip = SynthesizeImpactCrunchSound();
        }

        // =======================================================================
        // PROCEDURAL AUDIO SYNTHESIZERS (Guarantees zero missing audio dependencies)
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
            float duration = 2.5f;
            int numSamples = (int)(sampleRate * duration);
            float[] samples = new float[numSamples];

            float lastSample = 0f;
            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float white = UnityEngine.Random.value * 2f - 1f;
                lastSample = Mathf.Lerp(lastSample, white, 0.22f);
                float accelEnv = Mathf.Pow(t / duration, 1.8f);
                samples[i] = lastSample * accelEnv * 0.85f;
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
