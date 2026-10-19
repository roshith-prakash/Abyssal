using UnityEngine;

namespace Abyssal.Gameplay
{
    /// <summary>
    /// Simulates organic underwater current sway on creepvines and kelp foliage.
    /// </summary>
    public class UnderwaterFoliageSway : MonoBehaviour
    {
        [Header("Sway Motion")]
        [Tooltip("Maximum tilt angle in degrees")]
        [SerializeField] private float swayAngle = 3.8f;

        [Tooltip("Speed of the underwater current sway")]
        [SerializeField] private float swaySpeed = 0.85f;

        [Tooltip("Secondary high-frequency flutter speed")]
        [SerializeField] private float flutterSpeed = 1.4f;

        [Tooltip("Secondary flutter angle in degrees")]
        [SerializeField] private float flutterAngle = 1.2f;

        private Quaternion baseRotation;
        private float phaseOffset;
        private Vector3 swayAxis;

        private void Start()
        {
            baseRotation = transform.localRotation;
            // Desynchronize stalks based on their world position so the forest moves naturally
            Vector3 pos = transform.position;
            phaseOffset = (pos.x * 0.35f + pos.z * 0.25f + pos.y * 0.15f);

            // Perpendicular to approximate current direction (flowing down the canyon)
            swayAxis = new Vector3(Mathf.Sin(phaseOffset), 0f, Mathf.Cos(phaseOffset)).normalized;
        }

        private void Update()
        {
            float time = Time.time * swaySpeed + phaseOffset;
            float flutterTime = Time.time * flutterSpeed + phaseOffset * 1.7f;

            // Primary ocean current roll
            float primarySway = Mathf.Sin(time) * swayAngle;
            // Secondary cross-current perturbation
            float crossSway = Mathf.Cos(time * 0.73f) * (swayAngle * 0.45f);
            // High frequency leaf flutter
            float flutter = Mathf.Sin(flutterTime) * flutterAngle;

            Quaternion swayRot = Quaternion.Euler(
                primarySway + flutter,
                crossSway * 0.5f,
                crossSway + flutter * 0.6f
            );

            transform.localRotation = baseRotation * swayRot;
        }
    }
}
