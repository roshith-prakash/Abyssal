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

        private float currentPitch = 0f;
        private float currentYaw   = 0f;
        private Vector2 smoothedDelta  = Vector2.zero;
        private Vector2 smoothVelocity = Vector2.zero;

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
        }

        private void Start()
        {
            // Seed yaw from the player body's current world rotation
            currentYaw = playerBody != null ? playerBody.eulerAngles.y : transform.eulerAngles.y;

            // Seed pitch from camera's current local rotation, unwrapping from 0-360 to -180..180
            currentPitch = transform.localEulerAngles.x;
            if (currentPitch > 180f) currentPitch -= 360f;

            if (diverFlashlight != null)
                diverFlashlight.enabled = startFlashlightOn;

            LockCursor(true);
        }

        private void Update()
        {
            HandleCursorLock();
            HandleMouseLook();
            HandleFlashlight();
        }

        private void HandleMouseLook()
        {
            if (Cursor.lockState != CursorLockMode.Locked) return;

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
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) esc   = true;
            if (Mouse.current    != null && Mouse.current.leftButton.wasPressedThisFrame)   click = true;
#endif
            try
            {
                if (Input.GetKeyDown(KeyCode.Escape)) esc   = true;
                if (Input.GetMouseButtonDown(0))      click = true;
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

