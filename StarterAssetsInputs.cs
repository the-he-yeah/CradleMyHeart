using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
    public class StarterAssetsInputs : MonoBehaviour
    {
        // ... existing code ...

        private bool _crouchWasPressed = false;

#if ENABLE_INPUT_SYSTEM
        // ... existing OnMove, OnLook, OnJump, OnSprint methods ...

        public void OnCrouch(InputValue value)
        {
            // Only toggle when the button is pressed down, not released
            if (value.isPressed && !_crouchWasPressed)
            {
                crouch = !crouch;
                _crouchWasPressed = true;
                Debug.Log("Crouch toggled: " + crouch);
            }
            else if (!value.isPressed)
            {
                _crouchWasPressed = false;
            }
        }
#endif

        // ... existing MoveInput, LookInput, JumpInput, SprintInput methods ...

        public void CrouchInput(bool newCrouchState)
        {
            // This method is kept for compatibility but won't be used directly
            // The toggle logic is handled in OnCrouch
        }

        // ... rest of existing code ...
    }
}
