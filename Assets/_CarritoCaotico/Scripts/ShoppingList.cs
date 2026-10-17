using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarritoCaotico
{
    /// <summary>
    /// Lista de compras del Agarrador. Distingue unidades requeridas, conseguidas
    /// (presentes en la canasta) y faltantes. Un producto en la mano no cuenta.
    /// </summary>
    public class ShoppingList : MonoBehaviour
    {
        [SerializeField] CartContents cartContents;
        [SerializeField, Min(1)] int minUnits = 3;
        [SerializeField, Min(1)] int maxUnits = 5;
        [Tooltip("Máximo de unidades de un mismo tipo en la lista.")]
        [SerializeField, Min(1)] int maxPerType = 2;
        [Tooltip("0 = lista distinta en cada partida. Otro valor = lista reproducible.")]
        [SerializeField] int randomSeed;

        readonly Dictionary<ProductType, int> required = new Dictionary<ProductType, int>();
        readonly Dictionary<ProductType, int> inCart = new Dictionary<ProductType, int>();
        readonly List<ProductType> entries = new List<ProductType>();

        public IReadOnlyDictionary<ProductType, int> Required => required;
        /// <summary>Tipos de la lista en orden estable de visualización.</summary>
        public IReadOnlyList<ProductType> Entries => entries;
        public int MaxPerType => maxPerType;
        public bool IsGenerated => entries.Count > 0;

        public int TotalRequired
        {
            get
            {
                int total = 0;
                foreach (int value in required.Values) total += value;
                return total;
            }
        }

        public int TotalCollected
        {
            get
            {
                int total = 0;
                foreach (ProductType type in entries) total += GetCollected(type);
                return total;
            }
        }

        public bool IsComplete => IsGenerated && TotalCollected >= TotalRequired;

        public event Action Changed;

        void OnEnable()
        {
            if (cartContents != null) cartContents.Changed += Refresh;
        }

        void OnDisable()
        {
            if (cartContents != null) cartContents.Changed -= Refresh;
        }

        /// <summary>Genera una lista nueva y aleatoria.</summary>
        public void Generate()
        {
            int seed = randomSeed != 0 ? randomSeed : Guid.NewGuid().GetHashCode();
            ShoppingRules.GenerateList(new System.Random(seed), minUnits, maxUnits, maxPerType, required);

            entries.Clear();
            foreach (ProductType type in ProductCatalog.AllTypes)
            {
                if (required.ContainsKey(type)) entries.Add(type);
            }
            Refresh();
        }

        void Refresh()
        {
            if (cartContents != null) cartContents.FillCounts(inCart);
            else inCart.Clear();
            Changed?.Invoke();
        }

        public int GetRequired(ProductType type) => required.TryGetValue(type, out int value) ? value : 0;

        /// <summary>Unidades en la canasta que cuentan para la lista (sin pasar de lo pedido).</summary>
        public int GetCollected(ProductType type)
        {
            inCart.TryGetValue(type, out int present);
            return Mathf.Min(present, GetRequired(type));
        }

        public int GetMissing(ProductType type) => GetRequired(type) - GetCollected(type);

        /// <summary>Unidades en la canasta, incluidas las que sobran.</summary>
        public int GetInCart(ProductType type) => inCart.TryGetValue(type, out int value) ? value : 0;
    }
}
