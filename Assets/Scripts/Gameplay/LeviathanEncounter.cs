using System;
using System.Collections;
using UnityEngine;

namespace Abyssal.Gameplay
{
    /// <summary>
    /// Coordinates the Leviathan reveal sequence and the subsequent escape chase.
    /// Choreographs the roar, sudden appearance from darkness, animator triggers,
    /// and pursuit toward the player without complex AI pathfinding.
    /// </summary>
    public class LeviathanEncounter : MonoBehaviour
    {
        public static LeviathanEncounter Instance { get; private set; }

        [Header("References")]
        [Tooltip("The Ghost Leviathan GameObject in the scene.")]
        [SerializeField] private GameObject leviathanObject;

        [Tooltip("Animator on the Leviathan.")]
        [SerializeField] private Animator leviathanAnimator;

        [Tooltip("AudioSource for roar and creature sound effects.")]
        [SerializeField] private AudioSource roarAudioSource;

        [Tooltip("Roar audio clip.")]
        [SerializeField] private AudioClip roarClip;

        [Header("Reveal Choreography")]
        [Tooltip("Where the Leviathan hides in the darkness before the reveal.")]
        [SerializeField] private Transform hiddenSpawnPoint;

        [Tooltip("Where the Leviathan charges to during the reveal (in front of diver).")]
        [SerializeField] private Transform revealPoint;

        [Tooltip("Speed during the dramatic entrance charge.")]
        [SerializeField] private float entranceSpeed = 12.0f;

        [Tooltip("Duration of eerie silence after roar before the charge starts.")]
        [SerializeField] private float silenceDuration = 1.2f;

        [Header("Escape / Chase Settings")]
        [Tooltip("Pursuit speed during the escape phase (diver normal is 3.5, sprint is 6.0).")]
        [SerializeField] private float chaseSpeed = 4.2f;

        [Tooltip("Turn rotation speed toward the player during chase.")]
        [SerializeField] private float turnSpeed = 2.5f;

        private Transform playerTransform;
        private bool isEncounterStarted = false;
        private bool isChasing = false;

        public event Action OnEncounterTriggered;
        public event Action OnChaseStarted;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(this);

            if (roarAudioSource == null)
            {
                roarAudioSource = GetComponent<AudioSource>();
                if (roarAudioSource == null)
                {
                    roarAudioSource = gameObject.AddComponent<AudioSource>();
                    roarAudioSource.playOnAwake = false;
                    roarAudioSource.spatialBlend = 0.5f;
                }
            }
        }

        private void Start()
        {
            ResolveReferences();

            // Initially position Leviathan in deep dark fog behind cavern if hiddenSpawnPoint is set
            if (leviathanObject != null && hiddenSpawnPoint != null)
            {
                leviathanObject.transform.position = hiddenSpawnPoint.position;
                leviathanObject.transform.rotation = hiddenSpawnPoint.rotation;
            }
        }

        private void Update()
        {
            if (isChasing && leviathanObject != null && playerTransform != null)
            {
                UpdateChaseMovement();
            }
        }

        private void ResolveReferences()
        {
            if (leviathanObject == null)
            {
                leviathanObject = GameObject.Find("Ghost_Leviathan");
                if (leviathanObject == null) leviathanObject = GameObject.Find("GhostLeviathan");
            }

            if (leviathanObject != null && leviathanAnimator == null)
            {
                leviathanAnimator = leviathanObject.GetComponentInChildren<Animator>();
            }

            // Ensure Leviathan has a Collider and HazardDamage / Hazard tag
            if (leviathanObject != null)
            {
                Collider col = leviathanObject.GetComponent<Collider>();
                if (col == null)
                {
                    BoxCollider box = leviathanObject.AddComponent<BoxCollider>();
                    box.size = new Vector3(8f, 6f, 25f);
                    box.center = new Vector3(0f, 0f, 5f);
                }

                HazardDamage hazard = leviathanObject.GetComponent<HazardDamage>();
                if (hazard == null)
                {
                    hazard = leviathanObject.AddComponent<HazardDamage>();
                }

                leviathanObject.tag = "Leviathan";
            }

            GameObject player = GameObject.FindWithTag("Player");
            if (player == null) player = GameObject.Find("Diver");
            if (player != null) playerTransform = player.transform;
        }

        public void StartRevealSequence()
        {
            if (isEncounterStarted) return;
            isEncounterStarted = true;

            ResolveReferences();
            StartCoroutine(RevealSequenceRoutine());
        }

        private IEnumerator RevealSequenceRoutine()
        {
            Debug.Log("[LeviathanEncounter] Sequence started: Playing deafening roar...");
            OnEncounterTriggered?.Invoke();

            // 1. Play Roar
            if (roarAudioSource != null && roarClip != null)
            {
                roarAudioSource.PlayOneShot(roarClip, 1.0f);
            }

            // 2. Eerie silence delay
            yield return new WaitForSeconds(silenceDuration);

            // 3. Move Leviathan from darkness into the diver's field of view
            Vector3 targetEntrancePos = revealPoint != null ? revealPoint.position :
                                        (playerTransform != null ? playerTransform.position + playerTransform.forward * 12f + Vector3.up * 3f : transform.position);

            if (leviathanObject != null)
            {
                leviathanObject.SetActive(true);

                Vector3 startPos = leviathanObject.transform.position;
                float distance = Vector3.Distance(startPos, targetEntrancePos);
                float duration = Mathf.Max(2.0f, distance / Mathf.Max(1.0f, entranceSpeed));

                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                    leviathanObject.transform.position = Vector3.Lerp(startPos, targetEntrancePos, t);

                    // Face towards the entrance target / player
                    if (playerTransform != null)
                    {
                        Vector3 dirToPlayer = (playerTransform.position - leviathanObject.transform.position).normalized;
                        if (dirToPlayer.sqrMagnitude > 0.001f)
                        {
                            Quaternion lookRot = Quaternion.LookRotation(dirToPlayer);
                            leviathanObject.transform.rotation = Quaternion.Slerp(leviathanObject.transform.rotation, lookRot, Time.deltaTime * 3f);
                        }
                    }

                    yield return null;
                }
            }

            Debug.Log("[LeviathanEncounter] Leviathan revealed! Starting terrifying confrontation!");
            isChasing = true;
            OnChaseStarted?.Invoke();
        }

        private void UpdateChaseMovement()
        {
            // Simple, reliable pursuit toward player
            Vector3 leviathanPos = leviathanObject.transform.position;
            Vector3 targetPos = playerTransform.position;

            Vector3 moveDir = (targetPos - leviathanPos).normalized;
            leviathanObject.transform.position += moveDir * (chaseSpeed * Time.deltaTime);

            if (moveDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(moveDir);
                leviathanObject.transform.rotation = Quaternion.Slerp(leviathanObject.transform.rotation, targetRot, turnSpeed * Time.deltaTime);
            }
        }
    }
}
