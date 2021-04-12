using UnityEngine;

namespace MinimalFPC
{
    /// <summary>
    /// Manages the pause state of the game
    /// </summary>
    public class PauseManager : MonoBehaviour
    {
        public bool IsPaused { get; private set; }

        private void Update()
        {
            UpdateInput();
        }

        private void UpdateInput()
        {
            if (Input.GetButtonDown(InputConstants.ButtonNamePauseMenu))
            {
                if (!IsPaused)
                {
                    IsPaused = true;
                    ShowCursor();
                }
                else
                {
                    IsPaused = false;
                    HideCursor();
                }
            }
        }

        private void ShowCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        
        public void HideCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}