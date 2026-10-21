using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Abyssal.Player
{
    /// <summary>
    /// Minimal first-person camera controller.
    /// ONLY handles mouse look (pitch + yaw) and cursor locking.
    /// Position is NEVER touched in code — the camera moves with the
    /// diver automatically because it is a child of the Diver GameObject.
    /// </summary>
    public class FirstPersonCamera : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The Diver root transform rotated horizontally (yaw). Auto-resolved from parent if null.")]
        [SerializeField] private Transform playerBody;

        [Tooltip("Optional flashlight toggled with F.")]
        [SerializeField] private Light diverFlashlight;

        [Header("Mouse Look")]
        [Range(0.1f, 10f)]
        [SerializeField] private float mouseSensitivity = 2.0f;

        [Tooltip("Smooth look input for a fluid underwater feel.")]
        [SerializeField] private bool smoothLook = true;

        [Range(1f, 30f)]
        [SerializeField] private float smoothSpeed = 15f;

        [Header("Pitch Clamp")]
        [Range(-90f, 0f)]
        [SerializeField] private float minPitch = -80f;

        [Range(0f, 90f)]
        [SerializeField] private float maxPitch = 80f;

        [Header("Flashlight")]
        [SerializeField] private bool startFlashlightOn = true;

        [Header("First-Person Mesh Visibility")]
        [Tooltip("If true, all renderers on the diver body are set to ShadowsOnly so they don't block the first-person camera while swimming, while still casting dynamic shadows on the seabed.")]
        [SerializeField] private bool hideBodyInFirstPerson = true;

        private float currentPitch = 0f;
        private float currentYaw   = 0f;
        private Vector2 smoothedDelta  = Vector2.zero;
        private Vector2 smoothVelocity = Vector2.zero;
        private bool isControlsEnabled = true;

        private Vector3 localOffsetFromBody; // camera's local offset from playerBody, set once at Start

        public bool IsControlsEnabled => isControlsEnabled;
        public bool IsFlashlightOn => diverFlashlight != null && diverFlashlight.enabled;
        public Light DiverFlashlight => diverFlashlight;

        /// <summary>
        /// Enables or disables free mouse-look and flashlight input.
        /// When disabled, mouse movement is ignored and cursor remains locked.
        /// </summary>
        public void SetControlsEnabled(bool enabled)
        {
            isControlsEnabled = enabled;
            if (!enabled)
            {
                smoothedDelta = Vector2.zero;
                smoothVelocity = Vector2.zero;
                LockCursor(true);
            }
        }

        /// <summary>
        /// Forcibly re-parents the camera to the player body and restores first-person view.
        /// Call this after any scripted cutscene that detached the camera, to guarantee
        /// the player returns to first-person mode with no orphaned transform.
        /// </summary>
        public void ReattachToPlayer()
        {
            if (playerBody == null) return;

            transform.SetParent(playerBody, worldPositionStays: false);
            transform.localPosition = localOffsetFromBody;

            // Re-sync pitch/yaw trackers so mouse-look continues from the correct angles
            currentPitch = transform.localEulerAngles.x;
            if (currentPitch > 180f) currentPitch -= 360f;
            currentYaw = playerBody.eulerAngles.y;

            smoothedDelta  = Vector2.zero;
            smoothVelocity = Vector2.zero;

            isControlsEnabled = true;
            EnforceFirstPersonVisibility();
            LockCursor(true);
        }

        /// <summary>
        /// Smoothly orients the camera towards a target world position during scripted cutscenes.
        /// </summary>
        public void SmoothLookToward(Vector3 targetWorldPosition, float speed)
        {
            Vector3 dir = (targetWorldPosition - transform.position).normalized;
            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, speed * Time.deltaTime);

                // Keep pitch and yaw trackers in sync
                currentPitch = transform.localEulerAngles.x;
                if (currentPitch > 180f) currentPitch -= 360f;
                if (playerBody != null)
                {
                    currentYaw = playerBody.eulerAngles.y;
                }
            }
        }

        /// <summary>
        /// Smoothly rotates the camera towards a target world rotation during scripted cutscenes.
        /// </summary>
        public void SmoothRotateToward(Quaternion targetWorldRotation, float speed)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, targetWorldRotation, speed * Time.deltaTime);
            currentPitch = transform.localEulerAngles.x;
            if (currentPitch > 180f) currentPitch -= 360f;
            if (playerBody != null)
            {
                currentYaw = playerBody.eulerAngles.y;
            }
        }

        private void Awake()
        {
            if (playerBody == null)
            {
                playerBody = transform.parent;
                if (playerBody == null)
                {
                    GameObject diver = GameObject.Find("Diver");
                    if (diver != null) playerBody = diver.transform;
                }
            }

            // Re-establish the parent-child link at runtime.
            // A camera whose m_Father points to a stripped prefab transform in the
            // scene YAML may not have its position inherited during Play Mode.
            // Calling SetParent here guarantees the link is live.
            if (playerBody != null && transform.parent != playerBody)
            {
                transform.SetParent(playerBody, worldPositionStays: true);
            }

            EnforceFirstPersonVisibility();
        }

        private void Start()
        {
            // Seed yaw from the player body's current world rotation
            currentYaw = playerBody != null ? playerBody.eulerAngles.y : transform.eulerAngles.y;

            // Seed pitch from camera's current local rotation, unwrapping from 0-360 to -180..180
            currentPitch = transform.localEulerAngles.x;
            if (currentPitch > 180f) currentPitch -= 360f;

            // Record the local offset so LateUpdate can re-apply if the parent link ever breaks
            localOffsetFromBody = transform.localPosition;

            if (diverFlashlight == null)
                diverFlashlight = GetComponentInChildren<Light>();

            if (diverFlashlight != null)
            {
                diverFlashlight.enabled = startFlashlightOn;
                if (diverFlashlight.intensity < 3.0f) diverFlashlight.intensity = 3.8f;
                if (diverFlashlight.range < 45f) diverFlashlight.range = 55f;
                if (diverFlashlight.spotAngle < 60f) diverFlashlight.spotAngle = 68f;
            }

            EnforceFirstPersonVisibility();
            LockCursor(true);
        }

        /// <summary>
        /// Ensures all 3D mesh renderers on the diver's body are set to ShadowsOnly.
        /// This prevents the scuba suit, oxygen tank, and flippers from blocking the
        /// camera during horizontal swimming animations, while retaining realistic dynamic
        /// shadows on the seafloor and rocks.
        /// </summary>
        public void EnforceFirstPersonVisibility()
        {
            if (!hideBodyInFirstPerson) return;

            Transform body = playerBody != null ? playerBody : transform.parent;
            if (body == null)
            {
                GameObject diver = GameObject.Find("Diver");
                if (diver != null) body = diver.transform;
            }

            if (body != null)
            {
                Renderer[] renderers = body.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer r in renderers)
                {
                    // Never modify renderers attached directly to the camera (e.g. handheld tools)
                    if (r.transform.IsChildOf(transform)) continue;

                    // Never hide particle effects like MarineSnow
                    if (r is ParticleSystemRenderer) continue;

                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                }
            }
        }

        private void LateUpdate()
        {
            // Safety net: if the camera has somehow been unparented (Unity quirk with
            // prefab-instance stripped transforms), re-parent and restore position.
            // Only enforce re-parenting when player controls are active — cutscenes
            // legitimately detach the camera and must not be overridden here.
            if (playerBody != null && transform.parent != playerBody && isControlsEnabled)
            {
                transform.SetParent(playerBody, worldPositionStays: false);
                transform.localPosition = localOffsetFromBody;
            }
        }

        private void Update()
        {
            if (!isControlsEnabled) return;

            HandleCursorLock();
            HandleMouseLook();
            HandleFlashlight();
        }

        private void HandleMouseLook()
        {
            if (!isControlsEnabled) return;

            // In Editor or Game view, allow mouse look if locked or when holding mouse buttons
            bool canLook = (Cursor.lockState == CursorLockMode.Locked);
#if UNITY_EDITOR
            if (!canLook)
            {
#if ENABLE_INPUT_SYSTEM
                if (Mouse.current != null && (Mouse.current.rightButton.isPressed || Mouse.current.leftButton.isPressed)) canLook = true;
#endif
                try { if (Input.GetMouseButton(1) || Input.GetMouseButton(0)) canLook = true; } catch { }
            }
#endif
            if (!canLook) return;

            Vector2 raw = ReadMouseDelta();

            if (smoothLook)
                smoothedDelta = Vector2.SmoothDamp(smoothedDelta, raw, ref smoothVelocity, 1f / smoothSpeed);
            else
                smoothedDelta = raw;

            float dYaw   = smoothedDelta.x * mouseSensitivity;
            float dPitch = smoothedDelta.y * mouseSensitivity;

            // Pitch — camera rotates on its local X axis
            currentPitch -= dPitch;
            currentPitch  = Mathf.Clamp(currentPitch, minPitch, maxPitch);
            transform.localRotation = Quaternion.Euler(currentPitch, 0f, 0f);

            // Yaw — player body rotates on world Y axis
            currentYaw += dYaw;
            if (playerBody != null)
                playerBody.rotation = Quaternion.Euler(0f, currentYaw, 0f);
            else
                transform.rotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
        }

        private Vector2 ReadMouseDelta()
        {
            Vector2 delta = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
                delta = Mouse.current.delta.ReadValue() * 0.1f;

            if (Gamepad.current != null)
            {
                Vector2 stick = Gamepad.current.rightStick.ReadValue();
                if (stick.sqrMagnitude > 0.01f)
                    delta += stick * 10f * Time.deltaTime;
            }
#endif
            // Legacy fallback — covers "Old" and "Both" active input handling modes
            if (delta.sqrMagnitude < 0.0001f)
            {
                try
                {
                    delta = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
                }
                catch (System.Exception) { }
            }

            return delta;
        }

        private void HandleCursorLock()
        {
            bool esc   = false;
            bool click = false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) esc = true;
            if (Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame)) click = true;
#endif
            try
            {
                if (Input.GetKeyDown(KeyCode.Escape)) esc = true;
                if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)) click = true;
            }
            catch (System.Exception) { }

            if (esc)
                LockCursor(false);
            else if (click && Cursor.lockState != CursorLockMode.Locked)
                LockCursor(true);
        }

        private void HandleFlashlight()
        {
            bool pressed = false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame) pressed = true;
#endif
            try { if (!pressed && Input.GetKeyDown(KeyCode.F)) pressed = true; }
            catch (System.Exception) { }

            if (pressed && diverFlashlight != null)
                diverFlashlight.enabled = !diverFlashlight.enabled;
        }

        public void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible   = !locked;
        }
    }
}

