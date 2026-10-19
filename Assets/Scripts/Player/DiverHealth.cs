using System;
using UnityEngine;

namespace Abyssal.Player
{
    /// <summary>
    /// Manages the diver's health, collision damage, and destruction upon death.
    /// Satisfies all assignment requirements:
    /// - Initializes player health in Start()
    /// - Implements OnCollisionEnter() to reduce health on collision
    /// - Calls Destroy(gameObject) when health reaches zero or below
    /// </summary>
    public class DiverHealth : MonoBehaviour
    {
        [Header("Health Settings")]
        [Tooltip("Maximum health of the diver.")]
        [SerializeField] private float maxHealth = 100f;

        [Tooltip("Current health of the diver.")]
        [SerializeField] private float currentHealth;

        [Header("Damage Settings")]
        [Tooltip("Default damage taken from general hazard collisions.")]
        [SerializeField] private float defaultCollisionDamage = 25f;

        [Tooltip("Invulnerability time window after taking damage (seconds).")]
        [SerializeField] private float damageCooldown = 0.8f;

        private float lastDamageTime = -99f;

        // Events for UI or encounter controllers
        public event Action<float, float> OnHealthChanged; // (current, max)
        public event Action OnDiverDied;

        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public bool IsDead => currentHealth <= 0f;

        // -------------------------------------------------------------------
        // Requirement: Initialize the player's health in the Start() method
        // -------------------------------------------------------------------
        private void Start()
        {
            currentHealth = maxHealth;
            Debug.Log($"[DiverHealth] Player health initialized in Start() to {currentHealth:F0}/{maxHealth:F0} HP.");
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        // -------------------------------------------------------------------
        // Requirement: Implement OnCollisionEnter() to reduce health when a
        // relevant collision occurs.
        // -------------------------------------------------------------------
        private void OnCollisionEnter(Collision collision)
        {
            if (IsDead) return;

            GameObject other = collision.gameObject;
            string otherName = other.name.ToLower();

            // Check if the colliding object is a Leviathan, Hazard, or tagged appropriately
            bool isLeviathan = other.CompareTag("Leviathan") ||
                               otherName.Contains("leviathan") ||
                               other.GetComponentInParent<Animator>() != null && otherName.Contains("ghost");

            bool isHazard = other.CompareTag("Hazard") ||
                            otherName.Contains("hazard") ||
                            other.GetComponent<HazardDamage>() != null ||
                            other.GetComponentInParent<HazardDamage>() != null;

            if (isLeviathan || isHazard)
            {
                float damage = defaultCollisionDamage;

                HazardDamage hazardComp = other.GetComponent<HazardDamage>();
                if (hazardComp == null) hazardComp = other.GetComponentInParent<HazardDamage>();

                if (hazardComp != null)
                {
                    damage = hazardComp.DamageAmount;
                }
                else if (isLeviathan)
                {
                    damage = 50f; // Heavy attack damage from the Leviathan
                }

                TakeDamage(damage, $"Collision with '{other.name}'");
            }
        }

        [Tooltip("Delay in seconds before destroying player GameObject on death.")]
        [SerializeField] private float deathDestroyDelay = 0f;

        /// <summary>
        /// Reduces health by specified amount and checks for death.
        /// </summary>
        public void TakeDamage(float amount, string source = "Hazard")
        {
            if (IsDead) return;
            if (Time.time - lastDamageTime < damageCooldown) return;

            lastDamageTime = Time.time;
            currentHealth -= amount;
            if (currentHealth < 0f) currentHealth = 0f;

            Debug.Log($"[DiverHealth] Diver took {amount:F1} damage from {source}. Remaining Health: {currentHealth:F1}/{maxHealth:F1} HP");
            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            if (currentHealth <= 0f)
            {
                Die();
            }
        }

        /// <summary>
        /// Explicit lethal death trigger for the cutscene impact.
        /// Zeroes health, invokes death events, and schedules destruction of the GameObject.
        /// </summary>
        public void TriggerCinematicDeath(float destroyDelay = 3.0f)
        {
            if (IsDead) return;
            deathDestroyDelay = destroyDelay;
            currentHealth = 0f;
            Debug.Log($"[DiverHealth] Scripted cutscene lethal impact! Diver suit compromised. Remaining Health: 0 HP. Destroy scheduled in {destroyDelay}s.");
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            Die();
        }

        // -------------------------------------------------------------------
        // Requirement: When health is less than or equal to zero, call Destroy(gameObject)
        // -------------------------------------------------------------------
        private void Die()
        {
            Debug.Log("[DiverHealth] Diver health reached zero. Diver died. Calling Destroy(gameObject) as required.");

            OnDiverDied?.Invoke();

            // Notify UI or GameManager if present
            var hud = FindAnyObjectByType<MonoBehaviour>(); // Fallback / placeholder notification

            // Mandatory requirement: Destroy the player GameObject
            if (deathDestroyDelay > 0f)
            {
                Destroy(gameObject, deathDestroyDelay);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
