#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Abyssal.Editor
{
    [InitializeOnLoad]
    public static class SetupGhostLeviathanAnimations
    {
        private const string FbxPath = "Assets/ghost-leviathan/source/Ghost_Leviathan.fbx";
        private const string ControllerPath = "Assets/ghost-leviathan/GhostLeviathanController.controller";

        static SetupGhostLeviathanAnimations()
        {
            // Do not run automatically on compile; use menu item instead
        }

        // Menu item to trigger setup
        [MenuItem("Abyssal/Setup Ghost Leviathan Animations", false, 10)]
        public static void ExecuteSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            // 1. Ensure FBX clip has loopTime enabled
            ModelImporter importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
            if (importer != null && (importer.clipAnimations == null || importer.clipAnimations.Length == 0))
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
                    Debug.Log("[Abyssal] Configured Ghost Leviathan animation clips to loop and reimported FBX.");
                }
            }
            else
            {
                Debug.LogError($"[Abyssal] Could not find ModelImporter at {FbxPath}");
                return;
            }

            // 2. Load all AnimationClips from the FBX
            var subAssets = AssetDatabase.LoadAllAssetsAtPath(FbxPath);
            AnimationClip swimClip = null;
            Avatar fbxAvatar = null;

            foreach (var asset in subAssets)
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    swimClip = clip;
                    Debug.Log($"[Abyssal] Found Ghost Leviathan clip: '{clip.name}', Length: {clip.length:F2}s");
                }
                else if (asset is Avatar avatar)
                {
                    fbxAvatar = avatar;
                }
            }

            if (swimClip == null)
            {
                Debug.LogError("[Abyssal] No valid AnimationClip found in Ghost_Leviathan.fbx!");
                return;
            }

            // 3. Create or load AnimatorController
            AnimatorController animController = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (animController == null)
            {
                animController = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
                var rootStateMachine = animController.layers[0].stateMachine;

                var swimState = rootStateMachine.AddState("Swim");
                swimState.motion = swimClip;
                swimState.speed = 1.0f;
                rootStateMachine.defaultState = swimState;

                EditorUtility.SetDirty(animController);
                AssetDatabase.SaveAssets();
                Debug.Log($"[Abyssal] Created AnimatorController at {ControllerPath}");
            }
            else
            {
                // Ensure default state has the swim motion
                var rootStateMachine = animController.layers[0].stateMachine;
                if (rootStateMachine.defaultState == null)
                {
                    var swimState = rootStateMachine.AddState("Swim");
                    swimState.motion = swimClip;
                    swimState.speed = 1.0f;
                    rootStateMachine.defaultState = swimState;
                }
                else if (rootStateMachine.defaultState.motion == null)
                {
                    rootStateMachine.defaultState.motion = swimClip;
                }
                EditorUtility.SetDirty(animController);
                AssetDatabase.SaveAssets();
            }

            // 4. Find Ghost_Leviathan in the scene
            GameObject ghostGO = GameObject.Find("Ghost_Leviathan");
            if (ghostGO == null)
            {
                Debug.LogWarning("[Abyssal] Ghost_Leviathan GameObject not found in scene!");
                return;
            }

            // 5. Setup Animator component
            Animator anim = ghostGO.GetComponent<Animator>();
            if (anim == null)
            {
                anim = ghostGO.AddComponent<Animator>();
                Undo.RegisterCreatedObjectUndo(anim, "Add Animator to Ghost_Leviathan");
            }

            anim.runtimeAnimatorController = animController;
            if (fbxAvatar != null)
            {
                anim.avatar = fbxAvatar;
            }
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            anim.applyRootMotion = false;

            EditorUtility.SetDirty(ghostGO);

            // 6. Save Scene
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[Abyssal] Successfully configured Ghost Leviathan animations!");
        }
    }
}
#endif
