#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Abyssal.Editor
{
    [InitializeOnLoad]
    public static class SetupNewModels
    {
        private const string EggFbxPath = "Assets/creature-egg/CreatureEgg.fbx";
        private const string EggMatPath = "Assets/creature-egg/Materials/CreatureEgg_Mat.mat";
        private const string EggDiffusePath = "Assets/creature-egg/textures/Diffuse.png";
        private const string EggNormalPath = "Assets/creature-egg/textures/Normal.png";
        private const string EggSpecularPath = "Assets/creature-egg/textures/Specular.png";
        private const string EggGlossPath = "Assets/creature-egg/textures/Glossiness.png";

        private const string VineFbxPath = "Assets/creepvine-dense/leaf_mid1 (merge).fbx";
        private const string VineMatPath = "Assets/creepvine-dense/Materials/Creepvine_Mat.mat";
        private const string VineAlbedoPath = "Assets/creepvine-dense/textures/Coral_reef_kelp.png";
        private const string VineNormalPath = "Assets/creepvine-dense/textures/Coral_reef_Kelp_Leaves_normal.png";
        private const string VineIllumPath = "Assets/creepvine-dense/textures/Coral_reef_Kelp_illum.png";

        private const string CrystalObjPath = "Assets/magiccrystalix/magiccrystalix.obj";
        private const string CrystalMatPath = "Assets/magiccrystalix/Materials/MagicCrystal_Mat.mat";
        private const string CrystalDiffusePath = "Assets/magiccrystalix/textures/texture_diffuse_1.png";
        private const string CrystalNormalPath = "Assets/magiccrystalix/textures/texture_normal_0.png";
        private const string CrystalPbrPath = "Assets/magiccrystalix/textures/texture_metallic_smoothness.png";

        static SetupNewModels()
        {
            // Delay call to ensure editor is fully loaded
            EditorApplication.delayCall += CheckAndAutoSetup;
        }

        private static void CheckAndAutoSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            if (GameObject.Find("MagicCrystal") == null)
            {
                Debug.Log("[Abyssal] Auto-running Setup Newly Added Models for Magic Crystal...");
                SetupAndAddToScene();
            }
        }

        [MenuItem("Abyssal/Setup Newly Added Models", false, 20)]
        public static void SetupAndAddToScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            // 1. Ensure folders exist
            EnsureFolder("Assets/creature-egg", "Materials");
            EnsureFolder("Assets/creepvine-dense", "Materials");
            EnsureFolder("Assets/magiccrystalix", "Materials");

            // 2. Configure Normal Maps
            ConfigureNormalMap(EggNormalPath);
            ConfigureNormalMap(VineNormalPath);
            ConfigureNormalMap(CrystalNormalPath);

            // 3. Create / Configure Materials
            Material eggMat = SetupEggMaterial();
            Material vineMat = SetupVineMaterial();
            Material crystalMat = SetupCrystalMaterial();

            // 4. Instantiate or Update in Scene
            SetupSceneInstances(eggMat, vineMat, crystalMat);

            // 5. Mark Scene Dirty and Save
            var activeScene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            Debug.Log("[Abyssal] Compatible models (CreatureEgg, Creepvine, and MagicCrystal) successfully configured and saved in scene!");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string fullChild = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(fullChild))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static void ConfigureNormalMap(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
                Debug.Log($"[Abyssal] Configured {path} as NormalMap.");
            }
        }

        private static Material SetupEggMaterial()
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(EggMatPath);
            if (mat == null)
            {
                Shader shader = Shader.Find("Standard (Specular setup)") ?? Shader.Find("Standard");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, EggMatPath);
            }

            var diff = AssetDatabase.LoadAssetAtPath<Texture2D>(EggDiffusePath);
            var norm = AssetDatabase.LoadAssetAtPath<Texture2D>(EggNormalPath);
            var spec = AssetDatabase.LoadAssetAtPath<Texture2D>(EggSpecularPath);

            if (diff != null) mat.SetTexture("_MainTex", diff);
            if (norm != null)
            {
                mat.EnableKeyword("_NORMALMAP");
                mat.SetTexture("_BumpMap", norm);
                mat.SetFloat("_BumpScale", 1.0f);
            }
            if (spec != null)
            {
                mat.EnableKeyword("_SPECGLOSSMAP");
                mat.SetTexture("_SpecGlossMap", spec);
            }
            mat.SetFloat("_Glossiness", 0.75f);

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        private static Material SetupVineMaterial()
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(VineMatPath);
            if (mat == null)
            {
                Shader shader = Shader.Find("Standard");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, VineMatPath);
            }

            // Setup Cutout mode for kelp leaves
            mat.SetFloat("_Mode", 1); // Cutout
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            mat.SetInt("_ZWrite", 1);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            mat.SetFloat("_Cutoff", 0.35f);

            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(VineAlbedoPath);
            var norm = AssetDatabase.LoadAssetAtPath<Texture2D>(VineNormalPath);
            var illum = AssetDatabase.LoadAssetAtPath<Texture2D>(VineIllumPath);

            if (albedo != null) mat.SetTexture("_MainTex", albedo);
            if (norm != null)
            {
                mat.EnableKeyword("_NORMALMAP");
                mat.SetTexture("_BumpMap", norm);
                mat.SetFloat("_BumpScale", 1.0f);
            }
            if (illum != null)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetTexture("_EmissionMap", illum);
                mat.SetColor("_EmissionColor", new Color(0.9f, 1.3f, 0.4f) * 1.5f); // Bioluminescent glow
            }

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        private static Material SetupCrystalMaterial()
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(CrystalMatPath);
            if (mat == null)
            {
                Shader shader = Shader.Find("Standard");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, CrystalMatPath);
            }

            var diff = AssetDatabase.LoadAssetAtPath<Texture2D>(CrystalDiffusePath);
            var norm = AssetDatabase.LoadAssetAtPath<Texture2D>(CrystalNormalPath);
            var pbr = AssetDatabase.LoadAssetAtPath<Texture2D>(CrystalPbrPath);

            if (diff != null) mat.SetTexture("_MainTex", diff);
            if (norm != null)
            {
                mat.EnableKeyword("_NORMALMAP");
                mat.SetTexture("_BumpMap", norm);
                mat.SetFloat("_BumpScale", 1.0f);
            }
            if (pbr != null)
            {
                mat.EnableKeyword("_METALLICGLOSSMAP");
                mat.SetTexture("_MetallicGlossMap", pbr);
            }
            mat.SetFloat("_Glossiness", 0.85f);
            mat.SetFloat("_Metallic", 0.9f);

            // Subtle magical crystal emission glow
            mat.EnableKeyword("_EMISSION");
            mat.SetTexture("_EmissionMap", diff);
            mat.SetColor("_EmissionColor", new Color(0.2f, 0.7f, 1.3f) * 1.2f); // Magical cyan/blue glow

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        private static void SetupSceneInstances(Material eggMat, Material vineMat, Material crystalMat)
        {
            GameObject diver = GameObject.Find("Diver");
            Vector3 diverPos = diver != null ? diver.transform.position : new Vector3(-19.82f, 1f, -54.04f);

            // 1. Creature Egg
            GameObject eggPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EggFbxPath);
            if (eggPrefab != null)
            {
                GameObject eggGO = GameObject.Find("CreatureEgg");
                if (eggGO == null)
                {
                    eggGO = (GameObject)PrefabUtility.InstantiatePrefab(eggPrefab);
                    eggGO.name = "CreatureEgg";
                }

                foreach (var mr in eggGO.GetComponentsInChildren<Renderer>())
                {
                    mr.sharedMaterial = eggMat;
                }

                // Place in front-right of Diver at eye-level
                eggGO.transform.position = diverPos + new Vector3(2.2f, 5.8f, 5.5f);
                eggGO.transform.rotation = Quaternion.Euler(15f, 45f, 0f);
                eggGO.transform.localScale = Vector3.one * 1.8f;

                Debug.Log($"[Abyssal] Positioned CreatureEgg at {eggGO.transform.position}");
            }

            // 2. Magic Crystal
            GameObject crystalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CrystalObjPath);
            if (crystalPrefab != null)
            {
                GameObject crystalGO = GameObject.Find("MagicCrystal");
                if (crystalGO == null)
                {
                    crystalGO = (GameObject)PrefabUtility.InstantiatePrefab(crystalPrefab);
                    crystalGO.name = "MagicCrystal";
                }

                foreach (var mr in crystalGO.GetComponentsInChildren<Renderer>())
                {
                    mr.sharedMaterial = crystalMat;
                }

                // Place in front-left of Diver at eye-level, floating like a magical submerged artifact
                crystalGO.transform.position = diverPos + new Vector3(-2.2f, 5.4f, 5.0f);
                crystalGO.transform.rotation = Quaternion.Euler(-10f, 35f, 15f);
                crystalGO.transform.localScale = Vector3.one * 1.6f;

                Debug.Log($"[Abyssal] Positioned MagicCrystal at {crystalGO.transform.position}");
            }
            else
            {
                Debug.LogWarning($"[Abyssal] Could not load crystal prefab at {CrystalObjPath}");
            }

            // 3. Creepvine Dense Cluster
            GameObject vinePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(VineFbxPath);
            if (vinePrefab != null)
            {
                GameObject cluster = GameObject.Find("Creepvine_Cluster");
                if (cluster == null)
                {
                    cluster = new GameObject("Creepvine_Cluster");
                    cluster.transform.position = diverPos;
                }

                string[] vineNames = { "Creepvine_Left", "Creepvine_Right", "Creepvine_Center", "Creepvine_Backdrop" };
                Vector3[] offsets = {
                    new Vector3(-4.8f, 1.0f, 6.5f),
                    new Vector3(4.8f, 1.5f, 7.5f),
                    new Vector3(-2.2f, 0.5f, 10.5f),
                    new Vector3(3.0f, 0.0f, 13.0f)
                };
                Vector3[] scales = {
                    new Vector3(5.5f, 10.0f, 5.5f),
                    new Vector3(6.0f, 11.5f, 6.0f),
                    new Vector3(5.5f, 10.5f, 5.5f),
                    new Vector3(7.0f, 13.0f, 7.0f)
                };

                for (int i = 0; i < vineNames.Length; i++)
                {
                    Transform existing = cluster.transform.Find(vineNames[i]);
                    GameObject vGO = existing != null ? existing.gameObject : null;
                    if (vGO == null)
                    {
                        vGO = (GameObject)PrefabUtility.InstantiatePrefab(vinePrefab, cluster.transform);
                        vGO.name = vineNames[i];
                    }

                    foreach (var mr in vGO.GetComponentsInChildren<Renderer>())
                    {
                        mr.sharedMaterial = vineMat;
                    }

                    vGO.transform.position = diverPos + offsets[i];
                    vGO.transform.rotation = Quaternion.Euler(0f, i * 65f, 0f);
                    vGO.transform.localScale = scales[i];
                }

                Debug.Log($"[Abyssal] Positioned Creepvines in cluster at {cluster.transform.position}");
            }
        }
    }
}
#endif
