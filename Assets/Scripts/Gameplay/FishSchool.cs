using System.Collections.Generic;
using UnityEngine;

namespace Abyssal.Gameplay
{
    /// <summary>
    /// Manages an ambient school of deep-sea fish that weave through kelp and react to the diver.
    /// </summary>
    public class FishSchool : MonoBehaviour
    {
        [Header("School Settings")]
        [SerializeField] private GameObject fishPrefab;
        [SerializeField] private int fishCount = 14;
        [SerializeField] private float swimAreaRadius = 14f;
        [SerializeField] private float minHeight = -22f;
        [SerializeField] private float maxHeight = 6f;

        [Header("Appearance & Variety")]
        [SerializeField] private Material schoolMaterial;
        [SerializeField] private float minFishScale = 0.7f;
        [SerializeField] private float maxFishScale = 1.3f;

        [Header("Boid Dynamics")]
        [SerializeField] private float normalSpeed = 3.2f;
        [SerializeField] private float panicSpeed = 7.0f;
        [SerializeField] private float neighborRadius = 4.0f;
        [SerializeField] private float separationDistance = 1.2f;

        [Header("Diver Interaction")]
        [SerializeField] private float fleeDistance = 6.0f;
        [SerializeField] private float flashlightPanicDistance = 12.0f;

        private List<FishBoid> fishList = new List<FishBoid>();
        private Vector3 targetWaypoint;
        private float nextWaypointChangeTime;
        private Transform diverTransform;
        private Light diverFlashlight;

        private void Start()
        {
            GameObject diver = GameObject.Find("Diver");
            if (diver != null)
            {
                diverTransform = diver.transform;
                diverFlashlight = diver.GetComponentInChildren<Light>();
            }

            PickNewWaypoint();
            SpawnSchool();
        }

        private void PickNewWaypoint()
        {
            Vector2 circle = Random.insideUnitCircle * swimAreaRadius;
            float y = Mathf.Clamp(transform.position.y + Random.Range(-3f, 3f), minHeight, maxHeight);
            targetWaypoint = transform.position + new Vector3(circle.x, y - transform.position.y, circle.y);
            nextWaypointChangeTime = Time.time + Random.Range(5f, 10f);
        }

        public void Initialize(GameObject prefab, int count, float radius, float minY = -22f, float maxY = 6f, Material material = null, float minScale = 0.7f, float maxScale = 1.3f, float speed = 3.2f)
        {
            fishPrefab = prefab;
            fishCount = count;
            swimAreaRadius = radius;
            minHeight = minY;
            maxHeight = maxY;
            schoolMaterial = material;
            minFishScale = minScale;
            maxFishScale = maxScale;
            normalSpeed = speed;
        }

        private void SpawnSchool()
        {
            if (fishPrefab == null) return;

            for (int i = 0; i < fishCount; i++)
            {
                Vector3 spawnPos = transform.position + Random.insideUnitSphere * (swimAreaRadius * 0.4f);
                spawnPos.y = Mathf.Clamp(spawnPos.y, minHeight, maxHeight);

                GameObject fishGO = Instantiate(fishPrefab, spawnPos, Random.rotation, transform);
                fishGO.name = $"{gameObject.name}_Fish_{i + 1}";

                float scale = Random.Range(minFishScale, maxFishScale);
                fishGO.transform.localScale = Vector3.one * scale;

                if (schoolMaterial != null)
                {
                    foreach (var rend in fishGO.GetComponentsInChildren<Renderer>())
                    {
                        rend.sharedMaterial = schoolMaterial;
                    }
                }

                FishBoid boid = fishGO.GetComponent<FishBoid>();
                if (boid == null) boid = fishGO.AddComponent<FishBoid>();

                boid.Init(scale);
                boid.Velocity = Random.insideUnitSphere.normalized * normalSpeed;
                fishList.Add(boid);
            }
        }

        private void Update()
        {
            if (Time.time >= nextWaypointChangeTime || Vector3.Distance(transform.position, targetWaypoint) < 2f)
            {
                PickNewWaypoint();
            }

            bool playerClose = false;
            Vector3 fleeFrom = Vector3.zero;

            if (diverTransform != null)
            {
                float distToDiver = Vector3.Distance(transform.position, diverTransform.position);
                if (distToDiver < fleeDistance)
                {
                    playerClose = true;
                    fleeFrom = diverTransform.position;
                }
                else if (diverFlashlight != null && diverFlashlight.enabled && distToDiver < flashlightPanicDistance)
                {
                    // Check if flashlight is pointing towards this school
                    Vector3 toSchool = (transform.position - diverTransform.position).normalized;
                    if (Vector3.Dot(diverTransform.forward, toSchool) > 0.75f)
                    {
                        playerClose = true;
                        fleeFrom = diverTransform.position;
                    }
                }
            }

            float currentSpeed = playerClose ? panicSpeed : normalSpeed;

            // Update each boid using flocking rules
            for (int i = 0; i < fishList.Count; i++)
            {
                FishBoid boid = fishList[i];
                if (boid == null) continue;

                Vector3 boidPos = boid.transform.position;
                Vector3 separation = Vector3.zero;
                Vector3 alignment = Vector3.zero;
                Vector3 cohesion = Vector3.zero;
                int neighborCount = 0;

                for (int j = 0; j < fishList.Count; j++)
                {
                    if (i == j) continue;
                    FishBoid other = fishList[j];
                    if (other == null) continue;

                    Vector3 diff = boidPos - other.transform.position;
                    float dist = diff.magnitude;

                    if (dist < neighborRadius && dist > 0.001f)
                    {
                        if (dist < separationDistance)
                        {
                            separation += diff.normalized / dist;
                        }
                        alignment += other.Velocity;
                        cohesion += other.transform.position;
                        neighborCount++;
                    }
                }

                Vector3 accel = Vector3.zero;

                if (neighborCount > 0)
                {
                    alignment = (alignment / neighborCount).normalized;
                    cohesion = ((cohesion / neighborCount) - boidPos).normalized;
                    accel += separation * 2.2f + alignment * 1.0f + cohesion * 1.2f;
                }

                // Attraction to school waypoint
                Vector3 toWaypoint = (targetWaypoint - boidPos).normalized;
                accel += toWaypoint * 1.5f;

                // Flee from player
                if (playerClose)
                {
                    Vector3 fleeDir = (boidPos - fleeFrom).normalized;
                    accel += fleeDir * 4.0f;
                }

                // Soft boundary bounding
                float distFromCenter = Vector3.Distance(boidPos, transform.position);
                if (distFromCenter > swimAreaRadius)
                {
                    accel += (transform.position - boidPos).normalized * 2.5f;
                }

                // Vertical bounds constraint
                if (boidPos.y < minHeight) accel.y += 3.0f;
                if (boidPos.y > maxHeight) accel.y -= 3.0f;

                Vector3 newVelocity = Vector3.Lerp(boid.Velocity, (boid.Velocity + accel * Time.deltaTime).normalized * currentSpeed, Time.deltaTime * 3.5f);
                boid.Velocity = newVelocity;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, swimAreaRadius);
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(targetWaypoint, 0.6f);
        }
    }
}
