using System;
using UnityEngine;

namespace CarritoCaotico
{
    /// <summary>
    /// Cuenta regresiva de la partida. Solo notifica a la interfaz cuando cambia el segundo
    /// mostrado, para no generar trabajo cada frame.
    /// </summary>
    public class GameTimer : MonoBehaviour
    {
        [SerializeField, Min(1f)] float duration = 120f;

        float remaining;
        bool running;
        int lastNotifiedSecond = -1;

        public float Duration => duration;
        public float Remaining => remaining;
        public float Elapsed => duration - remaining;
        public bool IsRunning => running;

        /// <summary>Segundos restantes redondeados hacia arriba.</summary>
        public event Action<int> SecondsChanged;
        public event Action Expired;

        void Awake()
        {
            remaining = duration;
        }

        public void ResetTimer()
        {
            running = false;
            remaining = duration;
            Notify(true);
        }

        public void StartTimer()
        {
            if (remaining <= 0f) return;
            running = true;
        }

        public void StopTimer()
        {
            running = false;
        }

        /// <summary>Ajusta el tiempo restante (pruebas y depuración). Nunca negativo.</summary>
        public void SetRemaining(float seconds)
        {
            remaining = Mathf.Clamp(seconds, 0f, duration);
            Notify(false);
        }

        void Update()
        {
            if (!running) return;

            remaining = Mathf.Max(0f, remaining - Time.deltaTime);
            Notify(false);

            if (remaining <= 0f)
            {
                running = false;
                Expired?.Invoke();
            }
        }

        void Notify(bool force)
        {
            int seconds = Mathf.CeilToInt(remaining);
            if (!force && seconds == lastNotifiedSecond) return;
            lastNotifiedSecond = seconds;
            SecondsChanged?.Invoke(seconds);
        }

        /// <summary>Formato m:ss.</summary>
        public static string Format(float seconds)
        {
            int total = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{total / 60}:{total % 60:00}";
        }
    }
}
