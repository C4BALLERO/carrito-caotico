using UnityEngine;

namespace CarritoCaotico
{
    /// <summary>
    /// Obstáculo que va y viene entre dos puntos (reponedor, fantasma). Es cinemático: empuja el
    /// carrito y los productos que encuentra. Se detiene unos instantes en cada extremo.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class MovingObstacle : MonoBehaviour
    {
        [SerializeField] Vector3 pointA;
        [SerializeField] Vector3 pointB;
        [SerializeField, Min(0.1f)] float speed = 1.6f;
        [SerializeField, Min(0f)] float pauseAtEnds = 1.2f;
        [Tooltip("Altura de flotación (fantasmas). 0 = camina por el suelo.")]
        [SerializeField, Min(0f)] float bobHeight;
        [SerializeField] float bobSpeed = 2f;

        Rigidbody body;
        Vector3 from;
        Vector3 to;
        float pauseTimer;
        float baseY;

        public Vector3 PointA => pointA;
        public Vector3 PointB => pointB;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            baseY = transform.position.y;
            from = pointA;
            to = pointB;
        }

        /// <summary>Configuración usada por el generador de escena.</summary>
        public void Configure(Vector3 a, Vector3 b, float walkSpeed, float bob)
        {
            pointA = a;
            pointB = b;
            speed = walkSpeed;
            bobHeight = bob;
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            Vector3 position = body.position;

            if (pauseTimer > 0f)
            {
                pauseTimer -= dt;
            }
            else
            {
                Vector3 target = new Vector3(to.x, position.y, to.z);
                Vector3 next = Vector3.MoveTowards(position, target, speed * dt);
                Vector3 flat = target - position;
                flat.y = 0f;
                if (flat.sqrMagnitude > 0.0001f)
                    body.MoveRotation(Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(flat.normalized), 360f * dt));

                position = next;
                if ((new Vector3(position.x, 0f, position.z) - new Vector3(to.x, 0f, to.z)).sqrMagnitude < 0.0004f)
                {
                    (from, to) = (to, from);
                    pauseTimer = pauseAtEnds;
                }
            }

            if (bobHeight > 0f)
                position.y = baseY + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            body.MovePosition(position);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(pointA, pointB);
        }
    }
}
