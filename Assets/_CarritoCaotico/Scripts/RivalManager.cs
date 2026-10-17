using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace CarritoCaotico
{
    /// <summary>
    /// Crea y coordina a los compradores rivales. Les da caminos por una red sencilla de
    /// waypoints (centros de pasillo + pasillos transversales), reparte objetivos sin que dos
    /// rivales vayan a por el mismo producto y aplica una regla justa: nunca se llevan una
    /// unidad que el jugador necesite si no quedan suficientes en las baldas.
    /// </summary>
    public class RivalManager : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] RivalShopper rivalPrefab;
        [SerializeField] ShoppingList shoppingList;
        [SerializeField] CartContents cartContents;

        [Header("Red de pasillos")]
        [Tooltip("Coordenada X del centro de cada pasillo.")]
        [SerializeField] float[] aisleCentersX = Array.Empty<float>();
        [Tooltip("Coordenada Z de los pasillos transversales (delante, centro, fondo).")]
        [SerializeField] float[] corridorsZ = Array.Empty<float>();
        [Tooltip("Zona con estantes: |z| entre estos valores es interior de pasillo.")]
        [SerializeField] Vector2 shelfZoneAbsZ = new Vector2(1.5f, 13f);
        [Tooltip("Distancia lateral al centro del pasillo para alcanzar la balda.")]
        [SerializeField] float standOffset = 1.1f;
        [SerializeField] Vector3 entryPoint = new Vector3(-20f, 0f, -16f);
        [SerializeField] Vector3 exitPoint = new Vector3(20f, 0f, -16f);

        [Header("Flujo")]
        [SerializeField] float spawnInterval = 2.5f;
        [SerializeField] float respawnDelay = 6f;
        [SerializeField] Color[] palette =
        {
            new Color32(249, 115, 22, 255), new Color32(13, 148, 136, 255), new Color32(236, 72, 153, 255),
            new Color32(132, 204, 22, 255), new Color32(120, 113, 108, 255), new Color32(234, 179, 8, 255)
        };

        readonly List<Product> products = new List<Product>();
        readonly HashSet<Product> reserved = new HashSet<Product>();
        readonly List<RivalShopper> rivals = new List<RivalShopper>();
        readonly List<float> pendingSpawns = new List<float>();
        LevelConfig config;
        int spawned;

        public bool Running { get; private set; }
        public int ActiveCount => rivals.Count;
        public int TakenCount { get; private set; }
        public int ConfiguredCount => config != null ? config.RivalCount : 0;

        /// <summary>Un rival se llevó un producto.</summary>
        public event Action<Product> ProductTaken;
        /// <summary>Cambió el número de rivales o de productos robados.</summary>
        public event Action Changed;

        /// <summary>Empieza el nivel: crea los rivales de forma escalonada.</summary>
        public void Begin(LevelConfig levelConfig)
        {
            config = levelConfig;
            products.Clear();
            products.AddRange(FindObjectsByType<Product>(FindObjectsInactive.Exclude));
            Running = true;
            pendingSpawns.Clear();
            for (int i = 0; i < config.RivalCount; i++)
                pendingSpawns.Add(Time.time + 1.5f + i * spawnInterval);
            Changed?.Invoke();
        }

        /// <summary>Detiene a los rivales (fin de la partida). Se quedan quietos donde están.</summary>
        public void Stop()
        {
            Running = false;
            pendingSpawns.Clear();
        }

        void Update()
        {
            if (!Running || rivalPrefab == null) return;
            for (int i = pendingSpawns.Count - 1; i >= 0; i--)
            {
                if (Time.time < pendingSpawns[i]) continue;
                pendingSpawns.RemoveAt(i);
                Spawn();
            }
        }

        void Spawn()
        {
            RivalShopper rival = Instantiate(rivalPrefab, entryPoint, Quaternion.LookRotation(Vector3.right));
            rival.name = $"Comprador_Rival_{++spawned}";
            Color color = palette.Length > 0 ? palette[(spawned - 1) % palette.Length] : Color.gray;
            rival.Init(this, config.RivalSpeed, config.RivalGrabSeconds, config.RivalCapacity, color);
            rivals.Add(rival);
            Changed?.Invoke();
        }

        public void OnRivalLeft(RivalShopper rival)
        {
            rivals.Remove(rival);
            Destroy(rival.gameObject);
            if (Running) pendingSpawns.Add(Time.time + respawnDelay);
            Changed?.Invoke();
        }

        // ---------- Objetivos ----------

        public bool IsAvailable(Product product)
        {
            return product != null && product.IsOnShelf && (cartContents == null || !cartContents.Contains(product));
        }

        /// <summary>Elige el producto más cercano permitido; con cierta probabilidad, uno de la lista del jugador.</summary>
        public Product ReserveTarget(RivalShopper rival)
        {
            bool preferList = shoppingList != null && config != null && Random.value < config.RivalListBias;
            Product best = FindNearest(rival.transform.position, preferList);
            if (best == null && preferList) best = FindNearest(rival.transform.position, false);
            if (best != null) reserved.Add(best);
            return best;
        }

        Product FindNearest(Vector3 from, bool onlyListed)
        {
            Product best = null;
            float bestCost = float.MaxValue;
            foreach (Product product in products)
            {
                if (!IsAvailable(product) || reserved.Contains(product)) continue;
                if (onlyListed && shoppingList.GetRequired(product.Type) == 0) continue;
                if (!AllowedByFairness(product)) continue;

                Vector3 p = product.transform.position;
                // Coste aproximado por la red de pasillos (distancia Manhattan) con algo de azar.
                float cost = Mathf.Abs(p.x - from.x) + Mathf.Abs(p.z - from.z) + Random.Range(0f, 4f);
                if (cost < bestCost)
                {
                    bestCost = cost;
                    best = product;
                }
            }
            return best;
        }

        /// <summary>
        /// Regla justa: si el jugador aún necesita unidades de este tipo, el rival solo puede
        /// llevarse una cuando quedan más en las baldas de las que faltan.
        /// </summary>
        public bool AllowedByFairness(Product product)
        {
            if (shoppingList == null) return true;
            int missing = shoppingList.GetMissing(product.Type);
            if (missing <= 0) return true;

            int available = 0;
            foreach (Product other in products)
            {
                if (other.Type == product.Type && IsAvailable(other) && (other == product || !reserved.Contains(other)))
                    available++;
            }
            return available - 1 >= missing;
        }

        public void Release(Product product)
        {
            if (product != null) reserved.Remove(product);
        }

        public bool TakeProduct(RivalShopper rival, Product product)
        {
            reserved.Remove(product);
            if (!IsAvailable(product) || !AllowedByFairness(product)) return false;

            int index = rival.Carried;
            var offset = new Vector3((index % 2 == 0 ? -0.12f : 0.12f), 0.05f + (index / 2) * 0.22f, 0f);
            if (!product.TryTakeByRival(rival.BasketPoint, offset)) return false;

            TakenCount++;
            ProductTaken?.Invoke(product);
            Changed?.Invoke();
            return true;
        }

        // ---------- Caminos ----------

        public List<Vector3> BuildPathToProduct(Vector3 from, Product product)
        {
            Vector3 p = product.transform.position;
            float aisle = NearestAisle(p.x);
            float side = Mathf.Sign(p.x - aisle);
            List<Vector3> path = Route(from, new Vector3(aisle, 0f, p.z));
            path.Add(new Vector3(aisle + side * standOffset, 0f, p.z));
            return path;
        }

        public List<Vector3> BuildPathToExit(Vector3 from)
        {
            List<Vector3> path = Route(from, new Vector3(exitPoint.x, 0f, exitPoint.z));
            return path;
        }

        /// <summary>
        /// Camino por la red: salir al centro del pasillo actual, ir por el pasillo transversal
        /// más conveniente y entrar en el pasillo de destino.
        /// </summary>
        List<Vector3> Route(Vector3 from, Vector3 to)
        {
            var path = new List<Vector3>();
            Vector3 current = new Vector3(from.x, 0f, from.z);

            if (InShelfZone(current.z))
            {
                float aisle = NearestAisle(current.x);
                current = new Vector3(aisle, 0f, current.z);
                path.Add(current);

                // Mismo pasillo: línea recta por el centro.
                if (InShelfZone(to.z) && Mathf.Approximately(NearestAisle(to.x), aisle))
                {
                    path.Add(to);
                    return path;
                }
            }

            float corridor = BestCorridor(current.z, to.z);
            if (InShelfZone(to.z))
            {
                float targetAisle = NearestAisle(to.x);
                path.Add(new Vector3(current.x, 0f, corridor));
                path.Add(new Vector3(targetAisle, 0f, corridor));
                path.Add(new Vector3(targetAisle, 0f, to.z));
                if (!Mathf.Approximately(targetAisle, to.x)) path.Add(to);
            }
            else
            {
                path.Add(new Vector3(current.x, 0f, corridor));
                path.Add(new Vector3(to.x, 0f, corridor));
                if (!Mathf.Approximately(corridor, to.z)) path.Add(to);
            }
            return path;
        }

        bool InShelfZone(float z)
        {
            float abs = Mathf.Abs(z);
            return abs >= shelfZoneAbsZ.x && abs <= shelfZoneAbsZ.y;
        }

        float NearestAisle(float x)
        {
            float best = x;
            float bestDistance = float.MaxValue;
            foreach (float aisle in aisleCentersX)
            {
                float d = Mathf.Abs(aisle - x);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = aisle;
                }
            }
            return best;
        }

        float BestCorridor(float fromZ, float toZ)
        {
            float best = fromZ;
            float bestCost = float.MaxValue;
            foreach (float corridor in corridorsZ)
            {
                float cost = Mathf.Abs(fromZ - corridor) + Mathf.Abs(corridor - toZ);
                if (cost < bestCost)
                {
                    bestCost = cost;
                    best = corridor;
                }
            }
            return best;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.6f);
            foreach (float z in corridorsZ)
                Gizmos.DrawLine(new Vector3(-21f, 0.1f, z), new Vector3(21f, 0.1f, z));
            foreach (float x in aisleCentersX)
                Gizmos.DrawLine(new Vector3(x, 0.1f, -shelfZoneAbsZ.y), new Vector3(x, 0.1f, shelfZoneAbsZ.y));
        }
    }
}
