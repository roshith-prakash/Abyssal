using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Abyssal.Player
{
    /// <summary>
    /// Underwater 3D movement controller for the deep-sea diver.
    /// Uses direct transform translation (no CharacterController) for true 6DOF
    /// underwater swimming. Handles water drag, buoyancy drift, fin-kick sway,
    /// and animator driving.
    /// </summary>
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
        [Tooltip("Simulates fin kicks by subtly rolling the camera while swimming.")]
        [SerializeField] private bool enableFinKickSway = true;

        [SerializeField] private float finKickFrequency = 2.5f;
        [SerializeField] private float finKickRollAmount = 0.5f;

        [Header("Physics Settings")]
        [Tooltip("Optional Rigidbody for PhysX collision handling (useGravity should be false).")]
        [SerializeField] private Rigidbody diverRigidbody;

        private Vector3 currentVelocity = Vector3.zero;
        private float   finKickTimer    = 0f;
        private bool    isControlsEnabled = true;

        private static readonly int SpeedHash      = Animator.StringToHash("Speed");
        private static readonly int IsSwimmingHash = Animator.StringToHash("IsSwimming");

        public bool IsControlsEnabled => isControlsEnabled;

        /// <summary>
        /// Enables or disables diver movement controls.
        /// When disabled, velocity is zeroed immediately and all swimming input is ignored.
        /// </summary>
        public void SetControlsEnabled(bool enabled)
        {
            isControlsEnabled = enabled;
            if (!enabled)
            {
                currentVelocity = Vector3.zero;
                if (diverRigidbody != null)
                {
#if UNITY_6000_0_OR_NEWER
                    diverRigidbody.linearVelocity = Vector3.zero;
                    diverRigidbody.angularVelocity = Vector3.zero;
#else
                    diverRigidbody.velocity = Vector3.zero;
                    diverRigidbody.angularVelocity = Vector3.zero;
#endif
                }
            }
        }

        // -----------------------------------------------------------------------
        private void Awake()
        {
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;

            if (diverAnimator == null)
                diverAnimator = GetComponentInChildren<Animator>();

            if (diverRigidbody == null)
                diverRigidbody = GetComponent<Rigidbody>();

            if (diverRigidbody != null)
            {
                diverRigidbody.useGravity = false;
                diverRigidbody.constraints = RigidbodyConstraints.FreezeRotation;
                diverRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                diverRigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
            }

            // Explicitly parent the camera to this Diver root at runtime.
            // Prefab-instance stripped transforms can fail to propagate position
            // to non-prefab children in Unity Play Mode. Doing it here — from the
            // script that IS on the Diver — is unambiguous and always works.
            if (cameraTransform != null && cameraTransform.parent != this.transform)
            {
                cameraTransform.SetParent(this.transform, worldPositionStays: true);
                Debug.Log("[DiverMovement] Camera parented to Diver root at runtime.");
            }
        }

        private void Update()
        {
            HandleMovement();
            HandleSwimmingSway();
            UpdateAnimator();
        }

        private void FixedUpdate()
        {
            // Apply velocity through Rigidbody if available for proper PhysX collisions
            if (diverRigidbody != null && !diverRigidbody.isKinematic)
            {
#if UNITY_6000_0_OR_NEWER
                diverRigidbody.linearVelocity = currentVelocity;
#else
                diverRigidbody.velocity = currentVelocity;
#endif
            }
        }

        // -----------------------------------------------------------------------
        private void HandleMovement()
        {
            if (!isControlsEnabled)
            {
                currentVelocity = Vector3.zero;
                return;
            }

            Vector3 inputDir = ReadMovementInput(out bool isSprinting);
            float targetSpeed = isSprinting ? sprintSpeed : swimSpeed;

            // Lazily resolve camera in case it wasn't ready at Awake
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;

            // Build desired direction from camera orientation (true 3D underwater)
            Vector3 desiredDir = Vector3.zero;
            if (cameraTransform != null)
            {
                desiredDir += cameraTransform.forward * inputDir.z;
                desiredDir += cameraTransform.right   * inputDir.x;
            }
            else
            {
                desiredDir = transform.forward * inputDir.z + transform.right * inputDir.x;
            }

            if (desiredDir.sqrMagnitude > 1f) desiredDir.Normalize();

            Vector3 targetVelocity = desiredDir * targetSpeed + Vector3.up * (inputDir.y * verticalSpeed);

            // Smooth acceleration / water drag
            float rate = targetVelocity.sqrMagnitude > 0.01f ? acceleration : drag;
            currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity, rate * Time.deltaTime);

            // Neutral buoyancy drift when still
            if (enableBuoyancyDrift && currentVelocity.sqrMagnitude < 0.1f)
            {
                currentVelocity.y += Mathf.Sin(Time.time * buoyancyFrequency) * buoyancyDriftSpeed * Time.deltaTime;
            }

            // Fallback: If no dynamic Rigidbody is attached, translate transform directly
            if (diverRigidbody == null || diverRigidbody.isKinematic)
            {
                transform.position += currentVelocity * Time.deltaTime;
            }
        }

        // -----------------------------------------------------------------------
        private Vector3 ReadMovementInput(out bool isSprinting)
        {
            Vector3 input = Vector3.zero;
            isSprinting = false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)    input.z += 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)  input.z -= 1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) input.x += 1f;
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)  input.x -= 1f;
                if (Keyboard.current.spaceKey.isPressed || Keyboard.current.eKey.isPressed)      input.y += 1f;
                if (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.cKey.isPressed || Keyboard.current.qKey.isPressed) input.y -= 1f;
                if (Keyboard.current.leftShiftKey.isPressed) isSprinting = true;
            }

            if (Gamepad.current != null)
            {
                Vector2 stick = Gamepad.current.leftStick.ReadValue();
                input.x += stick.x;
                input.z += stick.y;
                if (Gamepad.current.rightTrigger.isPressed)  input.y += 1f;
                if (Gamepad.current.leftTrigger.isPressed)   input.y -= 1f;
                if (Gamepad.current.leftStickButton.isPressed) isSprinting = true;
            }
#endif
            // Legacy fallback (covers "Old" and "Both" active input handling)
            try
            {
                if (input.sqrMagnitude < 0.001f)
                {
                    input.x = Input.GetAxisRaw("Horizontal");
                    input.z = Input.GetAxisRaw("Vertical");
                    if (Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.E)) input.y += 1f;
                    if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C) || Input.GetKey(KeyCode.Q)) input.y -= 1f;
                    if (Input.GetKey(KeyCode.LeftShift)) isSprinting = true;
                }
            }
            catch (System.Exception) { }

            return input;
        }

        // -----------------------------------------------------------------------
        private void HandleSwimmingSway()
        {
            if (!enableFinKickSway || cameraTransform == null || !isControlsEnabled) return;

            bool isMoving = currentVelocity.magnitude > 0.3f;
            if (isMoving)
            {
                float speed = new Vector2(currentVelocity.x, currentVelocity.z).magnitude;
                finKickTimer += Time.deltaTime * finKickFrequency * (speed / swimSpeed);
                float roll = Mathf.Cos(finKickTimer * 0.5f) * finKickRollAmount;
                cameraTransform.localRotation = Quaternion.Euler(
                    cameraTransform.localEulerAngles.x,
                    cameraTransform.localEulerAngles.y,
                    roll);
            }
            else
            {
                finKickTimer = 0f;
            }
        }

        // -----------------------------------------------------------------------
        private void UpdateAnimator()
        {
            if (diverAnimator == null || !diverAnimator.enabled) return;
            float speed = currentVelocity.magnitude;
            diverAnimator.SetFloat(SpeedHash, speed);
            diverAnimator.SetBool(IsSwimmingHash, speed > 0.2f);
        }

        public float CurrentSpeed => currentVelocity.magnitude;
        public bool  IsSwimming   => currentVelocity.magnitude > 0.2f;
    }
}

