using UnityEngine;
using UnityEngine.InputSystem;

namespace CarritoCaotico
{
    /// <summary>
    /// Lectura centralizada de controles para los dos jugadores usando el New Input System.
    /// Teclado compartido: el Empujador usa WASD + Shift izquierdo + mouse,
    /// el Agarrador usa flechas + Enter + Ctrl derecho.
    /// Mandos opcionales: el primer mando es el Empujador y el segundo el Agarrador.
    /// </summary>
    public static class GameInput
    {
        const float StickDeadZone = 0.2f;

        static Gamepad PusherPad => Gamepad.all.Count > 0 ? Gamepad.all[0] : null;
        static Gamepad GrabberPad => Gamepad.all.Count > 1 ? Gamepad.all[1] : null;

        // ---------- Empujador ----------

        /// <summary>Acelerador: +1 adelante, -1 frenar / retroceder.</summary>
        public static float PusherThrottle()
        {
            float value = 0f;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed) value += 1f;
                if (keyboard.sKey.isPressed) value -= 1f;
            }

            var pad = PusherPad;
            if (pad != null)
            {
                value += pad.rightTrigger.ReadValue() - pad.leftTrigger.ReadValue();
                value += ApplyDeadZone(pad.leftStick.y.ReadValue());
            }

            return Mathf.Clamp(value, -1f, 1f);
        }

        /// <summary>Giro: -1 izquierda, +1 derecha.</summary>
        public static float PusherSteer()
        {
            float value = 0f;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed) value -= 1f;
                if (keyboard.dKey.isPressed) value += 1f;
            }

            var pad = PusherPad;
            if (pad != null) value += ApplyDeadZone(pad.leftStick.x.ReadValue());

            return Mathf.Clamp(value, -1f, 1f);
        }

        public static bool PusherDrift()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.leftShiftKey.isPressed) return true;

            var pad = PusherPad;
            return pad != null && (pad.rightShoulder.isPressed || pad.buttonSouth.isPressed);
        }

        /// <summary>Movimiento del mouse en píxeles durante este frame.</summary>
        public static Vector2 MouseLookDelta()
        {
            var mouse = Mouse.current;
            return mouse != null ? mouse.delta.ReadValue() : Vector2.zero;
        }

        /// <summary>Stick derecho del mando del Empujador (-1..1), para rotar la cámara.</summary>
        public static Vector2 PadLook()
        {
            var pad = PusherPad;
            if (pad == null) return Vector2.zero;
            Vector2 stick = pad.rightStick.ReadValue();
            return stick.magnitude < StickDeadZone ? Vector2.zero : stick;
        }

        // ---------- Agarrador ----------

        /// <summary>Movimiento del brazo: x lateral, y adelante / atrás.</summary>
        public static Vector2 GrabberMove()
        {
            Vector2 value = Vector2.zero;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.leftArrowKey.isPressed) value.x -= 1f;
                if (keyboard.rightArrowKey.isPressed) value.x += 1f;
                if (keyboard.downArrowKey.isPressed) value.y -= 1f;
                if (keyboard.upArrowKey.isPressed) value.y += 1f;
            }

            var pad = GrabberPad;
            if (pad != null)
            {
                Vector2 stick = pad.leftStick.ReadValue();
                if (stick.magnitude >= StickDeadZone) value += stick;
            }

            return Vector2.ClampMagnitude(value, 1f);
        }

        public static bool GrabberGrabPressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
                return true;

            var pad = GrabberPad;
            return pad != null && pad.buttonSouth.wasPressedThisFrame;
        }

        public static bool GrabberThrowPressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rightCtrlKey.wasPressedThisFrame) return true;

            var pad = GrabberPad;
            return pad != null && (pad.rightTrigger.wasPressedThisFrame || pad.buttonWest.wasPressedThisFrame);
        }

        // ---------- General ----------

        public static bool RestartPressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame) return true;

            for (int i = 0; i < Gamepad.all.Count; i++)
            {
                if (Gamepad.all[i].startButton.wasPressedThisFrame) return true;
            }
            return false;
        }

        public static bool ToggleCursorPressed()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
        }

        static float ApplyDeadZone(float value) => Mathf.Abs(value) < StickDeadZone ? 0f : value;
    }
}
