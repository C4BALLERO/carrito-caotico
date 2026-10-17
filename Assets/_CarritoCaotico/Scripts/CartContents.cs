using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarritoCaotico
{
    /// <summary>
    /// Zona trigger dentro de la canasta. Registra los productos presentes (sin duplicados),
    /// calcula el peso total y lo comunica al carrito. Los productos sostenidos por la mano
    /// nunca cuentan como contenido.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class CartContents : MonoBehaviour
    {
        [SerializeField] CartController cart;

        readonly HashSet<Product> products = new HashSet<Product>();
        readonly List<Product> pruneBuffer = new List<Product>();
        float totalWeight;

        public IReadOnlyCollection<Product> Products => products;
        public int Count => products.Count;
        public float TotalWeight => totalWeight;

        /// <summary>Se dispara cada vez que entra o sale un producto.</summary>
        public event Action Changed;

        void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        void Awake()
        {
            Collider zone = GetComponent<Collider>();
            if (!zone.isTrigger)
            {
                Debug.LogWarning("[CartContents] El collider de la canasta debe ser trigger. Se corrige automáticamente.", this);
                zone.isTrigger = true;
            }
        }

        void OnTriggerEnter(Collider other)
        {
            Product product = GetProduct(other);
            if (product == null || product.IsHeld) return;

            if (products.Add(product))
            {
                product.HeldStateChanged += OnProductHeldStateChanged;
                Recalculate();
            }
        }

        void OnTriggerExit(Collider other)
        {
            Remove(GetProduct(other));
        }

        void OnProductHeldStateChanged(Product product)
        {
            // Al agarrarlo deja de contar; al soltarlo dentro, OnTriggerEnter lo vuelve a registrar.
            if (product.IsHeld) Remove(product);
        }

        void FixedUpdate()
        {
            // Limpieza defensiva: productos destruidos, desactivados o sostenidos.
            pruneBuffer.Clear();
            foreach (Product product in products)
            {
                if (product == null || !product.isActiveAndEnabled || product.IsHeld)
                    pruneBuffer.Add(product);
            }
            if (pruneBuffer.Count == 0) return;

            for (int i = 0; i < pruneBuffer.Count; i++)
            {
                Product product = pruneBuffer[i];
                products.Remove(product);
                if (product != null) product.HeldStateChanged -= OnProductHeldStateChanged;
            }
            Recalculate();
        }

        void OnDestroy()
        {
            foreach (Product product in products)
            {
                if (product != null) product.HeldStateChanged -= OnProductHeldStateChanged;
            }
            products.Clear();
        }

        void Remove(Product product)
        {
            if (product == null || !products.Remove(product)) return;
            product.HeldStateChanged -= OnProductHeldStateChanged;
            Recalculate();
        }

        static Product GetProduct(Collider other)
        {
            Rigidbody rb = other.attachedRigidbody;
            return rb != null ? rb.GetComponent<Product>() : other.GetComponentInParent<Product>();
        }

        void Recalculate()
        {
            float sum = 0f;
            foreach (Product product in products)
            {
                if (product != null) sum += product.Weight;
            }
            totalWeight = sum;
            if (cart != null) cart.SetCargoWeight(totalWeight);
            Changed?.Invoke();
        }

        public bool Contains(Product product) => product != null && products.Contains(product);

        public int CountOf(ProductType type)
        {
            int count = 0;
            foreach (Product product in products)
            {
                if (product != null && product.Type == type) count++;
            }
            return count;
        }

        /// <summary>Rellena un diccionario con las unidades presentes por tipo.</summary>
        public void FillCounts(Dictionary<ProductType, int> counts)
        {
            counts.Clear();
            foreach (Product product in products)
            {
                if (product == null) continue;
                counts.TryGetValue(product.Type, out int current);
                counts[product.Type] = current + 1;
            }
        }
    }
}
