using System.Collections.Generic;
using UnityEngine;

namespace CarritoCaotico
{
    /// <summary>
    /// Comprador rival: recorre los pasillos siguiendo la red de waypoints del RivalManager,
    /// se detiene frente a un producto, lo mete en su carrito y, cuando lo llena, se va.
    /// Es cinemático: empuja al carrito de los jugadores si se cruza con él.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class RivalShopper : MonoBehaviour
    {
        enum Phase { Choosing, Walking, Grabbing, Browsing, Leaving, Gone }

        [SerializeField] Transform basketPoint;
        [SerializeField] Renderer[] tintRenderers;
        [Tooltip("Un cuerpo por personaje, en el orden de CharacterId; se activa uno según NetId.")]
        [SerializeField] GameObject[] bodies;
        [SerializeField] float turnSpeed = 540f;
        [SerializeField] float arriveDistance = 0.15f;

        readonly List<Vector3> path = new List<Vector3>();
        RivalManager manager;
        Rigidbody body;
        Product target;
        Phase phase = Phase.Gone;
        int pathIndex;
        float timer;
        float speed;
        float grabSeconds;
        int capacity;
        int carried;
        MaterialPropertyBlock block;

        public int Carried => carried;
        /// <summary>Número de rival para la red; también elige su cuerpo.</summary>
        public int NetId { get; set; }
        public Color TintColor { get; private set; } = Color.gray;
        public Transform BasketPoint => basketPoint != null ? basketPoint : transform;
        public bool IsLeaving => phase == Phase.Leaving;
        public Product Target => target;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
        }

        public void Init(RivalManager owner, float walkSpeed, float grabTime, int maxProducts, Color color)
        {
            manager = owner;
            speed = walkSpeed;
            grabSeconds = grabTime;
            capacity = Mathf.Max(1, maxProducts);
            carried = 0;
            phase = Phase.Choosing;
            timer = 0f;
            ShowBody();
            Tint(color);
        }

        /// <summary>Cliente LAN: copia visual del rival del anfitrión (no piensa ni se mueve sola).</summary>
        public void InitReplica(Color color)
        {
            manager = null;
            phase = Phase.Gone;
            ShowBody();
            Tint(color);
        }

        /// <summary>Activa el cuerpo que toca a este rival: el mismo en el anfitrión y en el cliente.</summary>
        void ShowBody()
        {
            if (bodies == null || bodies.Length == 0) return;
            int chosen = Mathf.Abs(NetId) % bodies.Length;
            for (int i = 0; i < bodies.Length; i++)
            {
                if (bodies[i] != null) bodies[i].SetActive(i == chosen);
            }
        }

        void Tint(Color color)
        {
            TintColor = color;
            if (tintRenderers == null) return;
            if (block == null) block = new MaterialPropertyBlock();
            foreach (Renderer r in tintRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor("_BaseColor", color);
                block.SetColor("_Color", color);
                r.SetPropertyBlock(block);
            }
        }

        void FixedUpdate()
        {
            if (manager == null || !manager.Running || phase == Phase.Gone) return;
            float dt = Time.fixedDeltaTime;

            switch (phase)
            {
                case Phase.Choosing:
                    timer -= dt;
                    if (timer > 0f) break;
                    target = manager.ReserveTarget(this);
                    if (target == null)
                    {
                        timer = 1f; // nada disponible ahora: vuelve a mirar en un segundo
                        break;
                    }
                    SetPath(manager.BuildPathToProduct(body.position, target));
                    phase = Phase.Walking;
                    break;

                case Phase.Walking:
                    if (!manager.IsAvailable(target))
                    {
                        manager.Release(target);
                        target = null;
                        phase = Phase.Choosing;
                        break;
                    }
                    if (FollowPath(dt))
                    {
                        phase = Phase.Grabbing;
                        timer = grabSeconds;
                        FaceTowards(target.transform.position, 1f);
                    }
                    break;

                case Phase.Grabbing:
                    if (!manager.IsAvailable(target))
                    {
                        manager.Release(target);
                        target = null;
                        phase = Phase.Choosing;
                        break;
                    }
                    timer -= dt;
                    if (timer <= 0f)
                    {
                        if (manager.TakeProduct(this, target)) carried++;
                        target = null;
                        phase = Phase.Browsing;
                        timer = Random.Range(1.5f, 3.5f);
                    }
                    break;

                case Phase.Browsing:
                    timer -= dt;
                    if (timer > 0f) break;
                    if (carried >= capacity)
                    {
                        SetPath(manager.BuildPathToExit(body.position));
                        phase = Phase.Leaving;
                    }
                    else
                    {
                        phase = Phase.Choosing;
                    }
                    break;

                case Phase.Leaving:
                    if (FollowPath(dt))
                    {
                        phase = Phase.Gone;
                        manager.OnRivalLeft(this);
                    }
                    break;
            }
        }

        void SetPath(List<Vector3> points)
        {
            path.Clear();
            path.AddRange(points);
            pathIndex = 0;
        }

        /// <summary>Avanza por el camino. Devuelve true al llegar al último punto.</summary>
        bool FollowPath(float dt)
        {
            if (pathIndex >= path.Count) return true;

            Vector3 position = body.position;
            Vector3 goal = path[pathIndex];
            goal.y = position.y;
            Vector3 toGoal = goal - position;
            float distance = toGoal.magnitude;

            if (distance <= arriveDistance)
            {
                pathIndex++;
                return pathIndex >= path.Count;
            }

            Vector3 step = toGoal / distance * Mathf.Min(distance, speed * dt);
            body.MovePosition(position + step);
            FaceTowards(goal, dt);
            return false;
        }

        void FaceTowards(Vector3 point, float dt)
        {
            Vector3 flat = point - body.position;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.0001f) return;
            Quaternion wanted = Quaternion.LookRotation(flat.normalized, Vector3.up);
            body.MoveRotation(Quaternion.RotateTowards(body.rotation, wanted, turnSpeed * dt));
        }

        void OnDestroy()
        {
            if (manager != null && target != null) manager.Release(target);
        }
    }
}
