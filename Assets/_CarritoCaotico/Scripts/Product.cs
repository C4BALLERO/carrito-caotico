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
        Lata = 5
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

        /// <summary>Se dispara al agarrar o soltar el producto.</summary>
        public event Action<Product> HeldStateChanged;

        void Awake()
        {
            if (body == null) body = GetComponent<Rigidbody>();
            colliders = GetComponentsInChildren<Collider>(true);
            body.mass = weight;
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
