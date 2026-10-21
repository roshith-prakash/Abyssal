using UnityEngine;

namespace Abyssal.Gameplay
{
    /// <summary>
    /// Controls the swimming patrol behavior of the Reaper Leviathan in the underwater canyon.
    /// Creates an imposing, living presence as it glides majestically through the deep water column.
    /// </summary>
    public class ReaperPatrol : MonoBehaviour
    {
        [Header("Patrol Path")]
        [Tooltip("Waypoints for the patrol loop")]
        [SerializeField] private Vector3[] waypoints;

        [Header("Swimming Dynamics")]
        [SerializeField] private float swimSpeed = 7.5f;
        [SerializeField] private float turnSpeed = 1.2f;
        [SerializeField] private float bankingFactor = 25f;
        [SerializeField] private float waveUndulationSpeed = 1.8f;
        [SerializeField] private float waveUndulationAmount = 0.8f;

        [Header("Audio")]
        [SerializeField] private AudioSource roarSource;
        [SerializeField] private float roarIntervalMin = 18f;
        [SerializeField] private float roarIntervalMax = 35f;

        private int currentWaypointIndex = 0;
        private float nextRoarTime;
        private Transform playerTransform;

        private void Start()
        {
            // Default wide elliptical patrol route through the canyon above the creepvine canopy
            if (waypoints == null || waypoints.Length == 0)
            {
                waypoints = new Vector3[]
                {
                    new Vector3(18f, 4f, 35f),
                    new Vector3(25f, -2f, 75f),
                    new Vector3(12f, -8f, 110f),
                    new Vector3(-18f, -7f, 115f),
                    new Vector3(-24f, -1f, 70f),
                    new Vector3(-15f, 5f, 30f)
                };
            }

            GameObject diver = GameObject.Find("Diver");
            if (diver != null) playerTransform = diver.transform;

            nextRoarTime = Time.time + Random.Range(8f, 16f);

            if (roarSource == null)
            {
                roarSource = GetComponent<AudioSource>();
            }
        }

        private void Update()
        {
            if (waypoints == null || waypoints.Length == 0) return;

            Vector3 targetPoint = waypoints[currentWaypointIndex];

            // Add subtle vertical wave undulation to simulate swimming motion
            float undulation = Mathf.Sin(Time.time * waveUndulationSpeed) * waveUndulationAmount;
            Vector3 currentDest = targetPoint + new Vector3(0f, undulation, 0f);

            Vector3 toTarget = currentDest - transform.position;
            float dist = toTarget.magnitude;

            if (dist < 10f)
            {
                currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
            }

            // Smooth rotation towards target
            if (toTarget.sqrMagnitude > 0.1f)
            {
                Quaternion targetRot = Quaternion.LookRotation(toTarget.normalized, Vector3.up);

                // Calculate turn banking angle (bank into the turn)
                float angleDiff = Vector3.SignedAngle(transform.forward, toTarget.normalized, Vector3.up);
                float bankRoll = Mathf.Clamp(-angleDiff * (bankingFactor / 45f), -35f, 35f);
                targetRot = targetRot * Quaternion.Euler(0f, 0f, bankRoll);

                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
            }

            // Move forward
            transform.position += transform.forward * (swimSpeed * Time.deltaTime);

            // Periodic distant roar / audio effect
            if (Time.time >= nextRoarTime)
            {
                TriggerAmbientRoar();
                nextRoarTime = Time.time + Random.Range(roarIntervalMin, roarIntervalMax);
            }
        }

        private void TriggerAmbientRoar()
        {
            // Ambient patrol roars disabled - roars are exclusively reserved for the final cutscene
        }

        private void OnDrawGizmosSelected()
        {
            if (waypoints == null || waypoints.Length == 0) return;
            Gizmos.color = Color.red;
            for (int i = 0; i < waypoints.Length; i++)
            {
                Gizmos.DrawWireSphere(waypoints[i], 3f);
                Gizmos.DrawLine(waypoints[i], waypoints[(i + 1) % waypoints.Length]);
            }
        }
    }
}
