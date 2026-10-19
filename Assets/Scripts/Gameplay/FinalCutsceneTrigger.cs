using System;
using UnityEngine;
using Abyssal.Player;

namespace Abyssal.Gameplay
{
    /// <summary>
    /// Dedicated trigger volume attached to the Magic Crystal in the cavern nest.
    /// Detects when the player's diver enters the activation radius, validates that
    /// the collider belongs to the diver, prevents repeated activation, and immediately
    /// launches the final cinematic horror cutscene.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class FinalCutsceneTrigger : MonoBehaviour
    {
        [Header("Trigger Settings")]
        [Tooltip("Configurable activation radius around the crystal.")]
        [SerializeField] private float triggerRadius = 12.0f;

        [Tooltip("True once the cutscene has been activated; guarantees one-time execution.")]
        [SerializeField] private bool hasTriggered = false;

        [Header("Target References")]
        [Tooltip("Optional direct reference to the player transform for proximity fallback.")]
        [SerializeField] private Transform playerTransform;

        private SphereCollider sphereCollider;

        public bool HasTriggered => hasTriggered;
        public float TriggerRadius => triggerRadius;

        private void Awake()
        {
            sphereCollider = GetComponent<SphereCollider>();
            if (sphereCollider == null) sphereCollider = gameObject.AddComponent<SphereCollider>();

            sphereCollider.isTrigger = true;
            sphereCollider.radius = triggerRadius;
        }

        private void Start()
        {
            ResolvePlayer();
        }

        private void Update()
        {
            if (hasTriggered) return;

            // Proximity fallback check ensures 100% reliable trigger regardless of
            // swimming velocity, teleporting, or physics matrix configurations.
            if (playerTransform == null)
            {
                ResolvePlayer();
                if (playerTransform == null) return;
            }

            float distance = Vector3.Distance(transform.position, playerTransform.position);
            if (distance <= triggerRadius)
            {
                TriggerCutscene("Proximity check");
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (hasTriggered) return;

            // Validate that entering collider strictly belongs to the Player diver
            bool isPlayer = other.CompareTag("Player") ||
                            other.GetComponentInParent<DiverMovement>() != null ||
                            other.GetComponentInParent<DiverHealth>() != null;

            if (isPlayer)
            {
                TriggerCutscene($"Trigger collision with {other.gameObject.name}");
            }
        }

        private void TriggerCutscene(string reason)
        {
            if (hasTriggered) return;
            hasTriggered = true;

            Debug.Log($"[FinalCutsceneTrigger] Activated ({reason}). Starting Final Horror Cutscene...");

            if (FinalCutsceneController.Instance != null)
            {
                FinalCutsceneController.Instance.StartCutscene();
            }
            else
            {
                // Fallback search
                var controller = FindAnyObjectByType<FinalCutsceneController>();
                if (controller != null)
                {
                    controller.StartCutscene();
                }
                else
                {
                    Debug.LogError("[FinalCutsceneTrigger] FinalCutsceneController not found in scene!");
                }
            }
        }

        private void ResolvePlayer()
        {
            if (playerTransform != null) return;
            GameObject playerGO = GameObject.FindWithTag("Player");
            if (playerGO == null) playerGO = GameObject.Find("Diver");
            if (playerGO != null) playerTransform = playerGO.transform;
        }

        private void OnValidate()
        {
            if (sphereCollider == null) sphereCollider = GetComponent<SphereCollider>();
            if (sphereCollider != null)
            {
                sphereCollider.isTrigger = true;
                sphereCollider.radius = triggerRadius;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.1f, 0.9f, 1.0f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, triggerRadius);
        }
    }
}
