#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Abyssal.Player;
using Abyssal.Scanner;
using Abyssal.Gameplay;
using Abyssal.UI;

namespace Abyssal.Editor
{
    /// <summary>
    /// Editor setup automation for the ABYSSAL Final Horror Cutscene.
    /// Configures the crystal trigger, cutscene master controller, Ghost Leviathan
    /// bioluminescent materials and head lighting, and hooks up all Inspector references.
    /// </summary>
    public static class SetupFinalCutscene
    {
        private const string RoarAudioPath = "Assets/Audio/Leviathan_Roar.wav";
        private const string AmbienceAudioPath = "Assets/Audio/Underwater_Ambience.wav";
        private const string GhostMatPath = "Assets/ghost-leviathan/Materials/Ghost_Leviathan_shell_spec.mat";
        private const string GhostNormalTexPath = "Assets/ghost-leviathan/textures/Ghost_Leviathan_shell_normal.png";
        private const string GhostIllumTexPath = "Assets/ghost-leviathan/textures/Ghost_Leviathan_shell_illum.png";

        [MenuItem("Abyssal/Setup Final Horror Cutscene", false, 6)]
        public static void ExecuteSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            Debug.Log("[Abyssal] Setting up Final Horror Cutscene System...");

            // Ensure MCP Server is started immediately upon domain reload
            try
            {
                var mcpServerType = System.Type.GetType("UnityMcp.Editor.MCP.McpServer, UnityMcp.Editor");
                if (mcpServerType != null)
                {
                    var initMethod = mcpServerType.GetMethod("Initialize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                    initMethod?.Invoke(null, null);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Abyssal] MCP Init: {ex.Message}");
            }

            // 1. Setup Ghost Leviathan Material & Head Lighting
            ConfigureGhostLeviathanMaterialAndLighting();

            // 2. Ensure Animations are set up
            SetupGhostLeviathanAnimations.ExecuteSetup();

            // 3. Setup Final Cutscene Trigger on Magic Crystal
            SetupCrystalTrigger();

            // 4. Setup Master Cutscene Controller GameObject & Wire References
            SetupCutsceneController();

            // 5. Mark Scene Dirty & Save
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[Abyssal] Final Horror Cutscene successfully configured and saved!");
        }

        private static void ConfigureGhostLeviathanMaterialAndLighting()
        {
            // Configure Normal Map texture importer
            var normalImporter = AssetImporter.GetAtPath(GhostNormalTexPath) as TextureImporter;
            if (normalImporter != null && normalImporter.textureType != TextureImporterType.NormalMap)
            {
                normalImporter.textureType = TextureImporterType.NormalMap;
                normalImporter.SaveAndReimport();
            }

            // Configure Material
            Material ghostMat = AssetDatabase.LoadAssetAtPath<Material>(GhostMatPath);
            Texture2D normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(GhostNormalTexPath);
            Texture2D illumTex = AssetDatabase.LoadAssetAtPath<Texture2D>(GhostIllumTexPath);

            if (ghostMat != null)
            {
                ghostMat.EnableKeyword("_EMISSION");
                if (normalTex != null) ghostMat.SetTexture("_BumpMap", normalTex);
                if (illumTex != null) ghostMat.SetTexture("_EmissionMap", illumTex);

                // Eerie deep-sea bioluminescent cyan emission
                ghostMat.SetColor("_EmissionColor", new Color(0.22f, 0.88f, 1.0f) * 2.8f);
                ghostMat.SetFloat("_Glossiness", 0.65f);
                ghostMat.SetFloat("_Metallic", 0.15f);
                EditorUtility.SetDirty(ghostMat);
                AssetDatabase.SaveAssets();
                Debug.Log("[Abyssal] Ghost Leviathan material emission and normal map configured.");
            }

            // Head Light on Ghost Leviathan
            GameObject ghostGO = GameObject.Find("Ghost_Leviathan");
            if (ghostGO != null)
            {
                Transform headBone = ghostGO.transform.Find("Armature/Ghost_Leviathan_anim/root/neck1/neck2/neck3/head");
                Transform lightParent = headBone != null ? headBone : ghostGO.transform;

                Transform headLightTrans = lightParent.Find("LeviathanHeadGlow");
                Light headLight = headLightTrans != null ? headLightTrans.GetComponent<Light>() : null;
                if (headLight == null)
                {
                    GameObject lg = new GameObject("LeviathanHeadGlow");
                    lg.transform.SetParent(lightParent, false);
                    headLight = lg.AddComponent<Light>();
                }

                headLight.type = LightType.Point;
                headLight.range = 28f;
                headLight.intensity = 3.5f;
                headLight.color = new Color(0.2f, 0.9f, 1.0f);
            }
        }

        private static void SetupCrystalTrigger()
        {
            GameObject crystalGO = GameObject.Find("MagicCrystal");
            if (crystalGO == null)
            {
                Debug.LogError("[Abyssal] MagicCrystal not found in scene!");
                return;
            }

            // Ensure FinalCutsceneTrigger is present on the crystal
            FinalCutsceneTrigger trigger = crystalGO.GetComponent<FinalCutsceneTrigger>();
            if (trigger == null) trigger = crystalGO.AddComponent<FinalCutsceneTrigger>();

            // Ensure trigger collider
            SphereCollider sphereCol = crystalGO.GetComponent<SphereCollider>();
            if (sphereCol == null) sphereCol = crystalGO.AddComponent<SphereCollider>();
            sphereCol.isTrigger = true;
            sphereCol.radius = 12.0f; // 12-meter radius around 9.5m crystal

            GameObject diverGO = GameObject.Find("Diver");
            if (diverGO != null)
            {
                SerializedObject serialized = new SerializedObject(trigger);
                serialized.FindProperty("playerTransform").objectReferenceValue = diverGO.transform;
                serialized.ApplyModifiedProperties();
            }

            EditorUtility.SetDirty(crystalGO);
        }

        private static void SetupCutsceneController()
        {
            GameObject managerGO = GameObject.Find("FinalCutsceneManager");
            if (managerGO == null)
            {
                managerGO = new GameObject("FinalCutsceneManager");
            }

            FinalCutsceneController controller = managerGO.GetComponent<FinalCutsceneController>();
            if (controller == null) controller = managerGO.AddComponent<FinalCutsceneController>();

            AudioSource audioSource = managerGO.GetComponent<AudioSource>();
            if (audioSource == null) audioSource = managerGO.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;

            // Wire up SerializedObject to set all Inspector references cleanly
            SerializedObject serialized = new SerializedObject(controller);

            // Player references
            GameObject diverGO = GameObject.Find("Diver");
            if (diverGO != null)
            {
                serialized.FindProperty("diverTransform").objectReferenceValue = diverGO.transform;
                serialized.FindProperty("diverMovement").objectReferenceValue = diverGO.GetComponent<DiverMovement>();
                serialized.FindProperty("diverHealth").objectReferenceValue = diverGO.GetComponent<DiverHealth>();
                serialized.FindProperty("scannerHUD").objectReferenceValue = diverGO.GetComponent<ScannerHUD>();

                Camera cam = diverGO.GetComponentInChildren<Camera>();
                if (cam == null && Camera.main != null) cam = Camera.main;
                if (cam != null)
                {
                    serialized.FindProperty("diverCamera").objectReferenceValue = cam.GetComponent<FirstPersonCamera>();
                    serialized.FindProperty("ambientAudioSource").objectReferenceValue = cam.GetComponent<AudioSource>();
                }
            }

            // Crystal references
            GameObject crystalGO = GameObject.Find("MagicCrystal");
            if (crystalGO != null)
            {
                serialized.FindProperty("crystalTransform").objectReferenceValue = crystalGO.transform;
                serialized.FindProperty("crystalLight").objectReferenceValue = crystalGO.GetComponentInChildren<Light>();
            }

            // Egg nest references
            GameObject nestGO = GameObject.Find("Leviathan_Egg_Nest");
            if (nestGO != null)
            {
                serialized.FindProperty("eggNestTransform").objectReferenceValue = nestGO.transform;
                SerializedProperty eggsProp = serialized.FindProperty("eggTransforms");
                eggsProp.ClearArray();
                int idx = 0;
                foreach (Transform child in nestGO.transform)
                {
                    if (child.name.Contains("Egg"))
                    {
                        eggsProp.InsertArrayElementAtIndex(idx);
                        eggsProp.GetArrayElementAtIndex(idx).objectReferenceValue = child;
                        idx++;
                    }
                }
            }

            // Leviathan references
            GameObject ghostGO = GameObject.Find("Ghost_Leviathan");
            if (ghostGO != null)
            {
                serialized.FindProperty("ghostLeviathanObject").objectReferenceValue = ghostGO;
                serialized.FindProperty("ghostLeviathanAnimator").objectReferenceValue = ghostGO.GetComponentInChildren<Animator>();
            }

            // Audio clips
            serialized.FindProperty("cutsceneAudioSource").objectReferenceValue = audioSource;
            AudioClip roarClip = AssetDatabase.LoadAssetAtPath<AudioClip>(RoarAudioPath);
            if (roarClip != null)
            {
                serialized.FindProperty("roarClip").objectReferenceValue = roarClip;
            }

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(managerGO);
            Debug.Log("[Abyssal] FinalCutsceneController references successfully wired!");
        }
    }
}
#endif
