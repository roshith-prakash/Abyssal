#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Abyssal.Gameplay;

namespace Abyssal.Editor
{
    [InitializeOnLoad]
    public static class SetupMarineLife
    {
        private const string ReaperFbxPath = "Assets/reaper-leviathan/source/Reaper.fbx";
        private const string ReaperControllerPath = "Assets/reaper-leviathan/ReaperLeviathanController.controller";
        private const string ReaperMatPath = "Assets/reaper-leviathan/Materials/Reaper_Leviathan_mat.mat";

        private const string CreepvineFbxPath = "Assets/creepvine-dense/leaf_mid1 (merge).fbx";
        private const string CreepvineMatPath = "Assets/creepvine-dense/Materials/Creepvine_Mat.mat";

        private const string FishMatPath = "Assets/diver/Materials/AmbientFish_Mat.mat";
        private const string FishPrefabPath = "Assets/Prefabs/AmbientFish.prefab";
        private const string FishBodyMeshPath = "Assets/Prefabs/FishBodyMesh.asset";
        private const string FishTailMeshPath = "Assets/Prefabs/FishTailMesh.asset";

        [MenuItem("Abyssal/Populate Marine Life & Kelp Forests", false, 5)]
        public static void ExecuteSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            Debug.Log("[Abyssal] Populating marine life and dense creepvine forests...");

            SetupCreepvineMaterial();
            SetupCreepvineForest();
            SetupReaperFauna();
            SetupAmbientFishSystem();

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[Abyssal] SUCCESS: Marine life and kelp forests successfully added to the environment!");
        }

        // --- 1. CREEPVINE KELP FORESTS ---

        private static void SetupCreepvineMaterial()
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(CreepvineMatPath);
            if (mat == null) return;

            mat.EnableKeyword("_EMISSION");
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_NORMALMAP");

            // Warm golden-yellow bioluminescent seed cluster emission
            mat.SetColor("_EmissionColor", new Color(1.85f, 2.3f, 0.65f, 1f));
            mat.SetFloat("_Glossiness", 0.45f);

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
        }

        private static void SetupCreepvineForest()
        {
            GameObject vinePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CreepvineFbxPath);
            Material vineMat = AssetDatabase.LoadAssetAtPath<Material>(CreepvineMatPath);
            if (vinePrefab == null)
            {
                Debug.LogError("[Abyssal] Could not find Creepvine FBX at " + CreepvineFbxPath);
                return;
            }

            // Remove old sparse trench creepvines
            GameObject oldForest = GameObject.Find("Creepvine_Trench");
            if (oldForest != null) Undo.DestroyObjectImmediate(oldForest);

            GameObject forestParent = GameObject.Find("Creepvine_Forest");
            if (forestParent != null) Undo.DestroyObjectImmediate(forestParent);

            forestParent = new GameObject("Creepvine_Forest");
            Undo.RegisterCreatedObjectUndo(forestParent, "Create Creepvine Forest");

            // 42 distinct natural grove positions spanning the descent
            // Zone 1: Upper Canopy (Z: 15 to 40)
            // Zone 2: Mid Canyon Flanks (Z: 42 to 82)
            // Zone 3: Deep Trench & Basin Entry (Z: 85 to 132)
            Vector2[] stalkCoordinates = new Vector2[]
            {
                // Zone 1: Upper entrance (Player descends from Z=0)
                new Vector2(-12f, 16f), new Vector2(-15f, 22f), new Vector2(-9f, 26f),
                new Vector2(-18f, 32f), new Vector2(-13f, 38f),
                new Vector2(11f, 18f),  new Vector2(16f, 23f),  new Vector2(8f, 29f),
                new Vector2(17f, 34f),  new Vector2(12f, 39f),
                new Vector2(-6f, 35f),  new Vector2(5f, 36f),   new Vector2(-16f, 42f), new Vector2(18f, 43f),

                // Zone 2: Mid Canyon walls and slopes (Z: 46 to 82)
                new Vector2(-14f, 48f), new Vector2(-19f, 54f), new Vector2(-11f, 60f),
                new Vector2(-17f, 66f), new Vector2(-13f, 72f), new Vector2(-20f, 78f),
                new Vector2(15f, 50f),  new Vector2(19f, 56f),  new Vector2(10f, 62f),
                new Vector2(18f, 68f),  new Vector2(14f, 74f),  new Vector2(21f, 80f),
                new Vector2(-8f, 58f),  new Vector2(7f, 65f),   new Vector2(-15f, 82f), new Vector2(16f, 83f),

                // Zone 3: Deep Basin & Nest approach (Z: 86 to 132)
                new Vector2(-16f, 88f),  new Vector2(-22f, 94f),  new Vector2(-14f, 100f),
                new Vector2(-24f, 108f), new Vector2(-18f, 116f), new Vector2(-12f, 124f),
                new Vector2(17f, 90f),   new Vector2(23f, 96f),   new Vector2(15f, 102f),
                new Vector2(25f, 110f),  new Vector2(19f, 118f),  new Vector2(13f, 126f),
                new Vector2(-20f, 128f), new Vector2(22f, 130f)
            };

            for (int i = 0; i < stalkCoordinates.Length; i++)
            {
                float worldX = stalkCoordinates[i].x;
                float worldZ = stalkCoordinates[i].y;
                float floorY = SetupAbyssalLevel.CalculateOrganicElevation(worldX, worldZ);

                GameObject vGO = (GameObject)PrefabUtility.InstantiatePrefab(vinePrefab);
                vGO.name = $"Creepvine_Stalk_{i + 1:D2}";
                PrefabUtility.UnpackPrefabInstance(vGO, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

                vGO.transform.SetParent(forestParent.transform, true);
                vGO.transform.position = new Vector3(worldX, floorY - 0.2f, worldZ);

                // Slight natural tilt and varied yaw
                float tiltX = UnityEngine.Random.Range(-4.5f, 4.5f);
                float tiltZ = UnityEngine.Random.Range(-4.5f, 4.5f);
                float yaw = (i * 37.5f + UnityEngine.Random.Range(-15f, 15f)) % 360f;
                vGO.transform.rotation = Quaternion.Euler(tiltX, yaw, tiltZ);

                // Height variation: 13m to 23m tall
                float heightScale = UnityEngine.Random.Range(14f, 23f);
                float widthScale = UnityEngine.Random.Range(5.0f, 6.5f);
                vGO.transform.localScale = new Vector3(widthScale, heightScale, widthScale);

                if (vineMat != null)
                {
                    foreach (var r in vGO.GetComponentsInChildren<Renderer>())
                    {
                        r.sharedMaterial = vineMat;
                    }
                }

                // Attach dynamic current sway motion
                UnderwaterFoliageSway sway = vGO.GetComponent<UnderwaterFoliageSway>();
                if (sway == null) sway = vGO.AddComponent<UnderwaterFoliageSway>();

                // Add warm glowing bioluminescent point light to every 3rd stalk to simulate luminous seed clusters
                if (i % 3 == 0)
                {
                    GameObject seedGlow = new GameObject("SeedClusterGlow");
                    seedGlow.transform.SetParent(vGO.transform, false);
                    seedGlow.transform.localPosition = new Vector3(0f, UnityEngine.Random.Range(0.35f, 0.65f), 0f);

                    Light l = seedGlow.AddComponent<Light>();
                    l.type = LightType.Point;
                    l.range = 11f;
                    l.intensity = 1.35f;
                    l.color = new Color(1.0f, 0.88f, 0.45f);
                }
            }

            Debug.Log($"[Abyssal] Planted {stalkCoordinates.Length} creepvine stalks in 3 dense forest groves with current sway and glowing seed clusters.");
        }

        // --- 2. REAPER PREDATOR MARINE FAUNA ---

        private static void SetupReaperFauna()
        {
            // 1. Ensure ModelImporter has looping animation clip
            ModelImporter importer = AssetImporter.GetAtPath(ReaperFbxPath) as ModelImporter;
            if (importer != null)
            {
                var defaultClips = importer.defaultClipAnimations;
                if (defaultClips != null && defaultClips.Length > 0)
                {
                    foreach (var c in defaultClips)
                    {
                        c.loopTime = true;
                    }
                    importer.clipAnimations = defaultClips;
                    importer.SaveAndReimport();
                    Debug.Log("[Abyssal] Reimported Reaper FBX with looping animation.");
                }
            }

            // 2. Load animation clip and avatar
            var subAssets = AssetDatabase.LoadAllAssetsAtPath(ReaperFbxPath);
            AnimationClip swimClip = null;
            Avatar fbxAvatar = null;

            foreach (var asset in subAssets)
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    swimClip = clip;
                }
                else if (asset is Avatar avatar)
                {
                    fbxAvatar = avatar;
                }
            }

            // 3. Create or load AnimatorController
            AnimatorController animController = AssetDatabase.LoadAssetAtPath<AnimatorController>(ReaperControllerPath);
            if (animController == null && swimClip != null)
            {
                animController = AnimatorController.CreateAnimatorControllerAtPath(ReaperControllerPath);
                var rootStateMachine = animController.layers[0].stateMachine;
                var swimState = rootStateMachine.AddState("Swim");
                swimState.motion = swimClip;
                swimState.speed = 1.0f;
                rootStateMachine.defaultState = swimState;
                EditorUtility.SetDirty(animController);
                AssetDatabase.SaveAssets();
            }

            // 4. Configure Reaper Material with emission
            Material reaperMat = AssetDatabase.LoadAssetAtPath<Material>(ReaperMatPath);
            if (reaperMat != null)
            {
                reaperMat.EnableKeyword("_EMISSION");
                reaperMat.EnableKeyword("_NORMALMAP");
                reaperMat.SetColor("_EmissionColor", new Color(2.6f, 0.5f, 0.2f, 1f)); // Menacing glowing eyes & crest
                EditorUtility.SetDirty(reaperMat);
                AssetDatabase.SaveAssets();
            }

            // 5. Find or instantiate Reaper in scene
            GameObject reaperGO = GameObject.Find("Reaper");
            if (reaperGO == null)
            {
                GameObject reaperPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ReaperFbxPath);
                if (reaperPrefab != null)
                {
                    reaperGO = (GameObject)PrefabUtility.InstantiatePrefab(reaperPrefab);
                    reaperGO.name = "Reaper";
                }
            }

            if (reaperGO != null)
            {
                reaperGO.SetActive(true);

                // Initial position cruising high above the mid-canyon
                reaperGO.transform.position = new Vector3(18f, 4f, 40f);
                reaperGO.transform.rotation = Quaternion.Euler(0f, 140f, 0f);
                reaperGO.transform.localScale = Vector3.one * 1.0f;

                // Configure Animator
                Animator anim = reaperGO.GetComponent<Animator>();
                if (anim == null) anim = reaperGO.AddComponent<Animator>();
                if (animController != null) anim.runtimeAnimatorController = animController;
                if (fbxAvatar != null) anim.avatar = fbxAvatar;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                anim.applyRootMotion = false;

                // Ensure material is assigned
                if (reaperMat != null)
                {
                    foreach (var rend in reaperGO.GetComponentsInChildren<Renderer>())
                    {
                        rend.sharedMaterial = reaperMat;
                    }
                }

                // Attach dynamic swimming patrol behavior
                ReaperPatrol patrol = reaperGO.GetComponent<ReaperPatrol>();
                if (patrol == null) patrol = reaperGO.AddComponent<ReaperPatrol>();

                // Configure 3D Spatial AudioSource for eerie leviathan calls
                AudioSource audio = reaperGO.GetComponent<AudioSource>();
                if (audio == null) audio = reaperGO.AddComponent<AudioSource>();
                audio.spatialBlend = 1.0f; // Full 3D audio
                audio.minDistance = 8f;
                audio.maxDistance = 85f;
                audio.rolloffMode = AudioRolloffMode.Logarithmic;
                audio.playOnAwake = false;

                AudioClip roarClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Reaper roar.mp3");
                if (roarClip != null)
                {
                    audio.clip = roarClip;
                }

                EditorUtility.SetDirty(reaperGO);
                Debug.Log("[Abyssal] Reaper Leviathan successfully activated with swimming patrol loop and glowing eyes!");
            }
        }

        // --- 3. AMBIENT BIOLUMINESCENT FISH SCHOOLS ---

        private static void SetupAmbientFishSystem()
        {
            // 1. Create or get fish materials (Cyan, Amber, Violet, Emerald)
            Material matCyan = GetOrCreateFishMaterial("Assets/diver/Materials/AmbientFish_Cyan.mat", new Color(0.12f, 0.45f, 0.70f, 1f), new Color(0.15f, 1.8f, 2.4f, 1f) * 1.6f, "AmbientFish_Cyan");
            Material matAmber = GetOrCreateFishMaterial("Assets/diver/Materials/AmbientFish_Amber.mat", new Color(0.70f, 0.38f, 0.10f, 1f), new Color(2.4f, 1.4f, 0.25f, 1f) * 1.6f, "AmbientFish_Amber");
            Material matViolet = GetOrCreateFishMaterial("Assets/diver/Materials/AmbientFish_Violet.mat", new Color(0.42f, 0.15f, 0.65f, 1f), new Color(1.9f, 0.4f, 2.6f, 1f) * 1.6f, "AmbientFish_Violet");
            Material matEmerald = GetOrCreateFishMaterial("Assets/diver/Materials/AmbientFish_Emerald.mat", new Color(0.12f, 0.60f, 0.35f, 1f), new Color(0.3f, 2.5f, 1.2f, 1f) * 1.6f, "AmbientFish_Emerald");

            // 2. Build or load procedural fish meshes
            Mesh bodyMesh = AssetDatabase.LoadAssetAtPath<Mesh>(FishBodyMeshPath);
            if (bodyMesh == null)
            {
                bodyMesh = CreateProceduralFishBodyMesh();
                bodyMesh.name = "FishBodyMesh";
                AssetDatabase.CreateAsset(bodyMesh, FishBodyMeshPath);
            }

            Mesh tailMesh = AssetDatabase.LoadAssetAtPath<Mesh>(FishTailMeshPath);
            if (tailMesh == null)
            {
                tailMesh = CreateProceduralFishTailMesh();
                tailMesh.name = "FishTailMesh";
                AssetDatabase.CreateAsset(tailMesh, FishTailMeshPath);
            }

            // 3. Create or load Fish Prefab
            GameObject fishPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FishPrefabPath);
            if (fishPrefab == null)
            {
                GameObject rootGO = new GameObject("AmbientFish");

                MeshFilter mf = rootGO.AddComponent<MeshFilter>();
                mf.sharedMesh = bodyMesh;

                MeshRenderer mr = rootGO.AddComponent<MeshRenderer>();
                mr.sharedMaterial = matCyan;

                FishBoid boid = rootGO.AddComponent<FishBoid>();

                // Create tail child for flutter
                GameObject tailGO = new GameObject("Tail");
                tailGO.transform.SetParent(rootGO.transform, false);
                tailGO.transform.localPosition = new Vector3(0f, 0f, -0.42f);

                MeshFilter tmf = tailGO.AddComponent<MeshFilter>();
                tmf.sharedMesh = tailMesh;

                MeshRenderer tmr = tailGO.AddComponent<MeshRenderer>();
                tmr.sharedMaterial = matCyan;

                boid.SetTailTransform(tailGO.transform);

                fishPrefab = PrefabUtility.SaveAsPrefabAsset(rootGO, FishPrefabPath);
                GameObject.DestroyImmediate(rootGO);
            }

            // 4. Instantiate Schools in the Scene
            GameObject schoolsParent = GameObject.Find("Ambient_Fauna");
            if (schoolsParent != null) Undo.DestroyObjectImmediate(schoolsParent);

            schoolsParent = new GameObject("Ambient_Fauna");
            Undo.RegisterCreatedObjectUndo(schoolsParent, "Create Ambient Fauna");

            // 16 distinct school locations inhabiting every elevation, canopy, and depth zone
            var schoolConfigs = new (string name, Vector3 pos, int count, float radius, float minY, float maxY, Material mat, float minScale, float maxScale, float speed)[]
            {
                // Starting shallows & upper shelf (immediate lively impression upon spawning)
                ("School_SpawnArrival_Cyan",         new Vector3(0f, 7f, 12f),       28, 12f,  3f,   9f, matCyan,    0.65f, 1.1f,  3.5f),
                ("School_UpperShelf_West_Amber",     new Vector3(-12f, 6f, 22f),     24, 10f,  2f,   8f, matAmber,   0.50f, 0.85f, 4.2f),
                ("School_UpperShelf_East_Emerald",   new Vector3(12f, 6f, 22f),      24, 10f,  2f,   8f, matEmerald, 0.70f, 1.1f,  3.2f),

                // Kelp forest canopy (swirling in and out of the towering creepvine fronds)
                ("School_UpperKelpCanopy_Cyan",      new Vector3(0f, 3f, 32f),       32, 13f, -1f,   6f, matCyan,    0.80f, 1.3f,  3.4f),
                ("School_KelpForest_West_Emerald",   new Vector3(-14f, 1f, 42f),     26, 11f, -3f,   4f, matEmerald, 0.75f, 1.2f,  3.0f),
                ("School_KelpForest_East_Amber",     new Vector3(15f, 1f, 44f),      26, 11f, -3f,   4f, matAmber,   0.70f, 1.15f, 3.6f),

                // Canyon drop-off & mid-canyon groves
                ("School_CanyonEntrance_Cyan",       new Vector3(1f, -3f, 54f),      28, 12f, -7f,   0f, matCyan,    0.85f, 1.4f,  3.2f),
                ("School_CanyonWestGrove_Violet",    new Vector3(-15f, -6f, 65f),    25, 10f, -10f, -3f, matViolet,  0.90f, 1.5f,  2.8f),
                ("School_CanyonEastGrove_Amber",     new Vector3(15f, -7f, 75f),     25, 11f, -11f, -4f, matAmber,   0.80f, 1.3f,  3.3f),
                ("School_MidCanyon_DeepPass_Cyan",   new Vector3(0f, -10f, 85f),     30, 13f, -14f, -6f, matCyan,    0.70f, 1.2f,  3.8f),

                // Deep canyon basin & amphitheater
                ("School_DeepBasinCenter_Violet",    new Vector3(0f, -15f, 102f),    36, 15f, -19f, -11f, matViolet, 1.00f, 1.7f,  2.9f),
                ("School_DeepBasin_North_Emerald",   new Vector3(12f, -16f, 114f),   22, 10f, -20f, -13f, matEmerald, 0.70f, 1.1f, 3.4f),
                ("School_DeepBasin_South_Amber",     new Vector3(-13f, -17f, 118f),  22, 10f, -21f, -14f, matAmber,  0.65f, 1.0f,  4.0f),

                // Egg nest perimeter & crystal approach
                ("School_EggNestPerimeter_West_Violet", new Vector3(-10f, -21f, 132f), 20,  9f, -24f, -18f, matViolet, 0.90f, 1.5f, 2.6f),
                ("School_EggNestPerimeter_East_Cyan",   new Vector3(10f, -21f, 136f),  20,  9f, -24f, -18f, matCyan,   0.85f, 1.35f, 3.0f),
                ("School_CrystalCavern_Emerald",        new Vector3(0f, -21f, 150f),   25, 10f, -24f, -18f, matEmerald, 0.75f, 1.25f, 2.8f)
            };

            int totalFish = 0;
            foreach (var cfg in schoolConfigs)
            {
                GameObject schoolGO = new GameObject(cfg.name);
                schoolGO.transform.SetParent(schoolsParent.transform, false);
                schoolGO.transform.position = cfg.pos;

                FishSchool schoolComp = schoolGO.AddComponent<FishSchool>();
                schoolComp.Initialize(fishPrefab, cfg.count, cfg.radius, cfg.minY, cfg.maxY, cfg.mat, cfg.minScale, cfg.maxScale, cfg.speed);
                totalFish += cfg.count;
            }

            Debug.Log($"[Abyssal] Created {schoolConfigs.Length} dynamic ambient fish schools with {totalFish} total fish across all canyon depths!");
        }

        private static Material GetOrCreateFishMaterial(string path, Color baseColor, Color emissionColor, string name)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader standardShader = Shader.Find("Standard");
                mat = new Material(standardShader) { name = name };
                mat.color = baseColor;
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emissionColor);
                mat.SetFloat("_Glossiness", 0.75f);
                mat.SetFloat("_Metallic", 0.15f);

                AssetDatabase.CreateAsset(mat, path);
            }
            return mat;
        }

        // Procedural fish mesh generator
        private static Mesh CreateProceduralFishBodyMesh()
        {
            Mesh mesh = new Mesh();

            // Aerodynamic tapered 3D fish body
            Vector3[] vertices = new Vector3[]
            {
                // Snout (tip)
                new Vector3(0f, 0f, 0.45f),

                // Mid-body ring (widest part)
                new Vector3(0f, 0.16f, 0.12f),   // Dorsal top
                new Vector3(0.09f, 0.02f, 0.12f),// Right flank
                new Vector3(0f, -0.14f, 0.12f),  // Belly bottom
                new Vector3(-0.09f, 0.02f, 0.12f),// Left flank

                // Caudal peduncle ring (narrowing to tail)
                new Vector3(0f, 0.08f, -0.38f),
                new Vector3(0.035f, 0f, -0.38f),
                new Vector3(0f, -0.07f, -0.38f),
                new Vector3(-0.035f, 0f, -0.38f),

                // Tail attachment tip
                new Vector3(0f, 0f, -0.42f),

                // Dorsal fin apex
                new Vector3(0f, 0.32f, -0.05f),
                new Vector3(0f, 0.12f, -0.22f),

                // Left pectoral fin tip
                new Vector3(-0.25f, -0.05f, 0.05f),
                // Right pectoral fin tip
                new Vector3(0.25f, -0.05f, 0.05f)
            };

            int[] triangles = new int[]
            {
                // Snout to mid-ring
                0, 1, 2,
                0, 2, 3,
                0, 3, 4,
                0, 4, 1,

                // Mid-ring to caudal peduncle
                1, 5, 6,  1, 6, 2,
                2, 6, 7,  2, 7, 3,
                3, 7, 8,  3, 8, 4,
                4, 8, 5,  4, 5, 1,

                // Caudal peduncle to tail tip
                5, 9, 6,
                6, 9, 7,
                7, 9, 8,
                8, 9, 5,

                // Dorsal fin (double-sided)
                1, 10, 11,
                1, 11, 10,
                1, 11, 5,
                1, 5, 11,

                // Pectoral fins (double-sided)
                4, 12, 8,
                4, 8, 12,
                2, 7, 13,
                2, 13, 7
            };

            Vector2[] uvs = new Vector2[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                uvs[i] = new Vector2(vertices[i].z + 0.5f, vertices[i].y + 0.5f);
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        private static Mesh CreateProceduralFishTailMesh()
        {
            Mesh mesh = new Mesh();

            // Caudal fin (tail lobes)
            Vector3[] vertices = new Vector3[]
            {
                new Vector3(0f, 0f, 0f),           // Base at peduncle
                new Vector3(0f, 0.22f, -0.24f),    // Upper lobe tip
                new Vector3(0f, 0.03f, -0.16f),    // Center notch
                new Vector3(0f, -0.20f, -0.24f)    // Lower lobe tip
            };

            int[] triangles = new int[]
            {
                // Double-sided caudal fin
                0, 1, 2,
                0, 2, 1,
                0, 2, 3,
                0, 3, 2
            };

            Vector2[] uvs = new Vector2[]
            {
                new Vector2(0f, 0.5f),
                new Vector2(1f, 1f),
                new Vector2(0.6f, 0.5f),
                new Vector2(1f, 0f)
            };

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }
    }
}
#endif
