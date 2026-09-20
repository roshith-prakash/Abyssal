using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Abyssal.Player
{
    /// <summary>
    /// Underwater 3D movement controller for the deep-sea diver.
    /// Handles pitch-directed swimming, vertical ascend/descend, water inertia/drag,
    /// procedural swimming kick sway, and animator driving.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class DiverMovement : MonoBehaviour
    {
        [Header("Movement Speeds")]
        [Tooltip("Standard swimming speed in meters per second.")]
        [SerializeField] private float swimSpeed = 3.5f;

        [Tooltip("Fast swimming speed when sprinting.")]
        [SerializeField] private float sprintSpeed = 6.0f;

        [Tooltip("Vertical ascend and descend speed.")]
        [SerializeField] private float verticalSpeed = 2.5f;

        [Header("Underwater Dynamics")]
        [Tooltip("How quickly the diver accelerates to target speed.")]
        [SerializeField] private float acceleration = 6.0f;

        [Tooltip("Water drag resistance when gliding to a stop.")]
        [SerializeField] private float drag = 3.5f;

        [Tooltip("Subtle upward/downward neutral buoyancy drift when idle.")]
        [SerializeField] private bool enableBuoyancyDrift = true;

        [SerializeField] private float buoyancyDriftSpeed = 0.15f;
        [SerializeField] private float buoyancyFrequency = 0.8f;

        [Header("References")]
        [Tooltip("The camera used to determine forward swimming direction.")]
        [SerializeField] private Transform cameraTransform;

        [Tooltip("Optional Animator on the diver model.")]
        [SerializeField] private Animator diverAnimator;

        [Header("Procedural Swimming Feedback")]
        [Tooltip("Simulates fin kicks by subtly bobbing and rolling the camera while swimming.")]
        [SerializeField] private bool enableFinKickSway = true;

        [SerializeField] private float finKickFrequency = 2.5f;
        [SerializeField] private float finKickBobAmount = 0.035f;
        [SerializeField] private float finKickRollAmount = 0.5f;

        private CharacterController characterController;
        private Vector3 currentVelocity = Vector3.zero;
        private Vector3 initialCameraLocalPos = Vector3.zero;
        private float finKickTimer = 0f;

        // Animator parameter hash IDs
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int IsSwimmingHash = Animator.StringToHash("IsSwimming");

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();

            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }

            if (cameraTransform != null)
            {
                initialCameraLocalPos = cameraTransform.localPosition;
            }

            if (diverAnimator == null)
            {
                diverAnimator = GetComponentInChildren<Animator>();
            }
        }

        private void Update()
        {
            HandleMovement();
            HandleSwimmingSway();
            UpdateAnimator();
        }

        private void HandleMovement()
        {
            Vector3 inputDir = ReadMovementInput(out bool isSprinting);

            // Calculate target speed
            float targetSpeed = isSprinting ? sprintSpeed : swimSpeed;

            // Direction calculation:
            // Forward/Back follows camera pitch & yaw (3D underwater swim)
            // Left/Right strafes horizontally relative to camera yaw
            Vector3 desiredDirection = Vector3.zero;

            if (cameraTransform != null)
            {
                Vector3 camForward = cameraTransform.forward;
                Vector3 camRight = cameraTransform.right;

                // 3D forward swimming (looking down swims down, looking up swims up)
                desiredDirection += camForward * inputDir.z;
                // Lateral strafing
                desiredDirection += camRight * inputDir.x;
                // Dedicated vertical ascend/descend (world space Y)
                desiredDirection += Vector3.up * inputDir.y;
            }
            else
            {
                desiredDirection = transform.forward * inputDir.z + transform.right * inputDir.x + Vector3.up * inputDir.y;
            }

            if (desiredDirection.sqrMagnitude > 1f)
            {
                desiredDirection.Normalize();
            }

            Vector3 targetVelocity = desiredDirection * targetSpeed;

            // Apply water drag / smooth acceleration
            float rate = (targetVelocity.sqrMagnitude > 0.01f) ? acceleration : drag;
            currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity, rate * Time.deltaTime);

            // Apply subtle neutral buoyancy float when nearly still
            if (enableBuoyancyDrift && currentVelocity.sqrMagnitude < 0.1f)
            {
                float driftY = Mathf.Sin(Time.time * buoyancyFrequency) * buoyancyDriftSpeed;
                currentVelocity.y += driftY * Time.deltaTime;
            }

            // Move the character controller
            if (characterController != null && characterController.enabled)
            {
                characterController.Move(currentVelocity * Time.deltaTime);
            }
        }

        private Vector3 ReadMovementInput(out bool isSprinting)
        {
            Vector3 input = Vector3.zero;
            isSprinting = false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed) input.z += 1f;
                if (Keyboard.current.sKey.isPressed) input.z -= 1f;
                if (Keyboard.current.dKey.isPressed) input.x += 1f;
                if (Keyboard.current.aKey.isPressed) input.x -= 1f;
                if (Keyboard.current.spaceKey.isPressed) input.y += 1f;
                if (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.cKey.isPressed) input.y -= 1f;
                isSprinting = Keyboard.current.leftShiftKey.isPressed;
            }

            if (Gamepad.current != null)
            {
                Vector2 leftStick = Gamepad.current.leftStick.ReadValue();
                input.x += leftStick.x;
                input.z += leftStick.y;

                if (Gamepad.current.rightTrigger.isPressed) input.y += 1f;
                if (Gamepad.current.leftTrigger.isPressed) input.y -= 1f;
                if (Gamepad.current.leftStickButton.isPressed) isSprinting = true;
            }
#else
            input.x = Input.GetAxisRaw("Horizontal");
            input.z = Input.GetAxisRaw("Vertical");

            if (Input.GetKey(KeyCode.Space)) input.y += 1f;
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C)) input.y -= 1f;
            isSprinting = Input.GetKey(KeyCode.LeftShift);
#endif

            return input;
        }

        private void HandleSwimmingSway()
        {
            if (!enableFinKickSway || cameraTransform == null) return;

            float horizontalSpeed = new Vector2(currentVelocity.x, currentVelocity.z).magnitude;
            bool isMoving = currentVelocity.magnitude > 0.3f;

            if (isMoving)
            {
                finKickTimer += Time.deltaTime * finKickFrequency * (horizontalSpeed / swimSpeed);
                float bobOffset = Mathf.Sin(finKickTimer) * finKickBobAmount;
                float rollOffset = Mathf.Cos(finKickTimer * 0.5f) * finKickRollAmount;

                cameraTransform.localPosition = initialCameraLocalPos + new Vector3(0f, bobOffset, 0f);
                cameraTransform.localRotation = Quaternion.Euler(
                    cameraTransform.localEulerAngles.x,
                    cameraTransform.localEulerAngles.y,
                    rollOffset
                );
            }
            else
            {
                // Smoothly return to center when stopped
                finKickTimer = 0f;
                cameraTransform.localPosition = Vector3.Lerp(
                    cameraTransform.localPosition,
                    initialCameraLocalPos,
                    5f * Time.deltaTime
                );
            }
        }

        private void UpdateAnimator()
        {
            if (diverAnimator == null || !diverAnimator.enabled) return;

            float speed = currentVelocity.magnitude;
            bool isSwimming = speed > 0.2f;

            diverAnimator.SetFloat(SpeedHash, speed);
            diverAnimator.SetBool(IsSwimmingHash, isSwimming);
        }

        /// <summary>
        /// Public read of current diver swimming speed.
        /// </summary>
        public float CurrentSpeed => currentVelocity.magnitude;

        /// <summary>
        /// Public read of whether diver is actively swimming.
        /// </summary>
        public bool IsSwimming => currentVelocity.magnitude > 0.2f;
    }
}
