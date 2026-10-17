using System;
using UnityEngine;

namespace CarritoCaotico
{
    /// <summary>
    /// Identificador estable de tipo de producto. Los valores numéricos no deben cambiar
    /// porque se serializan en escenas y prefabs.
    /// </summary>
    public enum ProductType
    {
        Leche = 0,
        Sandia = 1,
        Cereal = 2,
        Huevos = 3,
        PapelHigienico = 4,
        Lata = 5,
        Pan = 6,
        Manzana = 7,
        Platano = 8,
        Queso = 9,
        Agua = 10,
        Detergente = 11
    }

    /// <summary>Datos por defecto de cada tipo de producto.</summary>
    public static class ProductCatalog
    {
        public static readonly ProductType[] AllTypes = (ProductType[])Enum.GetValues(typeof(ProductType));

        public static string DisplayName(ProductType type)
        {
            switch (type)
            {
                case ProductType.Leche: return "Leche";
                case ProductType.Sandia: return "Sandía";
                case ProductType.Cereal: return "Cereal";
                case ProductType.Huevos: return "Huevos";
                case ProductType.PapelHigienico: return "Papel higiénico";
                case ProductType.Lata: return "Lata";
                case ProductType.Pan: return "Pan";
                case ProductType.Manzana: return "Manzana";
                case ProductType.Platano: return "Plátano";
                case ProductType.Queso: return "Queso";
                case ProductType.Agua: return "Agua";
                case ProductType.Detergente: return "Detergente";
                default: return type.ToString();
            }
        }

        /// <summary>Peso en kilogramos.</summary>
        public static float DefaultWeight(ProductType type)
        {
            switch (type)
            {
                case ProductType.Leche: return 1.0f;
                case ProductType.Sandia: return 4.0f;
                case ProductType.Cereal: return 0.5f;
                case ProductType.Huevos: return 0.6f;
                case ProductType.PapelHigienico: return 0.4f;
                case ProductType.Lata: return 0.35f;
                case ProductType.Pan: return 0.5f;
                case ProductType.Manzana: return 0.2f;
                case ProductType.Platano: return 0.9f;
                case ProductType.Queso: return 1.5f;
                case ProductType.Agua: return 1.5f;
                case ProductType.Detergente: return 3.0f;
                default: return 1f;
            }
        }
    }

    /// <summary>
    /// Producto físico del supermercado. Puede estar libre (simulado por física)
    /// o sostenido por la mano del Agarrador (cinemático y sin colisiones).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Product : MonoBehaviour
    {
        [SerializeField] ProductType type;
        [SerializeField] string displayName = "";
        [SerializeField, Min(0.05f)] float weight = 1f;
        [SerializeField] Rigidbody body;

        Collider[] colliders;
        RigidbodyInterpolation savedInterpolation = RigidbodyInterpolation.Interpolate;

        public ProductType Type => type;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? ProductCatalog.DisplayName(type) : displayName;
        public float Weight => weight;
        public Rigidbody Body => body;
        public bool IsHeld { get; private set; }
        /// <summary>Un comprador rival se lo llevó: ya no está disponible para nadie.</summary>
        public bool IsTakenByRival { get; private set; }
        /// <summary>Posición inicial en la balda, para saber si sigue en su sitio.</summary>
        public Vector3 HomePosition { get; private set; }

        /// <summary>true mientras el producto sigue quieto en su balda.</summary>
        public bool IsOnShelf => !IsHeld && !IsTakenByRival && isActiveAndEnabled
                                 && (transform.position - HomePosition).sqrMagnitude < 0.3f * 0.3f;

        /// <summary>Se dispara al agarrar o soltar el producto.</summary>
        public event Action<Product> HeldStateChanged;

        void Awake()
        {
            if (body == null) body = GetComponent<Rigidbody>();
            colliders = GetComponentsInChildren<Collider>(true);
            body.mass = weight;
            HomePosition = transform.position;
        }

        void OnValidate()
        {
            if (body == null) body = GetComponent<Rigidbody>();
            if (body != null) body.mass = Mathf.Max(0.05f, weight);
        }

        /// <summary>Configuración usada por el generador de escena.</summary>
        public void Configure(ProductType productType, string productName, float productWeight)
        {
            type = productType;
            displayName = productName;
            weight = Mathf.Max(0.05f, productWeight);
            if (body == null) body = GetComponent<Rigidbody>();
            if (body != null) body.mass = weight;
        }

        /// <summary>
        /// Une el producto al punto de agarre. Devuelve false si ya está sostenido.
        /// </summary>
        public bool TryAttach(Transform anchor)
        {
            if (IsHeld || anchor == null) return false;

            IsHeld = true;
            savedInterpolation = body.interpolation;
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.interpolation = RigidbodyInterpolation.None;
            body.isKinematic = true;
            body.detectCollisions = false;
            SetCollidersEnabled(false);

            transform.SetParent(anchor, true);
            transform.localPosition = Vector3.zero;

            HeldStateChanged?.Invoke(this);
            return true;
        }

        /// <summary>
        /// Un rival se lleva el producto: queda dentro de su carrito, sin física, para siempre.
        /// </summary>
        public bool TryTakeByRival(Transform basket, Vector3 localOffset)
        {
            if (IsHeld || IsTakenByRival || basket == null) return false;
            if (!TryAttach(basket)) return false;
            IsTakenByRival = true;
            transform.localPosition = localOffset;
            return true;
        }

        /// <summary>
        /// Suelta el producto en una posición del mundo con una velocidad inicial.
        /// </summary>
        public void Detach(Vector3 worldPosition, Vector3 velocity)
        {
            if (!IsHeld) return;

            transform.SetParent(null, true);
            transform.position = worldPosition;

            SetCollidersEnabled(true);
            body.detectCollisions = true;
            body.isKinematic = false;
            body.interpolation = savedInterpolation;
            body.position = worldPosition;
            body.linearVelocity = velocity;
            body.WakeUp();

            IsHeld = false;
            HeldStateChanged?.Invoke(this);
        }

        void SetCollidersEnabled(bool enabled)
        {
            if (colliders == null) colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null) colliders[i].enabled = enabled;
            }
        }
    }
}
