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
        [SerializeField] private float triggerRadius = 30.0f;

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
            if (triggerRadius < 30.0f) triggerRadius = 30.0f;

            sphereCollider = GetComponent<SphereCollider>();
            if (sphereCollider == null) sphereCollider = gameObject.AddComponent<SphereCollider>();

            sphereCollider.isTrigger = true;
            UpdateColliderRadius();
        }

        private void UpdateColliderRadius()
        {
            if (sphereCollider == null) return;
            // Lossy scale compensation: A SphereCollider's world radius is radius * lossyScale.
            // On a GameObject with scale (6.5, 6.5, 6.5), setting radius to 30 creates a 195m radius!
            // We divide triggerRadius by the transform's max scale so world radius is exactly triggerRadius (30m).
            float maxScale = Mathf.Max(transform.lossyScale.x, Mathf.Max(transform.lossyScale.y, transform.lossyScale.z));
            sphereCollider.radius = maxScale > 0.0001f ? (triggerRadius / maxScale) : triggerRadius;
        }

        private void Start()
        {
            UpdateColliderRadius();
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
                TriggerCutscene($"Proximity check (distance: {distance:F1}m)");
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (hasTriggered) return;

            // Validate that entering collider strictly belongs to the Player diver
            bool isPlayer = other.CompareTag("Player") ||
                            other.GetComponentInParent<DiverMovement>() != null ||
                            other.GetComponentInParent<DiverHealth>() != null;

            if (!isPlayer) return;

            // Strict distance check: Ensure diver is truly within triggerRadius (30m) in world space
            float distance = Vector3.Distance(transform.position, other.transform.position);
            if (distance > triggerRadius * 1.15f)
            {
                // Ignore premature triggers if collider bounds overlap outside the 30m world radius
                return;
            }

            TriggerCutscene($"Trigger collision with {other.gameObject.name} (distance: {distance:F1}m)");
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
                if (controller == null)
                {
                    GameObject manager = new GameObject("FinalCutsceneManager");
                    controller = manager.AddComponent<FinalCutsceneController>();
                    Debug.Log("[FinalCutsceneTrigger] Created FinalCutsceneManager on demand.");
                }
                controller.StartCutscene();
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
            if (triggerRadius < 30.0f) triggerRadius = 30.0f;
            if (sphereCollider == null) sphereCollider = GetComponent<SphereCollider>();
            UpdateColliderRadius();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.1f, 0.9f, 1.0f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, triggerRadius);
        }
    }
}
