using UnityEngine;

namespace MinimalFPC
{
    public class PlayerInputHandler : MonoBehaviour
    {
        [Tooltip("Sensitivity multiplier for moving the camera around")]
        public float lookSensitivity = 1f;
        [Tooltip("Additional sensitivity multiplier for WebGL")]
        public float webglLookSensitivityMultiplier = 0.25f;
        [Tooltip("Used to flip the vertical input axis")]
        public bool invertYAxis = false;
        [Tooltip("Used to flip the horizontal input axis")]
        public bool invertXAxis = false;

        [Header("Auto Injected")] 
        [SerializeField] private PauseManager pauseManager;
        
        private void Start()
        {
            pauseManager = FindObjectOfType<PauseManager>();
            pauseManager.HideCursor();
        }
        
        private bool CanProcessInput()
        {
            return !pauseManager.IsPaused;
        }

        public Vector3 GetMoveInput()
        {
            if (CanProcessInput())
            {
                Vector3 move = new Vector3(Input.GetAxisRaw(InputConstants.AxisNameHorizontal), 
                                           0f, 
                                           Input.GetAxisRaw(InputConstants.AxisNameVertical));

                // Constrain move input to a maximum magnitude of 1, otherwise diagonal movement might exceed the max move speed defined
                move = Vector3.ClampMagnitude(move, 1);

                return move;
            }

            return Vector3.zero;
        }

        public float GetLookInputsHorizontal()
        {
            return GetMouseOrStickLookAxis(InputConstants.MouseAxisNameHorizontal, 
                               InputConstants.AxisNameJoystickLookHorizontal);
        }

        public float GetLookInputsVertical()
        {
            return GetMouseOrStickLookAxis(InputConstants.MouseAxisNameVertical,
                               InputConstants.AxisNameJoystickLookVertical);
        }

        public bool GetJumpInputDown()
        {
            if (CanProcessInput())
            {
                return Input.GetButtonDown(InputConstants.ButtonNameJump);
            }

            return false;
        }

        public bool GetJumpInputHeld()
        {
            if (CanProcessInput())
            {
                return Input.GetButton(InputConstants.ButtonNameJump);
            }

            return false;
        }
        
        public bool GetAimInputHeld()
        {
            if (CanProcessInput())
            {
                bool isGamepad = Input.GetAxis(InputConstants.ButtonNameGamepadAim) != 0f;
                bool i = isGamepad
                    ? (Input.GetAxis(InputConstants.ButtonNameGamepadAim) > 0f)
                    : Input.GetButton(InputConstants.ButtonNameAim);
                return i;
            }

            return false;
        }

        public bool GetSprintInputHeld()
        {
            if (CanProcessInput())
            {
                return Input.GetButton(InputConstants.ButtonNameSprint);
            }

            return false;
        }

        public bool GetCrouchInputDown()
        {
            if (CanProcessInput())
            {
                return Input.GetButtonDown(InputConstants.ButtonNameCrouch);
            }

            return false;
        }
        
        private float GetMouseOrStickLookAxis(string mouseInputName, string stickInputName)
        {
            if (CanProcessInput())
            {
                // Check if this look input is coming from the mouse
                bool isGamepad = Input.GetAxis(stickInputName) != 0f;
                float i = isGamepad ? Input.GetAxis(stickInputName) : Input.GetAxisRaw(mouseInputName);

                // Handle inverting vertical input
                if (invertYAxis)
                    i *= -1f;

                // Apply sensitivity multiplier
                i *= lookSensitivity;

                if (isGamepad)
                {
                    // Since mouse input is already deltaTime-dependant, only scale input with frame time if it's coming from sticks
                    i *= Time.deltaTime;
                }
                else
                {
                    // reduce mouse input amount to be equivalent to stick movement
                    i *= 0.01f;
#if UNITY_WEBGL
                // Mouse tends to be even more sensitive in WebGL due to mouse acceleration, so reduce it even more
                i *= webglLookSensitivityMultiplier;
#endif
                }

                return i;
            }

            return 0f;
        }
    }
}