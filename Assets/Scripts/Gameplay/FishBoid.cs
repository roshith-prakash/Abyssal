using UnityEngine;

namespace Abyssal.Gameplay
{
    /// <summary>
    /// Individual fish in an ambient deep-sea school with tail wagging and schooling dynamics.
    /// </summary>
    public class FishBoid : MonoBehaviour
    {
        public Vector3 Velocity { get; set; }

        [Header("Animation")]
        [SerializeField] private Transform tailTransform;
        [SerializeField] private float tailWagSpeed = 8f;
        [SerializeField] private float tailWagAngle = 22f;

        private float randomOffset;
        private Quaternion initialTailRot;

        private void Start()
        {
            randomOffset = Random.Range(0f, 100f);
            if (tailTransform != null)
            {
                initialTailRot = tailTransform.localRotation;
            }
        }

        private void Update()
        {
            // Align fish forward with its velocity
            if (Velocity.sqrMagnitude > 0.05f)
            {
                Quaternion targetRot = Quaternion.LookRotation(Velocity.normalized, Vector3.up);
                // Banking into turn
                float roll = -Vector3.SignedAngle(transform.forward, Velocity.normalized, Vector3.up) * 0.4f;
                targetRot = targetRot * Quaternion.Euler(0f, 0f, Mathf.Clamp(roll, -25f, 25f));
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 6f);
            }

            // Move fish
            transform.position += Velocity * Time.deltaTime;

            // Tail flutter animation proportional to swim speed
            if (tailTransform != null)
            {
                float speedMultiplier = Mathf.Clamp(Velocity.magnitude / 2.5f, 0.6f, 2.8f);
                float angle = Mathf.Sin((Time.time + randomOffset) * tailWagSpeed * speedMultiplier) * tailWagAngle;
                tailTransform.localRotation = initialTailRot * Quaternion.Euler(0f, angle, 0f);
            }
        }

        public void SetTailTransform(Transform tail)
        {
            tailTransform = tail;
            if (tailTransform != null)
            {
                initialTailRot = tailTransform.localRotation;
            }
        }
    }
}
