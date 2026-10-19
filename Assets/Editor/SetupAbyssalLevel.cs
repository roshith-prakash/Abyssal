#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Abyssal.Player;
using Abyssal.Scanner;
using Abyssal.Gameplay;
using Abyssal.UI;

namespace Abyssal.Editor
{
    [InitializeOnLoad]
    public static class SetupAbyssalLevel
    {
        private const string OceanFloorMatPath = "Assets/diver/Materials/OceanFloor_Mat.mat";
        private const string MarineSnowMatPath = "Assets/diver/Materials/MarineSnow_Mat.mat";
        private const string SeafloorAlbedoPath = "Assets/diver/textures/Seafloor_Silt_Albedo.png";
        private const string SeafloorNormalPath = "Assets/diver/textures/Seafloor_Silt_Normal.png";
        private const string ParticleDustPath = "Assets/diver/textures/particle_dust.png";

        static SetupAbyssalLevel()
        {
            BuildLevelAndScene();
        }

        [MenuItem("Abyssal/Setup Complete Game Level & Scene", false, 5)]
        public static void BuildLevelAndScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            Debug.Log("[Abyssal] Constructing Atmospheric Horror Underwater Level...");

            // 1. Clean up old or conflicting scene objects
            CleanSceneArtifacts();

            // 2. Configure Texture Importers for Normal Map & Particle
            ConfigureTextureAssets();

            // 3. Deep Oceanic Horror Darkness & Atmosphere
            ConfigureHorrorAtmosphere();

            // 4. Create authentic, non-reflective ocean silt material
            Material floorMat = CreateSeafloorMaterial();

            // 5. Build open, expansive sculpted ocean floor with 26m descent and void drop
            BuildOrganicOceanFloor(floorMat);

            // 6. Setup Diver at Top Drop Point with focused flashlight & marine snow
            SetupDiverPlayer();

            // 7. Setup Magic Crystal directly on ocean floor
            Vector3 crystalPos = new Vector3(0f, -25.2f, 150f);
            SetupMagicCrystal(crystalPos);

            // 8. Setup 12 Creature Eggs directly surrounding the crystal on the seabed
            SetupEggNestOnOceanFloor(crystalPos);

            // 9. Root Creepvine Kelp Forest and populate marine life (Fish schools & Reaper)
            SetupMarineLife.ExecuteSetup();

            // 10. Position Ghost Leviathan deep in the black void beyond the nest
            SetupGhostLeviathan();

            // 11. Apply Maui Ocean Floor asset (8K photogrammetry texture & 3D scanned reef sections)
            SetupOceanFloorAsset.ExecuteSetup();

            // 12. Setup Final Horror Cutscene (Crystal trigger, master controller, materials & audio)
            SetupFinalCutscene.ExecuteSetup();

            // 13. Ensure Encounter & HUD managers
            SetupGameManagers();

            // 13. Save Scene & Assets
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[Abyssal] Atmospheric Horror Level built successfully! Seafloor, Eggs, Pitch Darkness & Flashlight are live.");
        }

        private static void CleanSceneArtifacts()
        {
            // Remove extraction beacon completely — this is a cinematic scene with NO win/extraction screen!
            GameObject beacon = GameObject.Find("ExtractionBeacon");
            if (beacon != null) Undo.DestroyObjectImmediate(beacon);

            GameObject oldBoulders = GameObject.Find("Seafloor_Boulders");
            if (oldBoulders != null) Undo.DestroyObjectImmediate(oldBoulders);

            // Remove any legacy box cavern formations
            GameObject oldCavern = GameObject.Find("Cavern_Formations");
            if (oldCavern != null) Undo.DestroyObjectImmediate(oldCavern);

            // Remove old standalone test eggs
            GameObject oldEgg = GameObject.Find("CreatureEgg");
            if (oldEgg != null && oldEgg.transform.parent == null)
            {
                oldEgg.SetActive(false);
            }

            // Remove legacy egg nest to rebuild clean unpacked hierarchy
            GameObject oldNest = GameObject.Find("Leviathan_Egg_Nest");
            if (oldNest != null) Undo.DestroyObjectImmediate(oldNest);

            GameObject oldCluster = GameObject.Find("Creepvine_Cluster");
            if (oldCluster != null) Undo.DestroyObjectImmediate(oldCluster);
        }

        private static void ConfigureTextureAssets()
        {
            // Configure Seafloor Normal Map
            var normalImporter = AssetImporter.GetAtPath(SeafloorNormalPath) as TextureImporter;
            if (normalImporter != null && normalImporter.textureType != TextureImporterType.NormalMap)
            {
                normalImporter.textureType = TextureImporterType.NormalMap;
                normalImporter.SaveAndReimport();
            }

            // Configure Particle Dust sprite
            var particleImporter = AssetImporter.GetAtPath(ParticleDustPath) as TextureImporter;
            if (particleImporter != null && particleImporter.textureType != TextureImporterType.Default)
            {
                particleImporter.textureType = TextureImporterType.Default;
                particleImporter.alphaIsTransparency = true;
                particleImporter.SaveAndReimport();
            }
        }

        private static void ConfigureHorrorAtmosphere()
        {
            // 1. Remove Skybox — deep ocean void
            RenderSettings.skybox = null;

            // 2. Atmospheric Deep Ocean Linear Fog
            // Objects are crystal clear from 0 to 12m, with gentle falloff up to 75m
            Color deepOceanFog = new Color(0.012f, 0.042f, 0.075f, 1f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 10f;
            RenderSettings.fogEndDistance = 72f;
            RenderSettings.fogColor = deepOceanFog;

            // 3. Ambient Light — Rich deep-sea ambient luminescence so seafloor, rocks, and kelp are clearly visible
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.08f, 0.16f, 0.24f, 1f);

            // 4. Soft Ocean Downwelling Light — Surface moonlight filtering down through water column
            GameObject dirLightGO = GameObject.Find("Directional Light");
            if (dirLightGO != null)
            {
                Light dirLight = dirLightGO.GetComponent<Light>();
                if (dirLight != null)
                {
                    dirLight.enabled = true;
                    dirLight.type = LightType.Directional;
                    dirLight.color = new Color(0.18f, 0.44f, 0.62f, 1f);
                    dirLight.intensity = 0.45f;
                    dirLight.transform.rotation = Quaternion.Euler(65f, 30f, 0f);
                    dirLight.shadows = LightShadows.Soft;
                }
            }
        }

        private static Material CreateSeafloorMaterial()
        {
            return SetupOceanFloorAsset.CreateOrUpdateMaterial();
        }

        private static void BuildOrganicOceanFloor(Material floorMat)
        {
            GameObject seafloorGO = GameObject.Find("Organic_Ocean_Floor");
            if (seafloorGO == null)
            {
                seafloorGO = new GameObject("Organic_Ocean_Floor");
            }

            MeshFilter mf = seafloorGO.GetComponent<MeshFilter>();
            if (mf == null) mf = seafloorGO.AddComponent<MeshFilter>();

            MeshRenderer mr = seafloorGO.GetComponent<MeshRenderer>();
            if (mr == null) mr = seafloorGO.AddComponent<MeshRenderer>();
            mr.sharedMaterial = floorMat;

            MeshCollider mc = seafloorGO.GetComponent<MeshCollider>();
            if (mc == null) mc = seafloorGO.AddComponent<MeshCollider>();

            // Generate sculpted organic mesh: 60 quads wide x 120 quads long
            int xRes = 60;
            int zRes = 120;
            int vertexCount = (xRes + 1) * (zRes + 1);

            Vector3[] vertices = new Vector3[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];
            int[] triangles = new int[xRes * zRes * 6];

            for (int z = 0; z <= zRes; z++)
            {
                float zNorm = (float)z / zRes;
                float worldZ = Mathf.Lerp(-20f, 220f, zNorm);

                for (int x = 0; x <= xRes; x++)
                {
                    float xNorm = (float)x / xRes;
                    float worldX = Mathf.Lerp(-50f, 50f, xNorm);

                    float height = CalculateOrganicElevation(worldX, worldZ);

                    int idx = z * (xRes + 1) + x;
                    vertices[idx] = new Vector3(worldX, height, worldZ);
                    uvs[idx] = new Vector2(worldX * 0.12f, worldZ * 0.12f);
                }
            }

            int triIndex = 0;
            for (int z = 0; z < zRes; z++)
            {
                for (int x = 0; x < xRes; x++)
                {
                    int current = z * (xRes + 1) + x;
                    int next = current + xRes + 1;

                    triangles[triIndex++] = current;
                    triangles[triIndex++] = next;
                    triangles[triIndex++] = current + 1;

                    triangles[triIndex++] = current + 1;
                    triangles[triIndex++] = next;
                    triangles[triIndex++] = next + 1;
                }
            }

            Mesh mesh = new Mesh();
            mesh.name = "OceanFloor_OrganicTrench";
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            mf.sharedMesh = mesh;
            mc.sharedMesh = null;
            mc.sharedMesh = mesh;
        }

        public static float CalculateOrganicElevation(float x, float z)
        {
            // 1. Longitudinal Slope (Descent from shallow shelf into deep abyss)
            float baseDepth;
            if (z < 15f)
            {
                baseDepth = 0f; // Research drop point
            }
            else if (z < 125f)
            {
                // Smooth, continuous 26-meter plunge into the abyss
                float t = Mathf.SmoothStep(0f, 1f, (z - 15f) / 110f);
                baseDepth = Mathf.Lerp(0f, -26f, t);
            }
            else if (z <= 175f)
            {
                // Expansive, wide, flat ocean floor basin where the nest rests
                baseDepth = -26f;
            }
            else
            {
                // The Abyssal Void drop-off — plunges into black abyss
                float tVoid = Mathf.SmoothStep(0f, 1f, (z - 175f) / 45f);
                baseDepth = Mathf.Lerp(-26f, -70f, tVoid);
            }

            // 2. Canyon Walls along the descent ONLY (Z < 120)
            // In the nest region (Z >= 120), the canyon walls open up completely into an open seabed plain!
            float canyonRidge = 0f;
            if (z < 120f)
            {
                float corridorWidth = Mathf.Lerp(14f, 22f, z / 120f);
                float distFromCenter = Mathf.Abs(x);
                if (distFromCenter > corridorWidth)
                {
                    float ridgeNorm = Mathf.Clamp01((distFromCenter - corridorWidth) / 25f);
                    float wallFactor = 1f - Mathf.Clamp01((z - 95f) / 25f); // Smoothly fades out before the nest
                    canyonRidge = Mathf.SmoothStep(0f, 1f, ridgeNorm) * 20f * wallFactor;
                }
            }

            // 3. Central Nest Altar Mound at Z = 150
            float nestMound = 0f;
            float distToCenter = Vector2.Distance(new Vector2(x, z), new Vector2(0f, 150f));
            if (distToCenter < 6.5f)
            {
                // Subtle, organic seabed mound (+0.8m) on which the crystal rests
                nestMound = Mathf.Cos(distToCenter / 6.5f * (Mathf.PI * 0.5f)) * 0.8f;
            }

            // 4. Subtle, natural sediment sand dunes (low amplitude Perlin noise)
            float sandDunes = (Mathf.PerlinNoise(x * 0.05f + 10f, z * 0.05f + 10f) - 0.5f) * 1.4f;

            return baseDepth + canyonRidge + nestMound + sandDunes;
        }

        private static void SetupDiverPlayer()
        {
            GameObject diverGO = GameObject.Find("Diver");
            if (diverGO == null) return;

            // Spawn at the top of the trench shelf
            diverGO.transform.position = new Vector3(0f, 10f, 0f);
            diverGO.transform.rotation = Quaternion.identity;

            SetupPlayerPrefab.ExecuteSetup();

            ScannerHUD scanner = diverGO.GetComponent<ScannerHUD>();
            if (scanner == null) scanner = diverGO.AddComponent<ScannerHUD>();

            // Setup Camera
            Camera mainCam = diverGO.GetComponentInChildren<Camera>();
            if (mainCam == null && Camera.main != null) mainCam = Camera.main;

            if (mainCam != null)
            {
                mainCam.clearFlags = CameraClearFlags.SolidColor;
                mainCam.backgroundColor = new Color(0.012f, 0.042f, 0.075f, 1f); // Matches deep ocean fog
                mainCam.nearClipPlane = 0.03f;
                mainCam.fieldOfView = 75f;
            }

            // Setup Powerful Diver Helmet Spotlight (Wide beam + clear reach)
            Transform flTrans = diverGO.transform.Find("Main Camera/Diver Flashlight");
            if (flTrans == null) flTrans = diverGO.transform.Find("Diver Flashlight");

            if (flTrans != null)
            {
                Light fl = flTrans.GetComponent<Light>();
                if (fl != null)
                {
                    fl.type = LightType.Spot;
                    fl.color = new Color(0.92f, 0.97f, 1.0f);
                    fl.intensity = 3.8f; // Bright, clear searchlight
                    fl.range = 55f;
                    fl.spotAngle = 68f; // Wide cone so user can clearly see their path
                    fl.innerSpotAngle = 36f;
                    fl.shadows = LightShadows.Soft;
                }

                // Add soft ambient fill light around the diver's immediate space
                Transform fillTrans = flTrans.Find("DiverFillLight");
                Light fill = fillTrans != null ? fillTrans.GetComponent<Light>() : null;
                if (fill == null)
                {
                    GameObject fillGO = new GameObject("DiverFillLight");
                    fillGO.transform.SetParent(flTrans, false);
                    fill = fillGO.AddComponent<Light>();
                }
                fill.type = LightType.Point;
                fill.range = 14f;
                fill.intensity = 0.85f;
                fill.color = new Color(0.40f, 0.65f, 0.85f);
            }

            // Setup Clean Marine Snow Particles (No pink squares!)
            SetupMarineSnow(mainCam);
        }

        private static void SetupMarineSnow(Camera mainCam)
        {
            if (mainCam == null) return;

            Transform snowTrans = mainCam.transform.Find("MarineSnow");
            GameObject snowGO = snowTrans != null ? snowTrans.gameObject : null;
            if (snowGO == null)
            {
                snowGO = new GameObject("MarineSnow");
                snowGO.transform.SetParent(mainCam.transform, false);
                snowGO.transform.localPosition = new Vector3(0f, 0f, 4f);
            }

            ParticleSystem ps = snowGO.GetComponent<ParticleSystem>();
            if (ps == null) ps = snowGO.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.loop = true;
            main.startLifetime = 5.0f;
            main.startSpeed = 0.15f;
            main.startSize = 0.04f;
            main.maxParticles = 120;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(0.7f, 0.9f, 1.0f, 0.45f);

            var emission = ps.emission;
            emission.rateOverTime = 20f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(18f, 14f, 18f);

            var vel = ps.velocityOverLifetime;
            vel.x = new ParticleSystem.MinMaxCurve(0f);
            vel.y = new ParticleSystem.MinMaxCurve(0f);
            vel.z = new ParticleSystem.MinMaxCurve(0f);
            vel.enabled = false;

            // Create dedicated clean particle material
            Material snowMat = AssetDatabase.LoadAssetAtPath<Material>(MarineSnowMatPath);
            if (snowMat == null)
            {
                Shader spriteShader = Shader.Find("Sprites/Default");
                snowMat = new Material(spriteShader) { name = "MarineSnow_Mat" };
                Texture2D dustTex = AssetDatabase.LoadAssetAtPath<Texture2D>(ParticleDustPath);
                if (dustTex != null) snowMat.mainTexture = dustTex;
                AssetDatabase.CreateAsset(snowMat, MarineSnowMatPath);
            }

            var renderer = snowGO.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = snowMat;
        }

        private static void SetupMagicCrystal(Vector3 crystalPos)
        {
            GameObject crystalGO = GameObject.Find("MagicCrystal");
            if (crystalGO == null) return;

            // Elevated atop the central reef crater so it towers majestically above the rocks and eggs
            crystalGO.transform.position = new Vector3(crystalPos.x, crystalPos.y + 2.4f, crystalPos.z);
            crystalGO.transform.rotation = Quaternion.Euler(-8f, 30f, 12f);
            // Massively enlarged into a 9.5-meter alien monolith
            crystalGO.transform.localScale = Vector3.one * 6.5f;

            // Configure crystal material for brilliant HDR luminescence and specular glint
            Material crystalMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/magiccrystalix/Materials/MagicCrystal_Mat.mat");
            if (crystalMat != null)
            {
                crystalMat.EnableKeyword("_EMISSION");
                crystalMat.SetTexture("_EmissionMap", null); // Remove dark map so full crystal volume glows with 100% HDR radiance
                crystalMat.SetColor("_Color", new Color(0.2f, 0.95f, 1.0f, 1.0f));
                crystalMat.SetColor("_EmissionColor", new Color(0.35f, 0.98f, 1.0f) * 9.0f);
                crystalMat.SetFloat("_Glossiness", 0.96f);
                crystalMat.SetFloat("_Metallic", 0.88f);
                EditorUtility.SetDirty(crystalMat);
            }

            CrystalInteractable interactable = crystalGO.GetComponent<CrystalInteractable>();
            if (interactable == null) interactable = crystalGO.AddComponent<CrystalInteractable>();

            // 1. Crystal Core Light (Intense internal heart)
            Transform coreLightTrans = crystalGO.transform.Find("CrystalCoreLight");
            Light coreLight = coreLightTrans != null ? coreLightTrans.GetComponent<Light>() : null;
            if (coreLight == null)
            {
                GameObject lg = new GameObject("CrystalCoreLight");
                lg.transform.SetParent(crystalGO.transform, false);
                coreLight = lg.AddComponent<Light>();
            }
            coreLight.type = LightType.Point;
            coreLight.range = 45f;
            coreLight.intensity = 8.5f;
            coreLight.color = new Color(0.18f, 0.92f, 1.0f);

            // 2. Crystal Apex Beacon Light (Pierces deep water fog, visible from far up the canyon)
            Transform beaconTrans = crystalGO.transform.Find("CrystalBeaconLight");
            Light beaconLight = beaconTrans != null ? beaconTrans.GetComponent<Light>() : null;
            if (beaconLight == null)
            {
                GameObject lg = new GameObject("CrystalBeaconLight");
                lg.transform.SetParent(crystalGO.transform, false);
                lg.transform.localPosition = new Vector3(0f, 0.9f, 0f);
                beaconLight = lg.AddComponent<Light>();
            }
            beaconLight.type = LightType.Point;
            beaconLight.range = 85f;
            beaconLight.intensity = 6.0f;
            beaconLight.color = new Color(0.35f, 0.96f, 1.0f);

            // 3. Crystal Downward Floor Spotlight (Illuminates the basin floor, eggs, and coral reef)
            Transform spotTrans = crystalGO.transform.Find("CrystalFloorSpot");
            Light spotLight = spotTrans != null ? spotTrans.GetComponent<Light>() : null;
            if (spotLight == null)
            {
                GameObject lg = new GameObject("CrystalFloorSpot");
                lg.transform.SetParent(crystalGO.transform, false);
                lg.transform.localPosition = new Vector3(0f, 1.0f, 0f);
                lg.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                spotLight = lg.AddComponent<Light>();
            }
            spotLight.type = LightType.Spot;
            spotLight.spotAngle = 135f;
            spotLight.range = 40f;
            spotLight.intensity = 7.5f;
            spotLight.color = new Color(0.15f, 0.88f, 1.0f);
        }

        private static void SetupEggNestOnOceanFloor(Vector3 crystalPos)
        {
            GameObject eggPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/creature-egg/CreatureEgg.fbx");
            Material eggMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/creature-egg/Materials/CreatureEgg_Mat.mat");
            if (eggPrefab == null)
            {
                Debug.LogError("[Abyssal] CreatureEgg.fbx not found!");
                return;
            }

            // Enable vivid bioluminescent glow on eggs
            if (eggMat != null)
            {
                eggMat.EnableKeyword("_EMISSION");
                eggMat.SetColor("_EmissionColor", new Color(0.18f, 1.0f, 0.60f) * 2.8f);
                EditorUtility.SetDirty(eggMat);
            }

            // Clean previous nest
            GameObject existingNest = GameObject.Find("Leviathan_Egg_Nest");
            if (existingNest != null) Undo.DestroyObjectImmediate(existingNest);

            GameObject nestParent = new GameObject("Leviathan_Egg_Nest");
            nestParent.transform.position = crystalPos;

            // Dedicated emerald nest glow light
            GameObject nestLightGO = new GameObject("NestBioluminescenceLight");
            nestLightGO.transform.SetParent(nestParent.transform, false);
            nestLightGO.transform.localPosition = new Vector3(0f, 1.0f, 0f);
            Light nestLight = nestLightGO.AddComponent<Light>();
            nestLight.type = LightType.Point;
            nestLight.range = 30f;
            nestLight.intensity = 4.2f;
            nestLight.color = new Color(0.12f, 0.98f, 0.60f);

            // 12 Creature eggs positioned in an expanded natural ring around the larger crystal monolith
            float[] angles = { 0f, 30f, 65f, 95f, 130f, 160f, 195f, 225f, 260f, 285f, 320f, 345f };
            float[] radii = { 5.2f, 6.8f, 5.5f, 7.5f, 5.0f, 6.5f, 5.4f, 7.8f, 4.8f, 7.0f, 5.6f, 8.2f };
            float[] scales = { 2.4f, 3.2f, 2.2f, 3.0f, 2.5f, 3.4f, 2.1f, 3.5f, 2.6f, 2.2f, 3.1f, 2.8f };

            for (int i = 0; i < angles.Length; i++)
            {
                float rad = angles[i] * Mathf.Deg2Rad;
                float r = radii[i];
                float xOffset = Mathf.Cos(rad) * r;
                float zOffset = Mathf.Sin(rad) * r;

                // Calculate exact ocean floor elevation at this egg position
                float worldX = crystalPos.x + xOffset;
                float worldZ = crystalPos.z + zOffset;
                float floorY = CalculateOrganicElevation(worldX, worldZ);

                // Instantiate and UNPACK so transforms are 100% serialized in scene
                GameObject eggGO = (GameObject)PrefabUtility.InstantiatePrefab(eggPrefab);
                eggGO.name = $"NestEgg_{i + 1}";
                PrefabUtility.UnpackPrefabInstance(eggGO, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

                eggGO.transform.SetParent(nestParent.transform, true);
                // Rest egg partially embedded in the seabed
                eggGO.transform.position = new Vector3(worldX, floorY + 0.35f, worldZ);
                eggGO.transform.rotation = Quaternion.Euler(Random.Range(-15f, 20f), angles[i] + 45f, Random.Range(-12f, 12f));
                eggGO.transform.localScale = Vector3.one * scales[i];

                if (eggMat != null)
                {
                    foreach (var rend in eggGO.GetComponentsInChildren<Renderer>())
                    {
                        rend.sharedMaterial = eggMat;
                    }
                }
            }

            Debug.Log($"[Abyssal] Created 12 unpacked bioluminescent creature eggs on the ocean floor around the crystal!");
        }

        private static void SetupCreepvines()
        {
            GameObject vinePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/creepvine-dense/leaf_mid1 (merge).fbx");
            Material vineMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/creepvine-dense/Materials/Creepvine_Mat.mat");
            if (vinePrefab == null) return;

            GameObject forest = GameObject.Find("Creepvine_Trench");
            if (forest != null) Undo.DestroyObjectImmediate(forest);

            forest = new GameObject("Creepvine_Trench");

            // Rooted along the canyon walls guiding the diver down
            Vector3[] positions = {
                new Vector3(-14f, 0.5f, 25f),
                new Vector3(15f, 0.8f, 32f),
                new Vector3(-13f, -5.0f, 50f),
                new Vector3(15f, -8.0f, 65f),
                new Vector3(-15f, -14.0f, 85f),
                new Vector3(14f, -18.0f, 100f),
                new Vector3(-16f, -22.0f, 115f),
                new Vector3(18f, -24.0f, 130f)
            };

            for (int i = 0; i < positions.Length; i++)
            {
                GameObject vGO = (GameObject)PrefabUtility.InstantiatePrefab(vinePrefab);
                vGO.name = $"Creepvine_{i + 1}";
                PrefabUtility.UnpackPrefabInstance(vGO, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

                vGO.transform.SetParent(forest.transform, true);
                vGO.transform.position = positions[i];
                vGO.transform.rotation = Quaternion.Euler(0f, i * 55f, 0f);
                vGO.transform.localScale = new Vector3(5.5f, Random.Range(14f, 18f), 5.5f);

                if (vineMat != null)
                {
                    foreach (var r in vGO.GetComponentsInChildren<Renderer>())
                    {
                        r.sharedMaterial = vineMat;
                    }
                }
            }
        }

        private static void SetupGhostLeviathan()
        {
            GameObject ghostGO = GameObject.Find("Ghost_Leviathan");
            if (ghostGO != null)
            {
                ghostGO.SetActive(true);
                // Positioned 45m past the nest in the pitch black void (Z = 195)
                ghostGO.transform.position = new Vector3(0f, -18f, 195f);
                ghostGO.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                ghostGO.transform.localScale = Vector3.one * 1.5f;
            }
        }

        private static void SetupSeafloorBoulders()
        {
            Material rockMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/diver/Materials/CavernRock_Mat.mat");
            if (rockMat == null) rockMat = AssetDatabase.LoadAssetAtPath<Material>(OceanFloorMatPath);

            GameObject rootBoulders = GameObject.Find("Seafloor_Boulders");
            if (rootBoulders != null) Undo.DestroyObjectImmediate(rootBoulders);

            rootBoulders = new GameObject("Seafloor_Boulders");

            // Submarine rock outcroppings and sediment mounds along descent & basin
            Vector3[] rockPositions = {
                new Vector3(-8f, 0.2f, 18f),
                new Vector3(9f, 0.5f, 28f),
                new Vector3(-12f, -5.5f, 48f),
                new Vector3(10f, -8.2f, 62f),
                new Vector3(-9f, -14.5f, 82f),
                new Vector3(11f, -18.2f, 102f),
                new Vector3(-18f, -25.2f, 132f),
                new Vector3(16f, -25.0f, 138f),
                new Vector3(-14f, -25.5f, 158f),
                new Vector3(15f, -25.2f, 162f),
                new Vector3(-7f, -25.3f, 142f),
                new Vector3(8f, -25.4f, 155f),
                new Vector3(-22f, -24.8f, 148f),
                new Vector3(20f, -24.9f, 152f)
            };

            Vector3[] rockScales = {
                new Vector3(3.2f, 1.8f, 3.5f),
                new Vector3(2.8f, 2.2f, 2.9f),
                new Vector3(4.5f, 2.5f, 4.0f),
                new Vector3(3.8f, 2.1f, 3.6f),
                new Vector3(4.2f, 2.8f, 4.8f),
                new Vector3(5.0f, 3.2f, 4.6f),
                new Vector3(6.5f, 3.8f, 7.0f),
                new Vector3(5.8f, 3.5f, 6.2f),
                new Vector3(6.2f, 3.4f, 5.8f),
                new Vector3(7.0f, 4.0f, 6.5f),
                new Vector3(3.5f, 1.9f, 3.8f),
                new Vector3(3.2f, 1.7f, 3.4f),
                new Vector3(7.5f, 4.5f, 8.0f),
                new Vector3(8.0f, 4.8f, 7.5f)
            };

            for (int i = 0; i < rockPositions.Length; i++)
            {
                // Organic eroded sea boulders with varying ellipsoidal profiles
                GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                rock.name = $"AbyssalRock_{i + 1}";
                rock.transform.SetParent(rootBoulders.transform, true);

                float floorY = CalculateOrganicElevation(rockPositions[i].x, rockPositions[i].z);
                // Embed bottom half in sediment floor
                rock.transform.position = new Vector3(rockPositions[i].x, floorY + rockScales[i].y * 0.35f, rockPositions[i].z);
                rock.transform.rotation = Quaternion.Euler(Random.Range(-25f, 25f), i * 47f, Random.Range(-25f, 25f));
                rock.transform.localScale = rockScales[i];

                MeshRenderer mr = rock.GetComponent<MeshRenderer>();
                if (mr != null && rockMat != null)
                {
                    mr.sharedMaterial = rockMat;
                }
            }
        }

        private static void SetupGameManagers()
        {
            GameObject encounterGO = GameObject.Find("LeviathanEncounterController");
            if (encounterGO == null) encounterGO = new GameObject("LeviathanEncounterController");
            LeviathanEncounter encounter = encounterGO.GetComponent<LeviathanEncounter>();
            if (encounter == null) encounter = encounterGO.AddComponent<LeviathanEncounter>();

            AudioSource encounterAudio = encounterGO.GetComponent<AudioSource>();
            if (encounterAudio == null) encounterAudio = encounterGO.AddComponent<AudioSource>();
            AudioClip roarClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Leviathan_Roar.wav");
            if (roarClip != null) encounterAudio.clip = roarClip;

            // Ambient underwater hydrostatic hum
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                AudioSource ambAudio = mainCam.GetComponent<AudioSource>();
                if (ambAudio == null) ambAudio = mainCam.gameObject.AddComponent<AudioSource>();
                AudioClip ambClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Underwater_Ambience.wav");
                if (ambClip != null)
                {
                    ambAudio.clip = ambClip;
                    ambAudio.loop = true;
                    ambAudio.volume = 0.55f;
                    ambAudio.playOnAwake = true;
                }
            }

            GameObject hudGO = GameObject.Find("GameHUDManager");
            if (hudGO == null) hudGO = new GameObject("GameHUDManager");
            if (hudGO.GetComponent<GameHUDManager>() == null)
            {
                hudGO.AddComponent<GameHUDManager>();
            }
        }
    }
}
#endif
