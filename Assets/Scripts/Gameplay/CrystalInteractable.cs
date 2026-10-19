using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Abyssal.Gameplay
{
    /// <summary>
    /// Attached to the Magic Crystal in the cavern nest.
    /// Handles proximity detection, HUD interaction prompt, and triggering
    /// the Leviathan encounter sequence when the player presses E.
    /// </summary>
    public class CrystalInteractable : MonoBehaviour
    {
        [Header("Interaction Settings")]
        [Tooltip("Maximum distance from player to interact with crystal.")]
        [SerializeField] private float interactionRadius = 4.0f;

        [Tooltip("Light component on or near the crystal for glow effects.")]
        [SerializeField] private Light crystalGlowLight;

        [Tooltip("Color of the crystal glow light.")]
        [SerializeField] private Color crystalColor = new Color(0.2f, 0.85f, 1.0f);

        private Transform playerTransform;
        private bool hasInteracted = false;
        private bool isPlayerInRange = false;

        public static CrystalInteractable Instance { get; private set; }

        public event Action OnCrystalInteracted;

        public bool IsPlayerInRange => isPlayerInRange && !hasInteracted;
        public bool HasInteracted => hasInteracted;
        public string PromptText => "[E] REACH FOR THE CRYSTAL";

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(this);

            if (crystalGlowLight == null)
            {
                crystalGlowLight = GetComponentInChildren<Light>();
                if (crystalGlowLight == null)
                {
                    GameObject lightGO = new GameObject("CrystalGlowLight");
                    lightGO.transform.SetParent(transform, false);
                    crystalGlowLight = lightGO.AddComponent<Light>();
                    crystalGlowLight.type = LightType.Point;
                    crystalGlowLight.range = 10f;
                    crystalGlowLight.intensity = 2.5f;
                    crystalGlowLight.color = crystalColor;
                }
            }
        }

        private void Start()
        {
            ResolvePlayer();
        }

        private void Update()
        {
            if (hasInteracted) return;

            if (playerTransform == null)
            {
                ResolvePlayer();
                if (playerTransform == null) return;
            }

            float dist = Vector3.Distance(transform.position, playerTransform.position);
            isPlayerInRange = dist <= interactionRadius;

            if (isPlayerInRange && CheckInteractInput())
            {
                Interact();
            }
        }

        private void ResolvePlayer()
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player == null) player = GameObject.Find("Diver");
            if (player != null) playerTransform = player.transform;
        }

        private bool CheckInteractInput()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) return true;
#endif
            try
            {
                if (Input.GetKeyDown(KeyCode.E)) return true;
            }
            catch (Exception) { }

            return false;
        }

        public void Interact()
        {
            if (hasInteracted) return;
            hasInteracted = true;
            isPlayerInRange = false;

            Debug.Log("[CrystalInteractable] Player interacted with Magic Crystal! Triggering Leviathan reveal sequence...");

            // Pulse crystal glow violently
            if (crystalGlowLight != null)
            {
                crystalGlowLight.intensity = 6.0f;
                crystalGlowLight.color = new Color(0.8f, 0.2f, 1.0f); // Shifts to unstable violet
            }

            OnCrystalInteracted?.Invoke();

            // Trigger the cinematic horror cutscene sequence
            if (FinalCutsceneController.Instance != null)
            {
                FinalCutsceneController.Instance.StartCutscene();
            }
            else if (LeviathanEncounter.Instance != null)
            {
                LeviathanEncounter.Instance.StartRevealSequence();
            }
            else
            {
                Debug.LogWarning("[CrystalInteractable] Cutscene controller instance not found in scene!");
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, interactionRadius);
        }
    }
}
