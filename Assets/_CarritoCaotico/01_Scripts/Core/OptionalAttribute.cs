using System;
using UnityEngine;

namespace CarritoCaotico
{
    /// <summary>
    /// Marca una referencia serializada que puede quedar vacía a propósito (por ejemplo, las
    /// flechas de una opción del menú que no tiene valor). El validador de escenas no la cuenta
    /// como un error.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class OptionalAttribute : PropertyAttribute
    {
    }
}
