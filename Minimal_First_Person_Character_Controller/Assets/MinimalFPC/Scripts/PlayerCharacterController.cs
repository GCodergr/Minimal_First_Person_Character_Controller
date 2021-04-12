using UnityEngine;
using UnityEngine.Events;

namespace MinimalFPC
{
    [RequireComponent(typeof(CharacterController), typeof(PlayerInputHandler), typeof(AudioSource))]
    public class PlayerCharacterController : MonoBehaviour
    {
        #region Fields and Properties
        [Header("References")]
        [Tooltip("Reference to the main camera used for the player")]
        public Camera playerCamera;
        [Tooltip("Audio source for footsteps, jump, etc...")]
        public AudioSource audioSource;

        [Header("General")] 
        [Tooltip("Force applied downward when in the air")]
        public float gravityDownForce = 20f;
        [Tooltip("Physic layers checked to consider the player grounded")]
        public LayerMask groundCheckLayers = -1;
        [Tooltip("distance from the bottom of the character controller capsule to test for grounded")]
        public float groundCheckDistance = 0.05f;

        [Header("Movement")] 
        [Tooltip("Max movement speed when grounded (when not sprinting)")]
        public float maxSpeedOnGround = 10f;
        [Tooltip("Sharpness for the movement when grounded, a low value will make the player accelerate and decelerate slowly, a high value will do the opposite")]
        public float movementSharpnessOnGround = 15;
        [Tooltip("Max movement speed when crouching")] [Range(0, 1)]
        public float maxSpeedCrouchedRatio = 0.5f;
        [Tooltip("Max movement speed when not grounded")]
        public float maxSpeedInAir = 10f;
        [Tooltip("Acceleration speed when in the air")]
        public float accelerationSpeedInAir = 25f;
        [Tooltip("Multiplier for the sprint speed (based on grounded speed)")]
        public float sprintSpeedModifier = 2f;

        [Header("Rotation")] 
        [Tooltip("Rotation speed for moving the camera")]
        public float rotationSpeed = 200f;

        [Header("Jump")] 
        [Tooltip("Force applied upward when jumping")]
        public float jumpForce = 9f;

        [Header("Stance")] 
        [Tooltip("Ratio (0-1) of the character height where the camera will be at")]
        public float cameraHeightRatio = 0.9f;
        [Tooltip("Height of character when standing")]
        public float capsuleHeightStanding = 1.8f;
        [Tooltip("Height of character when crouching")]
        public float capsuleHeightCrouching = 0.9f;
        [Tooltip("Speed of crouching transitions")]
        public float crouchingSharpness = 10f;

        [Header("Audio")] 
        [Tooltip("Amount of footstep sounds played when moving one meter")]
        public float footstepSFXFrequency = 1f;
        [Tooltip("Amount of footstep sounds played when moving one meter while sprinting")]
        public float footstepSFXFrequencyWhileSprinting = 1f;
        [Tooltip("Sound played for footsteps")]
        public AudioClip footstepSFX;
        [Tooltip("Sound played when jumping")] 
        public AudioClip jumpSFX;
        [Tooltip("Sound played when landing")] 
        public AudioClip landSFX;
        
        [Header("Auto Injected")]
        [SerializeField] private PlayerInputHandler inputHandler;
        [SerializeField] private CharacterController controller;        
        
        public UnityAction<bool> OnStanceChanged;
        
        private const float JumpGroundingPreventionTime = 0.2f;
        private const float GroundCheckDistanceInAir = 0.07f;

        private Vector3 groundNormal;
        private Vector3 characterVelocity;

        private float lastTimeJumped = 0f;
        private float cameraVerticalAngle = 0f;
        private float footstepDistanceCounter;
        private float targetCharacterHeight;

        public Vector3 CharacterVelocity { get; private set; }
        public bool IsGrounded { get; private set; }
        public bool HasJumpedThisFrame { get; private set; }
        private bool IsCrouching { get; set; }

        private float RotationMultiplier => 1f;
        #endregion

        private void Start()
        {
            // Fetch components on the same gameObject
            inputHandler = GetComponent<PlayerInputHandler>();
            controller = GetComponent<CharacterController>();

            controller.enableOverlapRecovery = true;

            // Force the crouch state to false when starting
            SetCrouchingState(false, true);
            UpdateCharacterHeight(true);
        }

        private void Update()
        {
            HasJumpedThisFrame = false;

            bool wasGrounded = IsGrounded;
            GroundCheck();

            // Landing
            if (IsGrounded && !wasGrounded)
            {
                // Land SFX
                audioSource.PlayOneShot(landSFX);
            }

            // Crouching
            if (inputHandler.GetCrouchInputDown())
            {
                SetCrouchingState(!IsCrouching, false);
            }

            UpdateCharacterHeight(false);

            HandleCharacterMovement();
        }

        private void GroundCheck()
        {
            // Make sure that the ground check distance while already in air is very small, to prevent suddenly snapping to ground
            float chosenGroundCheckDistance = IsGrounded ? (controller.skinWidth + groundCheckDistance) : GroundCheckDistanceInAir;

            // Reset values before the ground check
            IsGrounded = false;
            groundNormal = Vector3.up;

            // Only try to detect ground if it's been a short amount of time since last jump; otherwise we may snap to the ground instantly after we try jumping
            if (Time.time >= lastTimeJumped + JumpGroundingPreventionTime)
            {
                // if we're grounded, collect info about the ground normal with a downward capsule cast representing our character capsule
                if (Physics.CapsuleCast(GetCapsuleBottomHemisphere(), 
                                        GetCapsuleTopHemisphere(controller.height),
                                              controller.radius, 
                                       Vector3.down, 
                                               out RaycastHit hit, 
                                               chosenGroundCheckDistance, 
                                           groundCheckLayers,
                                               QueryTriggerInteraction.Ignore))
                {
                    // storing the upward direction for the surface found
                    groundNormal = hit.normal;

                    // Only consider this a valid ground hit if the ground normal goes in the same direction as the character up
                    // and if the slope angle is lower than the character controller's limit
                    if (Vector3.Dot(hit.normal, transform.up) > 0f && IsNormalUnderSlopeLimit(groundNormal))
                    {
                        IsGrounded = true;

                        // Handle snapping to the ground
                        if (hit.distance > controller.skinWidth)
                        {
                            controller.Move(Vector3.down * hit.distance);
                        }
                    }
                }
            }
        }

        private void HandleCharacterMovement()
        {
            // Horizontal character rotation
            {
                // Rotate the transform with the input speed around its local Y axis
                transform.Rotate(new Vector3(0f, (inputHandler.GetLookInputsHorizontal() * rotationSpeed * RotationMultiplier), 0f),
                              Space.Self);
            }

            // Vertical camera rotation
            {
                // Add vertical inputs to the camera's vertical angle
                cameraVerticalAngle += inputHandler.GetLookInputsVertical() * rotationSpeed * RotationMultiplier;
                // Limit the camera's vertical angle to min/max
                cameraVerticalAngle = Mathf.Clamp(cameraVerticalAngle, -89f, 89f);
                // Apply the vertical angle as a local rotation to the camera transform along its right axis (makes it pivot up and down)
                playerCamera.transform.localEulerAngles = new Vector3(cameraVerticalAngle, 0, 0);
            }

            // Character movement handling
            bool isSprinting = inputHandler.GetSprintInputHeld();
            {
                if (isSprinting)
                {
                    isSprinting = SetCrouchingState(false, false);
                }

                float speedModifier = isSprinting ? sprintSpeedModifier : 1f;
                // Converts move input to a worldspace vector based on our character's transform orientation
                Vector3 worldspaceMoveInput = transform.TransformVector(inputHandler.GetMoveInput());

                // Handle grounded movement
                if (IsGrounded)
                {
                    // Calculate the desired velocity from inputs, max speed, and current slope
                    Vector3 targetVelocity = worldspaceMoveInput * (maxSpeedOnGround * speedModifier);
                    // Reduce speed if crouching by crouch speed ratio
                    if (IsCrouching)
                    {
                        targetVelocity *= maxSpeedCrouchedRatio;
                    }
                    targetVelocity = GetDirectionReorientedOnSlope(targetVelocity.normalized, groundNormal) * targetVelocity.magnitude;

                    // Smoothly interpolate between our current velocity and the target velocity based on acceleration speed
                    CharacterVelocity = Vector3.Lerp(CharacterVelocity, targetVelocity, movementSharpnessOnGround * Time.deltaTime);

                    // Jumping
                    if (IsGrounded && inputHandler.GetJumpInputDown())
                    {
                        // Force the crouch state to false
                        if (SetCrouchingState(false, false))
                        {
                            // Start by canceling out the vertical component of our velocity
                            CharacterVelocity = new Vector3(CharacterVelocity.x, 0f, CharacterVelocity.z);
                            // Then, add the jumpSpeed value upwards
                            CharacterVelocity += Vector3.up * jumpForce;
                            // Play sound
                            audioSource.PlayOneShot(jumpSFX);
                            // Remember last time we jumped because we need to prevent snapping to ground for a short time
                            lastTimeJumped = Time.time;
                            HasJumpedThisFrame = true;
                            // Force grounding to false
                            IsGrounded = false;
                            groundNormal = Vector3.up;
                        }
                    }

                    // Footsteps sound
                    float chosenFootstepSFXFrequency = (isSprinting ? footstepSFXFrequencyWhileSprinting : footstepSFXFrequency);

                    if (footstepDistanceCounter >= 1f / chosenFootstepSFXFrequency)
                    {
                        footstepDistanceCounter = 0f;
                        audioSource.PlayOneShot(footstepSFX);
                    }

                    // Keep track of distance traveled for footsteps sound
                    footstepDistanceCounter += CharacterVelocity.magnitude * Time.deltaTime;
                }
                // Handle air movement
                else
                {
                    // Add air acceleration
                    CharacterVelocity += worldspaceMoveInput * (accelerationSpeedInAir * Time.deltaTime);

                    // Limit air speed to a maximum, but only horizontally
                    float verticalVelocity = CharacterVelocity.y;
                    Vector3 horizontalVelocity = Vector3.ProjectOnPlane(CharacterVelocity, Vector3.up);
                    horizontalVelocity = Vector3.ClampMagnitude(horizontalVelocity, maxSpeedInAir * speedModifier);
                    CharacterVelocity = horizontalVelocity + (Vector3.up * verticalVelocity);

                    // Apply the gravity to the velocity
                    CharacterVelocity += Vector3.down * (gravityDownForce * Time.deltaTime);
                }
            }

            // Apply the final calculated velocity value as a character movement
            Vector3 capsuleBottomBeforeMove = GetCapsuleBottomHemisphere();
            Vector3 capsuleTopBeforeMove = GetCapsuleTopHemisphere(controller.height);
            controller.Move(CharacterVelocity * Time.deltaTime);

            if (Physics.CapsuleCast(capsuleBottomBeforeMove,
                                    capsuleTopBeforeMove, 
                                          controller.radius,
                                   CharacterVelocity.normalized, 
                                          out RaycastHit hit, 
                                CharacterVelocity.magnitude * Time.deltaTime,
                                    -1,
                                           QueryTriggerInteraction.Ignore))
            {
                CharacterVelocity = Vector3.ProjectOnPlane(CharacterVelocity, hit.normal);
            }
        }

        /// <returns>True if the slope angle represented by the given normal is under the slope angle limit of the character controller</returns>
        bool IsNormalUnderSlopeLimit(Vector3 normal)
        {
            return Vector3.Angle(transform.up, normal) <= controller.slopeLimit;
        }

        // Gets the center point of the bottom hemisphere of the character controller capsule    
        Vector3 GetCapsuleBottomHemisphere()
        {
            return transform.position + (transform.up * controller.radius);
        }

        // Gets the center point of the top hemisphere of the character controller capsule    
        Vector3 GetCapsuleTopHemisphere(float atHeight)
        {
            return transform.position + (transform.up * (atHeight - controller.radius));
        }

        // Gets a reoriented direction that is tangent to a given slope
        private Vector3 GetDirectionReorientedOnSlope(Vector3 direction, Vector3 slopeNormal)
        {
            Vector3 directionRight = Vector3.Cross(direction, transform.up);
            return Vector3.Cross(slopeNormal, directionRight).normalized;
        }

        private void UpdateCharacterHeight(bool force)
        {
            // Update height instantly
            if (force)
            {
                controller.height = targetCharacterHeight;
                controller.center = Vector3.up * (controller.height * 0.5f);
                playerCamera.transform.localPosition = Vector3.up * (targetCharacterHeight * cameraHeightRatio);
            }
            // Update smooth height
            else if (controller.height != targetCharacterHeight)
            {
                // resize the capsule and adjust camera position
                controller.height = Mathf.Lerp(controller.height, targetCharacterHeight, crouchingSharpness * Time.deltaTime);
                controller.center = Vector3.up * (controller.height * 0.5f);
                playerCamera.transform.localPosition = Vector3.Lerp(playerCamera.transform.localPosition,
                                                                    Vector3.up * (targetCharacterHeight * cameraHeightRatio), 
                                                                     crouchingSharpness * Time.deltaTime);
            }
        }
        
        /// <returns>False if there was an obstruction</returns>
        private bool SetCrouchingState(bool crouched, bool ignoreObstructions)
        {
            // Set appropriate heights
            if (crouched)
            {
                targetCharacterHeight = capsuleHeightCrouching;
            }
            else
            {
                // Detect obstructions
                if (!ignoreObstructions)
                {
                    Collider[] standingOverlaps = Physics.OverlapCapsule(GetCapsuleBottomHemisphere(),
                                                                         GetCapsuleTopHemisphere(capsuleHeightStanding),
                                                                               controller.radius,
                                                                         -1,
                                                                         QueryTriggerInteraction.Ignore);
                    foreach (Collider c in standingOverlaps)
                    {
                        if (c != controller)
                        {
                            return false;
                        }
                    }
                }

                targetCharacterHeight = capsuleHeightStanding;
            }

            OnStanceChanged?.Invoke(crouched);

            IsCrouching = crouched;
            return true;
        }
    }
}