using System.Collections.Generic;
using UnityEngine;

namespace CarritoCaotico
{
    /// <summary>
    /// Comprador rival: recorre los pasillos siguiendo la red de waypoints del RivalManager, se
    /// detiene frente a un producto, lo alcanza con la mano, lo deja en su carrito y, cuando lo
    /// llena, se va. Acelera y frena de forma progresiva, se gira antes de echar a andar si tiene
    /// que dar media vuelta, guarda distancia con otros rivales y cede el paso al carrito de los
    /// jugadores (si el atasco dura, se aparta a un lado para pasar). Es cinemático.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class RivalShopper : MonoBehaviour, IShopperBrain
    {
        enum Phase { Choosing, Walking, Facing, Grabbing, Browsing, Leaving, Gone }

        [SerializeField] Transform basketPoint;
        [Tooltip("Esqueleto: coge los productos con la mano del lado del producto (vacío: van directos a la canasta).")]
        [SerializeField, Optional] ShopperRig rig;
        [Tooltip("Manos de un modelo con clips (KayKit), que no tiene ShopperRig.")]
        [SerializeField, Optional] Transform leftHand;
        [SerializeField, Optional] Transform rightHand;
        [SerializeField] Renderer[] tintRenderers;
        [Tooltip("Grados por segundo al girar.")]
        [SerializeField] float turnSpeed = 240f;
        [SerializeField] float arriveDistance = 0.15f;
        [Tooltip("Aceleración y frenada (m/s²).")]
        [SerializeField] float acceleration = 2.2f;
        [SerializeField] float deceleration = 3.2f;
        [Tooltip("Si el siguiente punto queda más girado que esto, se gira parado antes de andar.")]
        [SerializeField] float turnInPlaceAngle = 60f;
        [Tooltip("Distancia a la que espera a otro comprador o al carrito de los jugadores.")]
        [SerializeField] float personalSpace = 1.1f;

        readonly List<Vector3> path = new List<Vector3>();
        RivalManager manager;
        Rigidbody body;
        Product target;
        Product held;
        Phase phase = Phase.Gone;
        int pathIndex;
        float timer;
        float speed;
        float currentSpeed;
        float grabSeconds;
        float blockedTime;
        float lateralOffset;
        float startledUntil;
        int capacity;
        int carried;
        bool tookThisReach;
        bool stowedThisReach;
        Vector3 reachPoint;
        MaterialPropertyBlock block;

        // Cliente LAN: estado que llega del anfitrión.
        bool replica;
        ShopperAction replicaAction;
        float replicaProgress;

        public int Carried => carried;
        /// <summary>Número de rival para la red.</summary>
        public int NetId { get; set; }
        /// <summary>Qué modelo de comprador es (para que el otro PC use el mismo).</summary>
        public int Variant { get; set; }
        public Color TintColor { get; private set; } = Color.gray;
        public Transform BasketPoint => basketPoint != null ? basketPoint : transform;
        public bool IsLeaving => phase == Phase.Leaving;
        public Product Target => target;
        public float CurrentSpeed => currentSpeed;
        /// <summary>Segundos seguidos esperando a que se despeje el camino.</summary>
        public float BlockedTime => blockedTime;

        // ---------- IShopperBrain (animación) ----------

        public ShopperAction Action
        {
            get
            {
                if (replica) return replicaAction;
                if (Time.time < startledUntil) return ShopperAction.Startled;
                switch (phase)
                {
                    case Phase.Grabbing: return ShopperAction.Reaching;
                    case Phase.Facing:
                    case Phase.Browsing: return ShopperAction.Browsing;
                    default: return ShopperAction.None;
                }
            }
        }

        public Vector3 ReachPoint => target != null ? target.transform.position : reachPoint;

        public float ReachProgress => replica ? replicaProgress
            : phase == Phase.Grabbing && grabSeconds > 0f ? Mathf.Clamp01(1f - timer / grabSeconds) : 0f;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
        }

        public void Init(RivalManager owner, float walkSpeed, float grabTime, int maxProducts, Color color)
        {
            manager = owner;
            // Cada comprador camina a su ritmo.
            speed = walkSpeed * Random.Range(0.88f, 1.12f);
            grabSeconds = Mathf.Max(0.8f, grabTime);
            capacity = Mathf.Max(1, maxProducts);
            carried = 0;
            phase = Phase.Choosing;
            timer = Random.Range(0f, 0.6f);
            Tint(color);
        }

        /// <summary>Cliente LAN: copia visual del rival del anfitrión (no piensa ni se mueve sola).</summary>
        public void InitReplica(Color color)
        {
            manager = null;
            phase = Phase.Gone;
            replica = true;
            Tint(color);
        }

        /// <summary>Cliente LAN: qué está haciendo el rival en el anfitrión.</summary>
        public void SetReplicaAction(ShopperAction action, Vector3 point, float progress)
        {
            replicaAction = action;
            reachPoint = point;
            replicaProgress = progress;
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
                    Brake(dt);
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
                        LoseTarget();
                        break;
                    }
                    if (FollowPath(dt))
                    {
                        phase = Phase.Facing;
                        timer = 0.6f;
                    }
                    break;

                case Phase.Facing:
                    // Se para y mira la estantería antes de alargar la mano.
                    Brake(dt);
                    if (!manager.IsAvailable(target))
                    {
                        LoseTarget();
                        break;
                    }
                    timer -= dt;
                    if (timer <= 0f && currentSpeed < 0.05f)
                    {
                        phase = Phase.Grabbing;
                        timer = grabSeconds;
                        tookThisReach = false;
                        stowedThisReach = false;
                        reachPoint = target.transform.position;
                    }
                    break;

                case Phase.Grabbing:
                    Brake(dt);
                    if (!tookThisReach && !manager.IsAvailable(target))
                    {
                        LoseTarget();
                        break;
                    }
                    timer -= dt;
                    float progress = ReachProgress;
                    // La mano llega al producto a mitad del gesto: ahí lo coge.
                    if (!tookThisReach && progress >= 0.55f)
                    {
                        tookThisReach = true;
                        reachPoint = target.transform.position;
                        Transform hand = ReachHand(target.transform.position);
                        if (manager.TakeProduct(this, target, hand))
                        {
                            carried++;
                            held = hand != null ? target : null;
                        }
                        else
                        {
                            LoseTarget();
                            break;
                        }
                    }
                    // Al llegar la mano a la canasta lo suelta dentro.
                    if (tookThisReach && !stowedThisReach && progress >= 0.82f)
                    {
                        stowedThisReach = true;
                        StowHeld();
                    }
                    if (timer <= 0f)
                    {
                        if (!stowedThisReach) StowHeld();
                        target = null;
                        phase = Phase.Browsing;
                        timer = Random.Range(1.5f, 3.5f);
                    }
                    break;

                case Phase.Browsing:
                    Brake(dt);
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

        /// <summary>Otro le quitó el producto: se sorprende y busca otro.</summary>
        void LoseTarget()
        {
            if (target != null) manager.Release(target);
            target = null;
            phase = Phase.Choosing;
            timer = 0.8f;
            startledUntil = Time.time + 0.8f;
        }

        /// <summary>La mano del lado del producto (la misma que usa la animación).</summary>
        Transform ReachHand(Vector3 point)
        {
            Transform left = rig != null ? rig.leftHand : leftHand;
            Transform right = rig != null ? rig.rightHand : rightHand;
            if (left == null || right == null) return null;
            Vector3 toTarget = Vector3.ProjectOnPlane(point - transform.position, Vector3.up);
            return Vector3.SignedAngle(transform.forward, toTarget, Vector3.up) >= 0f ? right : left;
        }

        void StowHeld()
        {
            if (held == null) return;
            int index = carried - 1;
            held.transform.SetParent(BasketPoint, true);
            held.transform.localPosition = new Vector3(index % 2 == 0 ? -0.12f : 0.12f, 0.05f + (index / 2) * 0.22f, 0f);
            held = null;
        }

        void Brake(float dt)
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, deceleration * dt);
            if (currentSpeed > 0f) body.MovePosition(body.position + transform.forward * (currentSpeed * dt));
        }

        void SetPath(List<Vector3> points)
        {
            path.Clear();
            path.AddRange(points);
            pathIndex = 0;
            lateralOffset = 0f;
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
            bool last = pathIndex == path.Count - 1;

            if (distance <= arriveDistance || (!last && distance <= arriveDistance + currentSpeed * dt * 2f))
            {
                pathIndex++;
                return pathIndex >= path.Count;
            }

            Vector3 direction = toGoal / distance;
            // Apartado a un lado si lleva rato atascado (cada uno a su derecha, así se cruzan).
            Vector3 side = Vector3.Cross(Vector3.up, direction);
            Vector3 aim = goal + side * lateralOffset - position;
            if (aim.sqrMagnitude > 0.0001f) direction = aim.normalized;

            float angle = Vector3.Angle(transform.forward, direction);
            float targetSpeed = speed;
            // Giro parado si hay que dar media vuelta; más despacio en las curvas.
            if (angle > turnInPlaceAngle) targetSpeed = 0f;
            else targetSpeed *= 1f - Mathf.Clamp01(angle / 90f) * 0.6f;
            // Frena al acercarse al destino final.
            if (last) targetSpeed = Mathf.Min(targetSpeed, Mathf.Max(0.25f, distance * 1.4f));

            if (IsBlocked(direction))
            {
                targetSpeed = 0f;
                blockedTime += dt;
                if (blockedTime > 1.4f) lateralOffset = Mathf.MoveTowards(lateralOffset, 0.55f, dt * 0.8f);
            }
            else
            {
                blockedTime = 0f;
                if (lateralOffset > 0f && Vector3.Angle(direction, toGoal) < 25f) lateralOffset = Mathf.MoveTowards(lateralOffset, 0f, dt * 0.3f);
            }

            float rate = targetSpeed > currentSpeed ? acceleration : deceleration;
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, rate * dt);
            Vector3 step = transform.forward * (currentSpeed * dt);
            // No se pasa del destino.
            if (last && step.magnitude > distance) step = step.normalized * distance;
            body.MovePosition(position + step);
            FaceTowards(position + direction, dt);
            return false;
        }

        /// <summary>true si hay otro comprador o el carrito de los jugadores justo delante.</summary>
        bool IsBlocked(Vector3 direction)
        {
            Vector3 position = body.position;
            if (manager.PlayerCart != null && Ahead(manager.PlayerCart.position, position, direction, personalSpace + 0.4f)) return true;
            foreach (RivalShopper other in manager.Rivals)
            {
                if (other == null || other == this || other.phase == Phase.Gone) continue;
                if (Ahead(other.body.position, position, direction, personalSpace)) return true;
            }
            return false;
        }

        static bool Ahead(Vector3 other, Vector3 position, Vector3 direction, float radius)
        {
            Vector3 toOther = other - position;
            toOther.y = 0f;
            float distance = toOther.magnitude;
            return distance < radius && distance > 0.01f && Vector3.Dot(toOther / distance, direction) > 0.55f;
        }

        void FaceTowards(Vector3 point, float dt)
        {
            Vector3 flat = point - body.position;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.0001f) return;
            Quaternion wanted = Quaternion.LookRotation(flat.normalized, Vector3.up);
            // Giro con suavizado: arranca y termina despacio.
            float angle = Quaternion.Angle(body.rotation, wanted);
            float rate = turnSpeed * Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(angle / 45f));
            body.MoveRotation(Quaternion.RotateTowards(body.rotation, wanted, rate * dt));
        }

        void OnDestroy()
        {
            if (manager != null && target != null) manager.Release(target);
        }
    }
}
