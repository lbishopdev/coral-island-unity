using UnityEngine;
using UnityEngine.InputSystem;

namespace TideAndTill
{
    public sealed class GameInput : MonoBehaviour
    {
        private InputAction move;
        private InputAction look;
        private InputAction useTool;
        private InputAction interact;
        private InputAction sprint;

        public Vector2 Move => move.ReadValue<Vector2>();
        public Vector2 Look => look.ReadValue<Vector2>();
        public bool SprintHeld => sprint.IsPressed();
        public bool UsePressed => useTool.WasPressedThisFrame();
        public bool InteractPressed => interact.WasPressedThisFrame();

        private void Awake()
        {
            move = new InputAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/s").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/a").With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/d").With("Right", "<Keyboard>/rightArrow");
            move.AddBinding("<Gamepad>/leftStick");

            look = new InputAction("Look", InputActionType.Value);
            look.AddBinding("<Gamepad>/rightStick");

            useTool = new InputAction("Use tool", InputActionType.Button);
            useTool.AddBinding("<Keyboard>/space");
            useTool.AddBinding("<Mouse>/leftButton");
            useTool.AddBinding("<Gamepad>/buttonSouth");

            interact = new InputAction("Interact", InputActionType.Button);
            interact.AddBinding("<Keyboard>/e");
            interact.AddBinding("<Gamepad>/buttonWest");

            sprint = new InputAction("Sprint", InputActionType.Button);
            sprint.AddBinding("<Keyboard>/leftShift");
            sprint.AddBinding("<Keyboard>/rightShift");
            sprint.AddBinding("<Gamepad>/leftStickPress");
        }

        private void OnEnable()
        {
            move?.Enable();
            look?.Enable();
            useTool?.Enable();
            interact?.Enable();
            sprint?.Enable();
        }

        private void OnDisable()
        {
            move?.Disable();
            look?.Disable();
            useTool?.Disable();
            interact?.Disable();
            sprint?.Disable();
        }

        private void OnDestroy()
        {
            move?.Dispose();
            look?.Dispose();
            useTool?.Dispose();
            interact?.Dispose();
            sprint?.Dispose();
        }

        public int ReadToolHotkey()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return -1;
            if (keyboard.digit1Key.wasPressedThisFrame) return 0;
            if (keyboard.digit2Key.wasPressedThisFrame) return 1;
            if (keyboard.digit3Key.wasPressedThisFrame) return 2;
            if (keyboard.digit4Key.wasPressedThisFrame) return 3;
            if (keyboard.digit5Key.wasPressedThisFrame) return 4;
            return -1;
        }

        public int ReadToolCycle()
        {
            int direction = 0;
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (scroll > 0.1f) direction = -1;
                if (scroll < -0.1f) direction = 1;
            }

            Gamepad pad = Gamepad.current;
            if (pad != null)
            {
                if (pad.rightShoulder.wasPressedThisFrame) direction = 1;
                if (pad.leftShoulder.wasPressedThisFrame) direction = -1;
            }
            return direction;
        }
    }
}
