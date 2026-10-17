using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace CarritoCaotico
{
    /// <summary>
    /// Lectura centralizada de controles para los dos jugadores usando el New Input System.
    /// Respeta el <see cref="ControlScheme"/> activo:
    /// - Normal: Empujador WASD + Shift izq. + mouse; Agarrador flechas + Enter + Ctrl der.
    /// - Intercambiado: Empujador flechas + Shift der. + mouse; Agarrador WASD + Espacio + F.
    /// - Invertido: teclas normales con todas las direcciones al revés.
    /// Mandos opcionales: el primero es el Empujador y el segundo el Agarrador
    /// (se intercambian con el esquema Intercambiado).
    /// </summary>
    public static class GameInput
    {
        const float StickDeadZone = 0.2f;

        /// <summary>Esquema de control activo. Lo fija el GameManager al empezar cada nivel.</summary>
        public static ControlScheme Scheme { get; set; } = ControlScheme.Normal;

        static bool Swapped => Scheme == ControlScheme.Intercambiado;
        static float Direction => Scheme == ControlScheme.Invertido ? -1f : 1f;

        static Gamepad PadAt(int index) => Gamepad.all.Count > index ? Gamepad.all[index] : null;
        static Gamepad PusherPad => PadAt(Swapped ? 1 : 0);
        static Gamepad GrabberPad => PadAt(Swapped ? 0 : 1);

        // ---------- Empujador ----------

        /// <summary>Acelerador: +1 adelante, -1 frenar / retroceder.</summary>
        public static float PusherThrottle()
        {
            float value = 0f;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                value += Axis(Swapped ? keyboard.downArrowKey : keyboard.sKey, Swapped ? keyboard.upArrowKey : keyboard.wKey);
            }

            var pad = PusherPad;
            if (pad != null)
            {
                value += pad.rightTrigger.ReadValue() - pad.leftTrigger.ReadValue();
                value += ApplyDeadZone(pad.leftStick.y.ReadValue());
            }

            return Mathf.Clamp(value * Direction, -1f, 1f);
        }

        /// <summary>Giro: -1 izquierda, +1 derecha.</summary>
        public static float PusherSteer()
        {
            float value = 0f;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                value += Axis(Swapped ? keyboard.leftArrowKey : keyboard.aKey, Swapped ? keyboard.rightArrowKey : keyboard.dKey);
            }

            var pad = PusherPad;
            if (pad != null) value += ApplyDeadZone(pad.leftStick.x.ReadValue());

            return Mathf.Clamp(value * Direction, -1f, 1f);
        }

        public static bool PusherDrift()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && (Swapped ? keyboard.rightShiftKey : keyboard.leftShiftKey).isPressed) return true;

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
                value.x += Axis(Swapped ? keyboard.aKey : keyboard.leftArrowKey, Swapped ? keyboard.dKey : keyboard.rightArrowKey);
                value.y += Axis(Swapped ? keyboard.sKey : keyboard.downArrowKey, Swapped ? keyboard.wKey : keyboard.upArrowKey);
            }

            var pad = GrabberPad;
            if (pad != null)
            {
                Vector2 stick = pad.leftStick.ReadValue();
                if (stick.magnitude >= StickDeadZone) value += stick;
            }

            return Vector2.ClampMagnitude(value * Direction, 1f);
        }

        public static bool GrabberGrabPressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                bool pressed = Swapped
                    ? keyboard.spaceKey.wasPressedThisFrame
                    : keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;
                if (pressed) return true;
            }

            var pad = GrabberPad;
            return pad != null && pad.buttonSouth.wasPressedThisFrame;
        }

        public static bool GrabberThrowPressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && (Swapped ? keyboard.fKey : keyboard.rightCtrlKey).wasPressedThisFrame) return true;

            var pad = GrabberPad;
            return pad != null && (pad.rightTrigger.wasPressedThisFrame || pad.buttonWest.wasPressedThisFrame);
        }

        // ---------- General ----------

        public static bool PausePressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.escapeKey.wasPressedThisFrame || keyboard.pKey.wasPressedThisFrame)) return true;
            return AnyPadStartPressed();
        }

        public static bool RestartPressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame) return true;
            return AnyPadStartPressed();
        }

        public static bool NextLevelPressed()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.nKey.wasPressedThisFrame;
        }

        public static bool MenuPressed()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.mKey.wasPressedThisFrame;
        }

        static bool AnyPadStartPressed()
        {
            for (int i = 0; i < Gamepad.all.Count; i++)
            {
                if (Gamepad.all[i].startButton.wasPressedThisFrame) return true;
            }
            return false;
        }

        static float Axis(ButtonControl negative, ButtonControl positive)
        {
            float value = 0f;
            if (negative.isPressed) value -= 1f;
            if (positive.isPressed) value += 1f;
            return value;
        }

        static float ApplyDeadZone(float value) => Mathf.Abs(value) < StickDeadZone ? 0f : value;

        /// <summary>Etiquetas de teclas del Empujador: acelerar, frenar, girar, derrapar.</summary>
        public static string[] PusherKeyLabels(ControlScheme scheme)
        {
            switch (scheme)
            {
                case ControlScheme.Intercambiado: return new[] { "↑", "↓", "←  →", "Shift der." };
                case ControlScheme.Invertido: return new[] { "S", "W", "D  A", "Shift izq." };
                default: return new[] { "W", "S", "A  D", "Shift izq." };
            }
        }

        /// <summary>Etiquetas de teclas del Agarrador: mover, agarrar, lanzar.</summary>
        public static string[] GrabberKeyLabels(ControlScheme scheme)
        {
            switch (scheme)
            {
                case ControlScheme.Intercambiado: return new[] { "WASD", "Espacio", "F" };
                case ControlScheme.Invertido: return new[] { "Flechas", "Enter", "Ctrl der." };
                default: return new[] { "Flechas", "Enter", "Ctrl der." };
            }
        }
    }
}
