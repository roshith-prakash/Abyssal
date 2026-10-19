#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Abyssal.Editor
{
    [InitializeOnLoad]
    public static class SetupOceanFloorAsset
    {
        private const string ObjPath = "Assets/ocean-floor-2-meters-depth-maui/model_organic.obj";
        private const string AlbedoPath = "Assets/ocean-floor-2-meters-depth-maui/tex_u1_v1.jpg";
        private const string NormalPath = "Assets/ocean-floor-2-meters-depth-maui/MauiOceanFloor_Normal.png";
        private const string MaterialPath = "Assets/ocean-floor-2-meters-depth-maui/MauiOceanFloor_Mat.mat";

        [MenuItem("Abyssal/Apply Maui Ocean Floor Asset", false, 6)]
        public static void ExecuteSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            Debug.Log("[Abyssal] Applying authentic Maui Ocean Floor asset to level...");

            ConfigureTextures();
            Material oceanMat = CreateOrUpdateMaterial();
            ApplyToOrganicFloor(oceanMat);
            PlaceScannedOceanFloorTiles(oceanMat);

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[Abyssal] SUCCESS: Maui Ocean Floor asset applied to both organic trench and 3D scanned features!");
        }

        private static void ConfigureTextures()
        {
            // Configure Normal Map texture type
            var normalImporter = AssetImporter.GetAtPath(NormalPath) as TextureImporter;
            if (normalImporter != null && normalImporter.textureType != TextureImporterType.NormalMap)
            {
                normalImporter.textureType = TextureImporterType.NormalMap;
                normalImporter.SaveAndReimport();
            }

            // Configure Albedo texture type & max resolution
            var albedoImporter = AssetImporter.GetAtPath(AlbedoPath) as TextureImporter;
            if (albedoImporter != null)
            {
                bool dirty = false;
                if (albedoImporter.maxTextureSize < 4096)
                {
                    albedoImporter.maxTextureSize = 4096;
                    dirty = true;
                }
                if (dirty) albedoImporter.SaveAndReimport();
            }
        }

        public static Material CreateOrUpdateMaterial()
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat == null)
            {
                Shader shader = Shader.Find("Standard");
                mat = new Material(shader) { name = "MauiOceanFloor_Mat" };
                AssetDatabase.CreateAsset(mat, MaterialPath);
            }

            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(AlbedoPath);
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);

            if (albedo != null) mat.SetTexture("_MainTex", albedo);
            if (normal != null)
            {
                mat.SetTexture("_BumpMap", normal);
                mat.EnableKeyword("_NORMALMAP");
            }

            // Authentic submarine sediment: matte, non-reflective silt and porous basalt with pronounced relief
            mat.color = new Color(0.88f, 0.92f, 0.96f, 1f);
            mat.SetFloat("_Glossiness", 0.10f);
            mat.SetFloat("_Metallic", 0.0f);
            mat.SetFloat("_BumpScale", 1.4f);

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void ApplyToOrganicFloor(Material oceanMat)
        {
            GameObject floorGO = GameObject.Find("Organic_Ocean_Floor");
            if (floorGO != null)
            {
                MeshRenderer mr = floorGO.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    mr.sharedMaterial = oceanMat;
                    EditorUtility.SetDirty(floorGO);
                }
            }
        }

        private static void PlaceScannedOceanFloorTiles(Material oceanMat)
        {
            GameObject objPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ObjPath);
            if (objPrefab == null)
            {
                Debug.LogError("[Abyssal] Could not load model_organic.obj at " + ObjPath);
                return;
            }

            GameObject parent = GameObject.Find("Scanned_Ocean_Floor_Sections");
            if (parent != null) Undo.DestroyObjectImmediate(parent);

            parent = new GameObject("Scanned_Ocean_Floor_Sections");
            Undo.RegisterCreatedObjectUndo(parent, "Create Scanned Ocean Floor Sections");

            // Remove legacy placeholder primitive sphere boulders so photogrammetric scans take full visual prominence
            GameObject oldBoulders = GameObject.Find("Seafloor_Boulders");
            if (oldBoulders != null) Undo.DestroyObjectImmediate(oldBoulders);

            // Continuous chain of 5 overlapping photogrammetry sections spanning from player spawn down to nest basin.
            // Using model_organic.obj with all vertical drop skirts trimmed and boundary edges feathered down.
            // Rotations are strictly Y-axis (yaw) so the mesh base remains parallel to the seabed,
            // and the feathered perimeter is buried ~0.55m into the sediment sand, completely eliminating any visible edge cuts!
            var tileConfigs = new (string name, Vector3 pos, float yaw, Vector3 scale)[]
            {
                // 1. Upper Descent Shelf (immediate ground under player drop-in)
                (
                    "Seafloor_UpperShelf_ScannedReef",
                    new Vector3(0f, 0f, 18f),
                    180f,
                    new Vector3(1.05f, 0.55f, 1.05f)
                ),
                // 2. Upper Trench Descent
                (
                    "Seafloor_Trench_Upper_ScannedReef",
                    new Vector3(0f, 0f, 48f),
                    45f,
                    new Vector3(1.10f, 0.55f, 1.10f)
                ),
                // 3. Mid Trench Descent (embedded in canyon slope)
                (
                    "Seafloor_Trench_Mid_ScannedReef",
                    new Vector3(0f, 0f, 80f),
                    -35f,
                    new Vector3(1.10f, 0.55f, 1.10f)
                ),
                // 4. Basin Entrance Reef (transition threshold into the Leviathan crater)
                (
                    "Seafloor_Approach_ScannedReef",
                    new Vector3(0f, 0f, 114f),
                    90f,
                    new Vector3(1.10f, 0.55f, 1.10f)
                ),
                // 5. Deep Leviathan Egg Nest Basin (natural amphitheater cradling the enlarged Magic Crystal)
                (
                    "Seafloor_NestBasin_ScannedReef",
                    new Vector3(0f, 0f, 150f),
                    225f,
                    new Vector3(1.20f, 0.62f, 1.20f)
                )
            };

            foreach (var cfg in tileConfigs)
            {
                GameObject tileGO = (GameObject)PrefabUtility.InstantiatePrefab(objPrefab);
                tileGO.name = cfg.name;
                PrefabUtility.UnpackPrefabInstance(tileGO, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

                tileGO.transform.SetParent(parent.transform, true);

                // Dynamically anchor to exact procedural floor elevation so the feathered base is buried 55cm under sediment
                float floorY = SetupAbyssalLevel.CalculateOrganicElevation(cfg.pos.x, cfg.pos.z);
                tileGO.transform.position = new Vector3(cfg.pos.x, floorY - 0.55f, cfg.pos.z);
                tileGO.transform.rotation = Quaternion.Euler(0f, cfg.yaw, 0f);
                tileGO.transform.localScale = cfg.scale;

                // Assign ocean floor material with the 8K photogrammetry texture & normal map
                foreach (var mr in tileGO.GetComponentsInChildren<MeshRenderer>())
                {
                    mr.sharedMaterial = oceanMat;
                }

                // Add MeshCollider so player diver physically interacts with the scanned seabed
                foreach (var mf in tileGO.GetComponentsInChildren<MeshFilter>())
                {
                    if (mf.sharedMesh != null)
                    {
                        MeshCollider mc = mf.gameObject.GetComponent<MeshCollider>();
                        if (mc == null) mc = mf.gameObject.AddComponent<MeshCollider>();
                        mc.sharedMesh = mf.sharedMesh;
                    }
                }
            }

            Debug.Log("[Abyssal] Instantiated 3 photogrammetric ocean floor sections at Nest Basin, Upper Shelf, and Mid Canyon!");
        }
    }
}
#endif
