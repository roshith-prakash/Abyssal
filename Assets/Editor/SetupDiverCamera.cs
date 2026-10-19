#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Abyssal.Player;

namespace Abyssal.Editor
{
    [InitializeOnLoad]
    public static class SetupDiverCamera
    {
        static SetupDiverCamera()
        {
            // Do not run automatically on compile; use menu item instead
        }

        [MenuItem("Abyssal/Setup Third Person & Animations", false, 0)]
        public static void SwitchToNewDiver()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            GameObject newDiverPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/diver/newdiver.fbx");
            if (newDiverPrefab == null)
            {
                Debug.LogError("[Abyssal] Could not find Assets/diver/newdiver.fbx!");
                return;
            }

            // Find existing Diver or Camera
            GameObject existingDiver = GameObject.Find("Diver");
            Transform cameraTrans = Camera.main != null ? Camera.main.transform : null;
            if (cameraTrans == null)
            {
                GameObject camGO = GameObject.Find("Main Camera");
                if (camGO != null) cameraTrans = camGO.transform;
            }

            Vector3 spawnPos = new Vector3(0f, 0f, -10f);
            Quaternion spawnRot = Quaternion.identity;

            if (existingDiver != null)
            {
                spawnPos = existingDiver.transform.position;
                spawnRot = existingDiver.transform.rotation;
                if (cameraTrans != null && cameraTrans.IsChildOf(existingDiver.transform))
                {
                    cameraTrans.SetParent(null, true);
                }
                Undo.DestroyObjectImmediate(existingDiver);
            }

            // Instantiate new diver
            GameObject diverGO = (GameObject)PrefabUtility.InstantiatePrefab(newDiverPrefab);
            diverGO.name = "Diver";
            diverGO.transform.position = spawnPos;
            diverGO.transform.rotation = spawnRot;
            Undo.RegisterCreatedObjectUndo(diverGO, "Instantiate newdiver");

            // Setup camera
            if (cameraTrans == null)
            {
                GameObject camGO = new GameObject("Main Camera");
                camGO.tag = "MainCamera";
                camGO.AddComponent<Camera>();
                camGO.AddComponent<AudioListener>();
                cameraTrans = camGO.transform;
            }

            Camera mainCam = cameraTrans.GetComponent<Camera>();

            // Parent camera directly to Diver without overriding its position
            Undo.SetTransformParent(cameraTrans, diverGO.transform, "Parent Camera to Diver");
            cameraTrans.localRotation = Quaternion.identity;
            cameraTrans.localScale = Vector3.one;

            if (mainCam != null)
            {
                mainCam.nearClipPlane = 0.03f;
                mainCam.fieldOfView = 75f;
            }

            // Ensure the ENTIRE diver body (head, helmet, goggles, suit, arms, oxygen tank, flippers) is fully visible
            Renderer[] allRenderers = diverGO.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in allRenderers)
            {
                r.enabled = true;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }

            // Setup Diver Flashlight mounted at helmet/eye level
            Transform flTrans = cameraTrans.Find("Diver Flashlight");
            Light fl = null;
            if (flTrans == null)
            {
                GameObject flGO = new GameObject("Diver Flashlight");
                flTrans = flGO.transform;
                flTrans.SetParent(cameraTrans, false);
                flTrans.localPosition = new Vector3(0.2f, -0.05f, 0.25f);
                flTrans.localRotation = Quaternion.identity;

                fl = flGO.AddComponent<Light>();
                fl.type = LightType.Spot;
                fl.color = new Color(0.82f, 0.94f, 1f);
                fl.intensity = 3.5f;
                fl.range = 25f;
                fl.spotAngle = 55f;
                fl.innerSpotAngle = 35f;
                fl.shadows = LightShadows.Soft;
            }
            else
            {
                fl = flTrans.GetComponent<Light>();
                flTrans.localPosition = new Vector3(0.2f, -0.05f, 0.25f);
            }

            // Setup FirstPersonCamera in First-Person mode
            FirstPersonCamera fpsCam = cameraTrans.GetComponent<FirstPersonCamera>();
            if (fpsCam == null)
            {
                fpsCam = cameraTrans.gameObject.AddComponent<FirstPersonCamera>();
            }

            SerializedObject soCam = new SerializedObject(fpsCam);
            soCam.FindProperty("playerBody").objectReferenceValue = diverGO.transform;
            soCam.FindProperty("cameraTransform").objectReferenceValue = cameraTrans;
            soCam.FindProperty("diverFlashlight").objectReferenceValue = fl;
            soCam.ApplyModifiedProperties();

            // CharacterController intentionally NOT added — DiverMovement uses direct
            // transform.position translation for 6DOF underwater movement.
            // A CharacterController would fight PhysX and freeze the diver in place.

            // Setup Animator & Animations
            Animator anim = diverGO.GetComponent<Animator>();
            if (anim == null)
            {
                anim = diverGO.GetComponentInChildren<Animator>();
            }
            if (anim == null)
            {
                anim = diverGO.AddComponent<Animator>();
            }

            // Configure loopTime on all clips in newdiver.fbx
            ModelImporter importer = AssetImporter.GetAtPath("Assets/diver/newdiver.fbx") as ModelImporter;
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
                }
            }

            // Find all AnimationClips in newdiver.fbx
            var subAssets = AssetDatabase.LoadAllAssetsAtPath("Assets/diver/newdiver.fbx");
            List<AnimationClip> clips = new List<AnimationClip>();
            foreach (var asset in subAssets)
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    clips.Add(clip);
                    Debug.Log($"[Abyssal] Found newdiver clip: '{clip.name}', Length: {clip.length:F2}s");
                }
            }

            // Create or load AnimatorController
            string controllerPath = "Assets/diver/DiverAnimatorController.controller";
            AnimatorController animController = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);

            if (animController == null)
            {
                animController = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                animController.AddParameter("Speed", AnimatorControllerParameterType.Float);
                animController.AddParameter("IsSwimming", AnimatorControllerParameterType.Bool);

                var rootStateMachine = animController.layers[0].stateMachine;

                if (clips.Count > 0)
                {
                    // State 1: Idle (first clip or treading water)
                    var idleState = rootStateMachine.AddState("Idle");
                    idleState.motion = clips[0];

                    // State 2: Swim (swimming stroke clip)
                    var swimState = rootStateMachine.AddState("Swim");
                    swimState.motion = clips.Count > 1 ? clips[1] : clips[0];

                    // Idle -> Swim transition
                    var toSwim = idleState.AddTransition(swimState);
                    toSwim.AddCondition(AnimatorConditionMode.If, 0, "IsSwimming");
                    toSwim.duration = 0.25f;
                    toSwim.hasExitTime = false;

                    // Swim -> Idle transition
                    var toIdle = swimState.AddTransition(idleState);
                    toIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsSwimming");
                    toIdle.duration = 0.25f;
                    toIdle.hasExitTime = false;
                }

                EditorUtility.SetDirty(animController);
                AssetDatabase.SaveAssets();
            }

            if (anim != null && animController != null)
            {
                anim.runtimeAnimatorController = animController;
            }

            // Setup DiverMovement
            DiverMovement move = diverGO.GetComponent<DiverMovement>();
            if (move == null)
            {
                move = diverGO.AddComponent<DiverMovement>();
            }

            SerializedObject soMove = new SerializedObject(move);
            soMove.FindProperty("cameraTransform").objectReferenceValue = cameraTrans;
            if (anim != null)
            {
                soMove.FindProperty("diverAnimator").objectReferenceValue = anim;
            }
            soMove.ApplyModifiedProperties();

            // Save scene
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[Abyssal] Successfully configured Diver First-Person Camera at eye level with visible arms!");
        }

        [MenuItem("Abyssal/Attach Camera to Diver Eye Level (First-Person)", false, 0)]
        public static void ExecuteSetup()
        {
            SwitchToNewDiver();
        }

        [MenuItem("Abyssal/Parent Camera to Diver (Preserve Position)", false, 1)]
        public static void ParentCameraKeepPosition()
        {
            GameObject diver = GameObject.Find("Diver");
            GameObject cam = GameObject.Find("Main Camera");
            if (diver != null && cam != null)
            {
                Undo.SetTransformParent(cam.transform, diver.transform, "Parent Camera to Diver");
                EditorUtility.SetDirty(cam);
                EditorUtility.SetDirty(diver);
                var scene = EditorSceneManager.GetActiveScene();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[Abyssal] Main Camera successfully parented to Diver while preserving its current position!");
            }
        }
    }
}
#endif
