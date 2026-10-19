using System;
using UnityEngine;

namespace Abyssal.Scanner
{
    /// <summary>
    /// Underwater sonar/scanner navigation controller.
    /// Tracks the target MagicCrystal, calculates distance and horizontal bearing,
    /// determines dynamic signal status messages, and manages audio ping pulses.
    /// </summary>
    public class ScannerHUD : MonoBehaviour
    {
        [Header("Target Settings")]
        [Tooltip("The Magic Crystal transform to track. Auto-resolved if null.")]
        [SerializeField] private Transform crystalTarget;

        [Header("Ping Audio")]
        [Tooltip("AudioSource used to play sonar ping sounds.")]
        [SerializeField] private AudioSource pingAudioSource;

        [Tooltip("Audio clip for the sonar ping.")]
        [SerializeField] private AudioClip pingClip;

        [Range(0.1f, 1f)]
        [SerializeField] private float pingVolume = 0.5f;

        [Header("Ping Frequency")]
        [Tooltip("Longest interval between pings when far away (seconds).")]
        [SerializeField] private float maxPingInterval = 2.5f;

        [Tooltip("Shortest interval between pings when very close (seconds).")]
        [SerializeField] private float minPingInterval = 0.45f;

        [Tooltip("Distance at which ping frequency is fastest (meters).")]
        [SerializeField] private float minPingDistance = 15f;

        [Tooltip("Distance at which ping frequency is slowest (meters).")]
        [SerializeField] private float maxPingDistance = 250f;

        // Current scanner state values
        public float CurrentDistance { get; private set; }
        public float BearingAngle { get; private set; } // -180 to 180 (0 = ahead)
        public string DirectionIndicator { get; private set; } = "▲ AHEAD";
        public string StatusMessage { get; private set; } = "INITIALIZING...";
        public float PingProgress { get; private set; } // 0..1 for UI pulsing animation

        private float nextPingTime = 0f;
        private float currentPingInterval = 2.0f;

        public event Action OnPingPulse;

        private void Awake()
        {
            if (pingAudioSource == null)
            {
                pingAudioSource = GetComponent<AudioSource>();
                if (pingAudioSource == null)
                {
                    pingAudioSource = gameObject.AddComponent<AudioSource>();
                    pingAudioSource.playOnAwake = false;
                    pingAudioSource.spatialBlend = 0f; // 2D UI sound
                }
            }
        }

        private void Start()
        {
            ResolveCrystalTarget();
        }

        private void Update()
        {
            if (crystalTarget == null)
            {
                ResolveCrystalTarget();
                if (crystalTarget == null)
                {
                    StatusMessage = "SEARCHING FOR RESONANCE...";
                    DirectionIndicator = "---";
                    return;
                }
            }

            UpdateScannerReadings();
            HandleAudioPings();
        }

        private void ResolveCrystalTarget()
        {
            if (crystalTarget != null) return;

            GameObject crystalGO = GameObject.Find("MagicCrystal");
            if (crystalGO != null)
            {
                crystalTarget = crystalGO.transform;
                Debug.Log($"[ScannerHUD] Crystal target successfully resolved at {crystalTarget.position}");
            }
        }

        private void UpdateScannerReadings()
        {
            Vector3 playerPos = transform.position;
            Vector3 targetPos = crystalTarget.position;

            CurrentDistance = Vector3.Distance(playerPos, targetPos);

            // Calculate horizontal bearing (ignore vertical pitch difference for compass)
            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.001f) forward.Normalize();

            Vector3 toTarget = targetPos - playerPos;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.001f) toTarget.Normalize();

            BearingAngle = Vector3.SignedAngle(forward, toTarget, Vector3.up);

            // Format directional indicator
            float absAngle = Mathf.Abs(BearingAngle);
            if (absAngle <= 20f)
            {
                DirectionIndicator = "▲ [AHEAD]";
            }
            else if (BearingAngle > 20f && BearingAngle <= 70f)
            {
                DirectionIndicator = "►► [BEARING RIGHT]";
            }
            else if (BearingAngle > 70f && BearingAngle <= 120f)
            {
                DirectionIndicator = "► [HARD RIGHT]";
            }
            else if (BearingAngle < -20f && BearingAngle >= -70f)
            {
                DirectionIndicator = "◄◄ [BEARING LEFT]";
            }
            else if (BearingAngle < -70f && BearingAngle >= -120f)
            {
                DirectionIndicator = "◄ [HARD LEFT]";
            }
            else
            {
                DirectionIndicator = "▼ [BEHIND]";
            }

            // Determine status message based on distance & narrative phase
            if (CurrentDistance > 180f)
            {
                StatusMessage = "SIGNAL DETECTED — TRACKING FREQUENCY";
            }
            else if (CurrentDistance > 75f)
            {
                StatusMessage = "SIGNAL AMPLIFIED — APPROACHING SOURCE";
            }
            else if (CurrentDistance > 28f)
            {
                StatusMessage = "WARNING: BIOLOGICAL INTERFERENCE DETECTED";
            }
            else
            {
                StatusMessage = "SIGNAL SOURCE IDENTIFIED: LEVIATHAN NEST";
            }
        }

        private void HandleAudioPings()
        {
            float t = Mathf.InverseLerp(minPingDistance, maxPingDistance, CurrentDistance);
            currentPingInterval = Mathf.Lerp(minPingInterval, maxPingInterval, t);

            if (Time.time >= nextPingTime)
            {
                nextPingTime = Time.time + currentPingInterval;
                PlayPingSound();
                OnPingPulse?.Invoke();
            }

            // Calculate pulse progress (0 to 1) for visual animation
            float timeUntilNext = nextPingTime - Time.time;
            PingProgress = 1f - Mathf.Clamp01(timeUntilNext / currentPingInterval);
        }

        private void PlayPingSound()
        {
            if (pingAudioSource == null) return;

            if (pingClip != null)
            {
                pingAudioSource.PlayOneShot(pingClip, pingVolume);
            }
            else
            {
                // Fallback: procedural synth beep if no audio asset assigned yet
                // Ensures project is completely playable without external asset files
            }
        }

        public void SetTarget(Transform target)
        {
            crystalTarget = target;
        }
    }
}
