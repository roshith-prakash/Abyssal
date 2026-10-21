#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Abyssal.Player;

namespace Abyssal.Editor
{
    [InitializeOnLoad]
    public static class SetupPlayerPrefab
    {
        private const string PhysicsDir = "Assets/Physics";
        private const string PhysicMatPath = "Assets/Physics/DiverUnderwaterPhysicMaterial.physicMaterial";
        private const string CustomMatPath = "Assets/diver/Materials/DiverSuit_CustomMat.mat";
        private const string PrefabDir = "Assets/Prefabs";
        private const string PrefabPath = "Assets/Prefabs/PlayerDiver.prefab";

        static SetupPlayerPrefab()
        {
            // Do not run automatically on compile; use menu item instead
        }

        [MenuItem("Abyssal/Setup Player Prefab & Physics", false, 1)]
        public static void ExecuteSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            // 1. Ensure folders exist
            EnsureFolder("Assets", "Physics");
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets/diver", "Materials");

            // 2. Create / Configure Custom Physics Material (PhysicMaterial)
            PhysicsMaterial physMat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(PhysicMatPath);
            if (physMat == null)
            {
                physMat = new PhysicsMaterial("DiverUnderwaterPhysicMaterial")
                {
                    dynamicFriction = 0.1f,
                    staticFriction = 0.1f,
                    bounciness = 0.0f,
                    frictionCombine = PhysicsMaterialCombine.Minimum,
                    bounceCombine = PhysicsMaterialCombine.Minimum
                };
                AssetDatabase.CreateAsset(physMat, PhysicMatPath);
                Debug.Log($"[Abyssal] Created underwater Physics Material at {PhysicMatPath}");
            }
            else
            {
                physMat.dynamicFriction = 0.1f;
                physMat.staticFriction = 0.1f;
                physMat.bounciness = 0.0f;
                physMat.frictionCombine = PhysicsMaterialCombine.Minimum;
                physMat.bounceCombine = PhysicsMaterialCombine.Minimum;
                EditorUtility.SetDirty(physMat);
            }

            // 3. Create / Configure Custom Diver Material
            Material customMat = AssetDatabase.LoadAssetAtPath<Material>(CustomMatPath);
            if (customMat == null)
            {
                Shader standardShader = Shader.Find("Standard");
                customMat = new Material(standardShader)
                {
                    name = "DiverSuit_CustomMat",
                    color = new Color(0.12f, 0.28f, 0.42f, 1f) // Sleek deep ocean navy blue
                };

                // Link existing wetsuit normal map if available
                Texture2D normalMap = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/diver/textures/wetsuit_nrm2.png");
                if (normalMap != null)
                {
                    customMat.EnableKeyword("_NORMALMAP");
                    customMat.SetTexture("_BumpMap", normalMap);
                }

                // Link wetsuit diffuse if available
                Texture2D diffMap = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/diver/textures/wetsuit_diff.png");
                if (diffMap != null)
                {
                    customMat.SetTexture("_MainTex", diffMap);
                }

                customMat.SetFloat("_Glossiness", 0.65f); // Wet neoprene sheen
                customMat.SetFloat("_Metallic", 0.15f);

                AssetDatabase.CreateAsset(customMat, CustomMatPath);
                Debug.Log($"[Abyssal] Created custom Diver Material at {CustomMatPath}");
            }

            // 4. Find Diver GameObject in scene
            GameObject diverGO = GameObject.Find("Diver");
            if (diverGO == null)
            {
                Debug.LogError("[Abyssal] Diver GameObject not found in active scene!");
                return;
            }

            // Unpack existing prefab instance if needed to make fundamental component changes
            if (PrefabUtility.IsPartOfPrefabInstance(diverGO))
            {
                PrefabUtility.UnpackPrefabInstance(diverGO, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }

            // 5. Apply Custom Material to Player's rendered model and configure ShadowsOnly for First-Person
            Renderer[] renderers = diverGO.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r.name.ToLower().Contains("suit") || r.name.ToLower().Contains("body") || r.name.ToLower().Contains("diver"))
                {
                    r.sharedMaterial = customMat;
                    Debug.Log($"[Abyssal] Assigned custom material {customMat.name} to {r.gameObject.name}");
                }
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            }

            // 6. Setup Rigidbody
            Rigidbody rb = diverGO.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = diverGO.AddComponent<Rigidbody>();
            }
            rb.useGravity = false;
            rb.isKinematic = false;
            rb.mass = 80f;
            rb.linearDamping = 2.0f; // in Unity 6, drag is linearDamping
            rb.angularDamping = 5.0f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.constraints = RigidbodyConstraints.FreezeRotation;

            // 7. Setup CapsuleCollider with custom Physics Material
            CapsuleCollider col = diverGO.GetComponent<CapsuleCollider>();
            if (col == null)
            {
                col = diverGO.AddComponent<CapsuleCollider>();
            }
            col.center = new Vector3(0f, 0.9f, 0f);
            col.radius = 0.45f;
            col.height = 1.8f;
            col.direction = 1; // Y-axis
            col.sharedMaterial = physMat;

            // 8. Setup DiverMovement
            DiverMovement movement = diverGO.GetComponent<DiverMovement>();
            if (movement == null)
            {
                movement = diverGO.AddComponent<DiverMovement>();
            }

            // 9. Setup DiverHealth
            DiverHealth health = diverGO.GetComponent<DiverHealth>();
            if (health == null)
            {
                health = diverGO.AddComponent<DiverHealth>();
            }

            // 10. Tag player
            diverGO.tag = "Player";

            // 11. Create / Save Reusable Prefab
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAssetAndConnect(diverGO, PrefabPath, InteractionMode.AutomatedAction);
            Debug.Log($"[Abyssal] Successfully created and saved Player Prefab at {PrefabPath}!");

            // 12. Mark Scene Dirty and Save
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[Abyssal] Setup of Player Prefab, Rigidbody, Collider, Physics Material, Custom Material, and DiverHealth completed successfully!");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
#endif
