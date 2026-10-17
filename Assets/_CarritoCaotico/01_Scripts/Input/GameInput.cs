using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace CarritoCaotico
{
    /// <summary>Qué jugador usa el mando cuando solo hay uno conectado.</summary>
    public enum PadAssignment
    {
        /// <summary>El mando lo usa el Empujador (con dos mandos, uno para cada jugador).</summary>
        Automatico = 0,
        Empujador = 1,
        Agarrador = 2
    }

    /// <summary>
    /// Lectura centralizada de controles para los dos jugadores usando el New Input System.
    /// Respeta el <see cref="ControlScheme"/> activo:
    /// - Normal: Empujador WASD + Shift izq. + mouse; Agarrador flechas + Enter + Ctrl der.
    /// - Intercambiado: Empujador flechas + Shift der. + mouse; Agarrador WASD + Espacio + F.
    /// - Invertido: teclas normales con todas las direcciones al revés.
    /// Mandos: con dos mandos, el primero es del Empujador y el segundo del Agarrador. Con uno
    /// solo, <see cref="PadMode"/> decide quién lo usa y el otro jugador juega con teclado.
    /// El esquema Intercambiado también intercambia los mandos.
    /// </summary>
    public static class GameInput
    {
        const float StickDeadZone = 0.2f;

        /// <summary>Esquema de control activo. Lo fija el GameManager al empezar cada nivel.</summary>
        public static ControlScheme Scheme { get; set; } = ControlScheme.Normal;

        static bool Swapped => Scheme == ControlScheme.Intercambiado;
        static float Direction => Scheme == ControlScheme.Invertido ? -1f : 1f;

        const string PadModeKey = "CarritoCaotico.Mando";
        static bool padModeLoaded;
        static PadAssignment padMode;

        /// <summary>Quién usa el mando cuando solo hay uno. Se guarda en PlayerPrefs.</summary>
        public static PadAssignment PadMode
        {
            get
            {
                if (!padModeLoaded)
                {
                    padMode = (PadAssignment)PlayerPrefs.GetInt(PadModeKey, (int)PadAssignment.Automatico);
                    padModeLoaded = true;
                }
                return padMode;
            }
            set
            {
                padMode = value;
                padModeLoaded = true;
                PlayerPrefs.SetInt(PadModeKey, (int)value);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Se dispara al conectar o desconectar un mando.</summary>
        public static event Action DevicesChanged;

        static bool deviceHookInstalled;

        /// <summary>Activa el aviso de cambios de dispositivo (se llama desde la interfaz).</summary>
        public static void EnsureDeviceHook()
        {
            if (deviceHookInstalled) return;
            deviceHookInstalled = true;
            InputSystem.onDeviceChange += (device, change) =>
            {
                if (device is Gamepad && (change == InputDeviceChange.Added || change == InputDeviceChange.Removed
                                          || change == InputDeviceChange.Disconnected || change == InputDeviceChange.Reconnected))
                    DevicesChanged?.Invoke();
            };
        }

        public static int GamepadCount => Gamepad.all.Count;
        public static string GamepadName => Gamepad.all.Count > 0 ? Gamepad.all[0].displayName : "";

        static Gamepad PusherPad => PadForRole(true);
        static Gamepad GrabberPad => PadForRole(false);

        public static bool PusherUsesGamepad => PusherPad != null;
        public static bool GrabberUsesGamepad => GrabberPad != null;

        /// <summary>Mando que corresponde a un jugador según los mandos conectados, PadMode y el esquema.</summary>
        static Gamepad PadForRole(bool pusher)
        {
            var pads = Gamepad.all;
            if (pads.Count == 0) return null;
            if (pads.Count >= 2)
            {
                bool firstIsPusher = !Swapped;
                return pusher == firstIsPusher ? pads[0] : pads[1];
            }
            // Un solo mando: lo usa quien diga PadMode (el esquema Intercambiado lo cambia de jugador).
            bool ownerIsPusher = PadMode != PadAssignment.Agarrador;
            if (Swapped) ownerIsPusher = !ownerIsPusher;
            return pusher == ownerIsPusher ? pads[0] : null;
        }

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

        /// <summary>Texto que explica qué usa cada jugador.</summary>
        public static string DevicesSummary()
        {
            if (GamepadCount == 0) return "Sin mando: los dos juegan con el teclado.";
            if (GamepadCount >= 2) return "Dos mandos: uno para cada jugador.";
            string owner = PusherUsesGamepad ? "Empujador" : "Agarrador";
            string other = PusherUsesGamepad ? "Agarrador" : "Empujador";
            return $"Mando para el {owner} · teclado para el {other}.";
        }

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

        /// <summary>Tutorial: saltar el paso actual (Tab o Select / View en el mando).</summary>
        public static bool TutorialSkipPressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.tabKey.wasPressedThisFrame) return true;
            for (int i = 0; i < Gamepad.all.Count; i++)
            {
                if (Gamepad.all[i].selectButton.wasPressedThisFrame) return true;
            }
            return false;
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

        /// <summary>Control de la cámara: mouse o stick derecho.</summary>
        public static string CameraLabel => PusherUsesGamepad ? "Stick der." : "Mouse";

        /// <summary>
        /// Etiquetas del Empujador: acelerar, frenar, girar, derrapar. Muestra botones del mando
        /// si el Empujador usa uno.
        /// </summary>
        public static string[] PusherKeyLabels(ControlScheme scheme)
        {
            if (PusherUsesGamepad)
                return scheme == ControlScheme.Invertido
                    ? new[] { "LT", "RT", "Stick izq.", "RB" }
                    : new[] { "RT", "LT", "Stick izq.", "RB" };
            return PusherKeyboardLabels(scheme);
        }

        /// <summary>Etiquetas de teclado del Empujador para un esquema.</summary>
        public static string[] PusherKeyboardLabels(ControlScheme scheme)
        {
            switch (scheme)
            {
                case ControlScheme.Intercambiado: return new[] { "↑", "↓", "←  →", "Shift der." };
                case ControlScheme.Invertido: return new[] { "S", "W", "D  A", "Shift izq." };
                default: return new[] { "W", "S", "A  D", "Shift izq." };
            }
        }

        /// <summary>Etiquetas del Agarrador: mover, agarrar, lanzar (botones del mando si usa uno).</summary>
        public static string[] GrabberKeyLabels(ControlScheme scheme)
        {
            if (GrabberUsesGamepad) return new[] { "Stick izq.", "A", "RT" };
            return GrabberKeyboardLabels(scheme);
        }

        /// <summary>Etiquetas de teclado del Agarrador para un esquema.</summary>
        public static string[] GrabberKeyboardLabels(ControlScheme scheme)
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
