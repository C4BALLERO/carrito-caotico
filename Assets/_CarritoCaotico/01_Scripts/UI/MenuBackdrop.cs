using UnityEngine;

namespace CarritoCaotico
{
    /// <summary>
    /// Fondo vivo del menú principal: mientras se está en el menú, la cámara recorre despacio el
    /// supermercado desde lo alto (con los compradores y el carrito a la vista) en lugar de un
    /// fondo plano. Al empezar la partida devuelve el control a <see cref="CameraController"/>.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class MenuBackdrop : MonoBehaviour
    {
        [SerializeField] GameManager gameManager;
        [SerializeField] Vector3 center = new Vector3(0f, 0.8f, 0f);
        [Tooltip("Radios de la vuelta (dentro de las paredes).")]
        [SerializeField] Vector2 radius = new Vector2(11f, 15f);
        [SerializeField] float height = 7f;
        [Tooltip("Grados por segundo de la vuelta.")]
        [SerializeField] float orbitSpeed = 3f;

        float angle = 200f;

        public bool IsActive => gameManager != null && gameManager.State == GameState.Menu && !gameManager.IsAwaitingNetwork;

        void LateUpdate()
        {
            if (!IsActive) return;
            angle = Mathf.Repeat(angle + orbitSpeed * Time.unscaledDeltaTime, 360f);
            float rad = angle * Mathf.Deg2Rad;
            Vector3 position = center + new Vector3(Mathf.Sin(rad) * radius.x, height + Mathf.Sin(rad * 2f) * 0.6f, Mathf.Cos(rad) * radius.y);
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(center - position, Vector3.up));
        }

        /// <summary>Configuración usada por el generador de escena.</summary>
        public void Configure(GameManager manager, float halfWidth, float halfLength)
        {
            gameManager = manager;
            radius = new Vector2(halfWidth * 0.62f, halfLength * 0.62f);
        }
    }
}
