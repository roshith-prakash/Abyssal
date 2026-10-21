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
    [InitializeOnLoad]
    public static class SetupFinalCutscene
    {
        private const string RoarAudioPath = "Assets/Audio/Reaper roar.mp3";
        private const string AmbienceAudioPath = "Assets/Audio/First Immersion.mp3";
        private const string GhostMatPath = "Assets/ghost-leviathan/Materials/Ghost_Leviathan_shell_spec.mat";
        private const string GhostNormalTexPath = "Assets/ghost-leviathan/textures/Ghost_Leviathan_shell_normal.png";
        private const string GhostIllumTexPath = "Assets/ghost-leviathan/textures/Ghost_Leviathan_shell_illum.png";

        static SetupFinalCutscene()
        {
            try
            {
                EditorPrefs.SetBool("kRunInBackground", true);
                Application.runInBackground = true;
                EditorApplication.delayCall += () =>
                {
                    try { CleanupRogueAudiosAndEnforceClips(); } catch {}
                };
            }
            catch {}

            try
            {
                if (!Application.isBatchMode)
                {
                    var mcpServerType = System.Type.GetType("UnityMcp.Editor.MCP.McpServer, UnityMcp.Editor");
                    if (mcpServerType != null)
                    {
                        var initMethod = mcpServerType.GetMethod("Initialize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                        initMethod?.Invoke(null, null);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Abyssal] MCP Init in static ctor: {ex.Message}");
            }

            try
            {
                UnityMcp.Editor.MCP.JsonRpcDispatcher.RegisterMethod("abyssal.test_cutscene", p =>
                {
                    if (FinalCutsceneController.Instance != null)
                    {
                        FinalCutsceneController.Instance.StartCutscene();
                        return new Newtonsoft.Json.Linq.JObject { ["ok"] = true };
                    }
                    var controller = Object.FindAnyObjectByType<FinalCutsceneController>();
                    if (controller != null)
                    {
                        controller.StartCutscene();
                        return new Newtonsoft.Json.Linq.JObject { ["ok"] = true };
                    }
                    return new Newtonsoft.Json.Linq.JObject { ["ok"] = false, ["error"] = "Controller not found" };
                });

                UnityMcp.Editor.MCP.JsonRpcDispatcher.RegisterMethod("abyssal.teleport_diver", p =>
                {
                    float x = (float)(p["x"] ?? 0f);
                    float y = (float)(p["y"] ?? 0f);
                    float z = (float)(p["z"] ?? 0f);
                    var diver = Object.FindAnyObjectByType<DiverMovement>();
                    if (diver != null)
                    {
                        diver.Teleport(new Vector3(x, y, z));
                        return new Newtonsoft.Json.Linq.JObject { ["ok"] = true, ["pos"] = $"{x}, {y}, {z}" };
                    }
                    return new Newtonsoft.Json.Linq.JObject { ["ok"] = false, ["error"] = "DiverMovement not found" };
                });

                UnityMcp.Editor.MCP.JsonRpcDispatcher.RegisterMethod("abyssal.setup_cutscene", p =>
                {
                    ExecuteSetup();
                    return new Newtonsoft.Json.Linq.JObject { ["ok"] = true };
                });

                UnityMcp.Editor.MCP.JsonRpcDispatcher.RegisterMethod("abyssal.set_playmode", p =>
                {
                    bool play = (bool)(p["play"] ?? true);
                    EditorApplication.isPlaying = play;
                    return new Newtonsoft.Json.Linq.JObject { ["ok"] = true, ["isPlaying"] = EditorApplication.isPlaying };
                });

                UnityMcp.Editor.MCP.JsonRpcDispatcher.RegisterMethod("abyssal.get_diver_state", p =>
                {
                    var diver = Object.FindAnyObjectByType<DiverMovement>();
                    var cam = Camera.main;
                    var fpsCam = cam != null ? cam.GetComponent<FirstPersonCamera>() : null;
                    var cutscene = Object.FindAnyObjectByType<FinalCutsceneController>();
                    var diverRb = diver != null ? diver.GetComponent<Rigidbody>() : null;

                    var obj = new Newtonsoft.Json.Linq.JObject();
                    obj["isPlaying"] = EditorApplication.isPlaying;
                    obj["isPaused"] = EditorApplication.isPaused;

                    if (diver != null)
                    {
                        obj["diver_pos"] = $"{diver.transform.position.x:F2}, {diver.transform.position.y:F2}, {diver.transform.position.z:F2}";
                        obj["diver_controls_enabled"] = diver.IsControlsEnabled;
                        obj["diver_current_speed"] = diver.CurrentSpeed;
                        if (diverRb != null)
                        {
#if UNITY_6000_0_OR_NEWER
                            obj["rb_linear_velocity"] = $"{diverRb.linearVelocity.x:F2}, {diverRb.linearVelocity.y:F2}, {diverRb.linearVelocity.z:F2}";
                            obj["rb_linear_damping"] = diverRb.linearDamping;
#else
                            obj["rb_linear_velocity"] = $"{diverRb.velocity.x:F2}, {diverRb.velocity.y:F2}, {diverRb.velocity.z:F2}";
                            obj["rb_linear_damping"] = diverRb.drag;
#endif
                            obj["rb_is_kinematic"] = diverRb.isKinematic;
                            obj["rb_use_gravity"] = diverRb.useGravity;
                        }
                    }

                    if (cam != null)
                    {
                        obj["cam_pos"] = $"{cam.transform.position.x:F2}, {cam.transform.position.y:F2}, {cam.transform.position.z:F2}";
                        obj["cam_parent"] = cam.transform.parent != null ? cam.transform.parent.name : "null (root)";
                    }

                    if (fpsCam != null)
                    {
                        obj["fps_controls_enabled"] = fpsCam.IsControlsEnabled;
                    }

                    if (cutscene != null)
                    {
                        obj["cutscene_state"] = cutscene.CurrentState.ToString();
                        obj["cutscene_active"] = cutscene.IsCutsceneActive;
                    }

#if ENABLE_INPUT_SYSTEM
                    obj["gamepad_present"] = UnityEngine.InputSystem.Gamepad.current != null;
                    if (UnityEngine.InputSystem.Gamepad.current != null)
                    {
                        obj["gp_rt_pressed"] = UnityEngine.InputSystem.Gamepad.current.rightTrigger.isPressed;
                        obj["gp_rt_val"] = UnityEngine.InputSystem.Gamepad.current.rightTrigger.ReadValue();
                        obj["gp_lt_val"] = UnityEngine.InputSystem.Gamepad.current.leftTrigger.ReadValue();
                        obj["gp_stick"] = UnityEngine.InputSystem.Gamepad.current.leftStick.ReadValue().ToString();
                    }

                    var devList = new Newtonsoft.Json.Linq.JArray();
                    foreach (var dev in UnityEngine.InputSystem.InputSystem.devices)
                    {
                        if (dev.added) devList.Add(dev.displayName + " (" + dev.layout + ")");
                    }
                    obj["devices"] = devList;
#endif

                    return obj;
                });
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Abyssal] MCP RPC registration: {ex.Message}");
            }

            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode && !Application.isBatchMode)
                {
                    GameObject ghost = GameObject.Find("Ghost_Leviathan");
                    if (ghost == null) ghost = GameObject.Find("GhostLeviathan");
                    if (ghost == null)
                    {
                        Debug.Log("[Abyssal] Ghost_Leviathan not found in scene on load. Running setup automation...");
                        ExecuteSetup();
                    }
                }
            };
        }

        [MenuItem("Abyssal/Setup Final Horror Cutscene", false, 6)]
        public static void ExecuteSetup()
        {
            if (EditorApplication.isPlaying)
            {
                TestCutsceneNow();
                return;
            }

            Debug.Log("[Abyssal] Setting up Final Horror Cutscene System...");
            InspectLeviathan();

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

            // 1. Ensure Ghost Leviathan is in scene & configure material & head lighting
            EnsureGhostLeviathanInScene();

            // 2. Ensure Animations are set up
            SetupGhostLeviathanAnimations.ExecuteSetup();

            // 3. Setup Final Cutscene Trigger on Magic Crystal
            SetupCrystalTrigger();

            // 4. Setup Master Cutscene Controller GameObject & Wire References
            SetupCutsceneController();

            // 5. Cleanup Rogue Audios and enforce proper clips
            CleanupRogueAudiosAndEnforceClips();

            // 6. Mark Scene Dirty & Save
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[Abyssal] Final Horror Cutscene successfully configured and saved!");
        }

        [MenuItem("Abyssal/Cleanup Rogue Audios And Enforce Clips", false, 6)]
        public static void CleanupRogueAudiosAndEnforceClips()
        {
            // 1. Remove rogue LeviathanEncounterController so no audio plays at start
            GameObject rogueController = GameObject.Find("LeviathanEncounterController");
            if (rogueController != null)
            {
                Undo.DestroyObjectImmediate(rogueController);
                Debug.Log("[Abyssal] Removed rogue LeviathanEncounterController from scene.");
            }

            // 2. Clear any audio clips from Reaper / Ghost Leviathan patrol sources so no roar plays on patrol
            foreach (var audioSource in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include))
            {
                if (audioSource.gameObject.name.Contains("Reaper") || audioSource.gameObject.name.Contains("Ghost"))
                {
                    audioSource.clip = null;
                    audioSource.playOnAwake = false;
                    audioSource.Stop();
                    EditorUtility.SetDirty(audioSource);
                }
            }

            // 3. Configure Camera.main with First Immersion.mp3 (plays throughout game)
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                AudioSource camAudio = mainCam.GetComponent<AudioSource>();
                if (camAudio == null) camAudio = mainCam.gameObject.AddComponent<AudioSource>();
                AudioClip musicClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AmbienceAudioPath);
                if (musicClip != null)
                {
                    camAudio.clip = musicClip;
                    camAudio.loop = true;
                    camAudio.volume = 0.65f;
                    camAudio.spatialBlend = 0f;
                    camAudio.playOnAwake = true;
                    EditorUtility.SetDirty(camAudio);
                }
            }

            // 4. Enforce Reaper roar on FinalCutsceneController for the end cutscene
            FinalCutsceneController cutscene = Object.FindAnyObjectByType<FinalCutsceneController>();
            if (cutscene != null)
            {
                SerializedObject serialized = new SerializedObject(cutscene);
                AudioClip roarClip = AssetDatabase.LoadAssetAtPath<AudioClip>(RoarAudioPath);
                if (roarClip != null)
                {
                    serialized.FindProperty("roarClip").objectReferenceValue = roarClip;
                    serialized.ApplyModifiedProperties();
                    EditorUtility.SetDirty(cutscene);
                }
            }

            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.isLoaded)
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
            }
        }

        [MenuItem("Abyssal/Test Final Cutscene Now", false, 7)]
        public static void TestCutsceneNow()
        {
            if (FinalCutsceneController.Instance != null)
            {
                Debug.Log("[Abyssal] Manually triggering cutscene for testing via menu item.");
                FinalCutsceneController.Instance.StartCutscene();
            }
            else
            {
                var controller = Object.FindAnyObjectByType<FinalCutsceneController>();
                if (controller != null)
                {
                    controller.StartCutscene();
                }
                else
                {
                    Debug.LogError("[Abyssal] FinalCutsceneController not found in scene!");
                }
            }
        }

        public static GameObject EnsureGhostLeviathanInScene()
        {
            GameObject ghostGO = GameObject.Find("Ghost_Leviathan");
            if (ghostGO == null) ghostGO = GameObject.Find("GhostLeviathan");

            if (ghostGO == null)
            {
                GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ghost-leviathan/source/Ghost_Leviathan.fbx");
                if (fbx == null)
                {
                    Debug.LogError("[Abyssal] Cannot find Ghost_Leviathan.fbx at Assets/ghost-leviathan/source/Ghost_Leviathan.fbx");
                    return null;
                }

                ghostGO = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
                ghostGO.name = "Ghost_Leviathan";
                Undo.RegisterCreatedObjectUndo(ghostGO, "Create Ghost_Leviathan");
                Debug.Log("[Abyssal] Instantiated Ghost_Leviathan into scene from FBX.");
            }

            ghostGO.SetActive(true);
            ghostGO.transform.position = new Vector3(0f, -18f, 185f);
            ghostGO.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            ghostGO.transform.localScale = Vector3.one * 35.0f;

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

                foreach (var renderer in ghostGO.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.sharedMaterial = ghostMat;
                }
            }

            // Head Light on Ghost Leviathan
            Transform headBone = ghostGO.transform.Find("Armature/Ghost_Leviathan_anim/root/neck1/neck2/neck3/head");
            if (headBone == null)
            {
                foreach (Transform child in ghostGO.GetComponentsInChildren<Transform>())
                {
                    if (child.name.Equals("head", System.StringComparison.OrdinalIgnoreCase))
                    {
                        headBone = child;
                        break;
                    }
                }
            }

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
            headLight.range = 50f;
            headLight.intensity = 6.5f;
            headLight.color = new Color(0.2f, 0.9f, 1.0f);

            Animator anim = ghostGO.GetComponent<Animator>();
            if (anim != null)
            {
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            foreach (var col in ghostGO.GetComponentsInChildren<Collider>(true))
            {
                col.isTrigger = true;
            }

            EditorUtility.SetDirty(ghostGO);
            return ghostGO;
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

            // Ensure trigger collider with 30m world radius
            SphereCollider sphereCol = crystalGO.GetComponent<SphereCollider>();
            if (sphereCol == null) sphereCol = crystalGO.AddComponent<SphereCollider>();
            sphereCol.isTrigger = true;
            float maxScale = Mathf.Max(crystalGO.transform.lossyScale.x, Mathf.Max(crystalGO.transform.lossyScale.y, crystalGO.transform.lossyScale.z));
            float localRadius = maxScale > 0.0001f ? (30.0f / maxScale) : 30.0f;
            sphereCol.radius = localRadius;
            SerializedObject serializedCol = new SerializedObject(sphereCol);
            serializedCol.FindProperty("m_Radius").floatValue = localRadius;
            serializedCol.FindProperty("m_IsTrigger").boolValue = true;
            serializedCol.ApplyModifiedProperties();
            EditorUtility.SetDirty(sphereCol);

            GameObject diverGO = GameObject.Find("Diver");
            if (diverGO != null)
            {
                SerializedObject serialized = new SerializedObject(trigger);
                serialized.FindProperty("playerTransform").objectReferenceValue = diverGO.transform;
                serialized.FindProperty("triggerRadius").floatValue = 30.0f;
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
            GameObject ghostGO = EnsureGhostLeviathanInScene();
            if (ghostGO != null)
            {
                serialized.FindProperty("ghostLeviathanObject").objectReferenceValue = ghostGO;
                serialized.FindProperty("ghostLeviathanAnimator").objectReferenceValue = ghostGO.GetComponentInChildren<Animator>();
                Transform headBone = ghostGO.transform.Find("Armature/Ghost_Leviathan_anim/root/neck1/neck2/neck3/head");
                if (headBone != null)
                {
                    serialized.FindProperty("ghostHeadTransform").objectReferenceValue = headBone;
                }
            }

            // Audio clips
            serialized.FindProperty("cutsceneAudioSource").objectReferenceValue = audioSource;
            AudioClip roarClip = AssetDatabase.LoadAssetAtPath<AudioClip>(RoarAudioPath);
            if (roarClip != null)
            {
                serialized.FindProperty("roarClip").objectReferenceValue = roarClip;
            }

            serialized.FindProperty("spawnDistance").floatValue = 28.0f;
            serialized.FindProperty("chargeDuration").floatValue = 2.0f;
            serialized.FindProperty("revealDuration").floatValue = 1.2f;

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(managerGO);
            Debug.Log("[Abyssal] FinalCutsceneController references successfully wired!");
        }

        private static void InspectLeviathan()
        {
            GameObject ghost = GameObject.Find("Ghost_Leviathan");
            if (ghost != null)
            {
                SkinnedMeshRenderer smr = ghost.GetComponentInChildren<SkinnedMeshRenderer>();
                Debug.Log($"[Inspect] Ghost root: pos={ghost.transform.position}, scale={ghost.transform.localScale}");
                if (smr != null)
                {
                    Debug.Log($"[Inspect] SMR local bounds: center={smr.localBounds.center}, size={smr.localBounds.size}");
                    Debug.Log($"[Inspect] SMR world bounds: center={smr.bounds.center}, size={smr.bounds.size}");
                }

                foreach (var t in ghost.GetComponentsInChildren<Transform>())
                {
                    if (t.name.ToLower().Contains("head"))
                    {
                        Debug.Log($"[Inspect] Child '{t.name}' world pos: {t.position}, delta from root: {t.position - ghost.transform.position}");
                    }
                }
            }
        }
    }
}
#endif
