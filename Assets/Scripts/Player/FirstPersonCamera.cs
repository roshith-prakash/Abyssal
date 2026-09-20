using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Abyssal.Player
{
    /// <summary>
    /// First-person camera controller placed at diver eye-level.
    /// Handles pitch/yaw mouse look, cursor locking, and diver flashlight control.
    /// Supports both the New Input System and legacy input fallback.
    /// </summary>
    public class FirstPersonCamera : MonoBehaviour
    {
        [Header("Target References")]
        [Tooltip("The root player transform rotated for horizontal yaw. If null, rotates this transform.")]
        [SerializeField] private Transform playerBody;

        [Tooltip("The camera transform rotated for vertical pitch. Defaults to this transform.")]
        [SerializeField] private Transform cameraTransform;

        [Tooltip("Optional diver flashlight (Spotlight) mounted to the helmet/camera.")]
        [SerializeField] private Light diverFlashlight;

        [Header("Look Sensitivity")]
        [Range(0.1f, 10f)]
        [SerializeField] private float mouseSensitivity = 2.0f;

        [Tooltip("Smooth look motion for fluid underwater feel.")]
        [SerializeField] private bool smoothLook = true;

        [Range(1f, 30f)]
        [SerializeField] private float smoothSpeed = 15f;

        [Header("Pitch Limits")]
        [Range(-90f, 0f)]
        [SerializeField] private float minPitch = -80f;

        [Range(0f, 90f)]
        [SerializeField] private float maxPitch = 80f;

        [Header("Perspective Mode")]
        [Tooltip("When true, places the camera above and behind the diver in third-person to view swimming animations.")]
        [SerializeField] private bool isThirdPerson = true;

        [Tooltip("Camera offset relative to the diver in third-person (elevated above the diver).")]
        [SerializeField] private Vector3 thirdPersonOffset = new Vector3(0.3f, 3.2f, -4.5f);

        [Tooltip("Downward pitch angle looking down at the diver from above in third-person.")]
        [SerializeField] private float thirdPersonPitchAngle = 20f;

        [Header("Eye Level Positioning (First-Person)")]
        [Tooltip("Default eye-level height in meters above diver ground position.")]
        [SerializeField] private float eyeHeight = 1.65f;

        [Header("Flashlight Settings")]
        [SerializeField] private bool startFlashlightOn = true;

        private float currentPitch = 0f;
        private float currentYaw = 0f;
        private Vector2 currentLookDelta = Vector2.zero;
        private Vector2 smoothLookVelocity = Vector2.zero;

        private void Awake()
        {
            if (cameraTransform == null)
            {
                cameraTransform = transform;
            }

            if (playerBody == null && transform.parent != null)
            {
                playerBody = transform.parent;
            }
        }

        private void Start()
        {
            // Initialize camera orientation
            currentPitch = cameraTransform.localEulerAngles.x;
            if (currentPitch > 180f) currentPitch -= 360f;

            if (playerBody != null)
            {
                currentYaw = playerBody.eulerAngles.y;
            }
            else
            {
                currentYaw = transform.eulerAngles.y;
            }

            // Apply initial position based on perspective mode
            ApplyPerspectivePosition();

            // Lock and hide cursor for gameplay
            LockCursor(true);

            // Initialize flashlight state
            if (diverFlashlight != null)
            {
                diverFlashlight.enabled = startFlashlightOn;
            }
        }

        private void Update()
        {
            HandleCursorToggle();
            HandleLookInput();
            HandleFlashlightToggle();
        }

        private void HandleLookInput()
        {
            // Only look when cursor is locked
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                return;
            }

            Vector2 rawLook = ReadLookDelta();

            if (smoothLook)
            {
                currentLookDelta = Vector2.SmoothDamp(
                    currentLookDelta,
                    rawLook,
                    ref smoothLookVelocity,
                    1f / smoothSpeed
                );
            }
            else
            {
                currentLookDelta = rawLook;
            }

            float mouseX = currentLookDelta.x * mouseSensitivity;
            float mouseY = currentLookDelta.y * mouseSensitivity;

            // Pitch (Vertical - Up/Down)
            currentPitch -= mouseY;
            currentPitch = Mathf.Clamp(currentPitch, minPitch, maxPitch);

            // Apply camera pitch looking down at the diver from above
            float pitchOffset = isThirdPerson ? thirdPersonPitchAngle : 0f;
            cameraTransform.localRotation = Quaternion.Euler(currentPitch + pitchOffset, 0f, 0f);
            ApplyPerspectivePosition();

            // Yaw (Horizontal - Left/Right)
            currentYaw += mouseX;
            if (playerBody != null)
            {
                playerBody.rotation = Quaternion.Euler(0f, currentYaw, 0f);
            }
            else
            {
                transform.rotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
            }
        }

        private void ApplyPerspectivePosition()
        {
            if (cameraTransform == null) return;

            if (isThirdPerson)
            {
                cameraTransform.localPosition = thirdPersonOffset;
            }
            else
            {
                cameraTransform.localPosition = new Vector3(0f, eyeHeight, 0f);
            }
        }

        private Vector2 ReadLookDelta()
        {
            Vector2 delta = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                delta = Mouse.current.delta.ReadValue() * 0.1f;
            }

            if (Gamepad.current != null)
            {
                Vector2 stick = Gamepad.current.rightStick.ReadValue();
                if (stick.sqrMagnitude > 0.01f)
                {
                    delta += stick * 10f * Time.deltaTime;
                }
            }
#else
            delta = new Vector2(
                Input.GetAxisRaw("Mouse X"),
                Input.GetAxisRaw("Mouse Y")
            );
#endif

            return delta;
        }

        private void HandleCursorToggle()
        {
            // Unlock on Escape
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                LockCursor(false);
            }
            // Re-lock on Left Click inside game view
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            {
                LockCursor(true);
            }
#else
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                LockCursor(false);
            }
            if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked)
            {
                LockCursor(true);
            }
#endif
        }

        private void HandleFlashlightToggle()
        {
            bool togglePressed = false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            {
                togglePressed = true;
            }
#else
            if (Input.GetKeyDown(KeyCode.F))
            {
                togglePressed = true;
            }
#endif

            if (togglePressed && diverFlashlight != null)
            {
                diverFlashlight.enabled = !diverFlashlight.enabled;
            }
        }

        /// <summary>
        /// Set cursor lock and visibility state.
        /// </summary>
        public void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        /// <summary>
        /// Adjust camera local position to eye height.
        /// </summary>
        [ContextMenu("Align to Eye Height")]
        public void SetEyeHeight(float height)
        {
            eyeHeight = height;
            Vector3 pos = cameraTransform.localPosition;
            pos.y = eyeHeight;
            cameraTransform.localPosition = pos;
        }
    }
}
