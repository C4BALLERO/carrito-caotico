using System.Text;
using UnityEngine;

namespace CarritoCaotico
{
    /// <summary>
    /// Todos los sonidos del juego. Cada campo corresponde a un archivo de 05_Audio con el mismo
    /// nombre en snake_case (<see cref="FileNameFor"/>): el generador de escena los asigna solo.
    /// Cualquier clip puede faltar; el AudioDirector simplemente no lo reproduce.
    /// </summary>
    [CreateAssetMenu(menuName = "Carrito Caótico/Biblioteca de sonidos", fileName = "SoundLibrary")]
    public class SoundLibrary : ScriptableObject
    {
        [Header("Ambientación (bucle)")]
        public AudioClip ambDia;
        public AudioClip ambNoche;

        [Header("Música (bucle)")]
        public AudioClip musMenu;
        public AudioClip musDia;
        public AudioClip musNoche;
        public AudioClip musUrgencia;

        [Header("Carrito")]
        public AudioClip sfxRuedasLoop;
        public AudioClip sfxDerrape;
        public AudioClip sfxChoque;
        public AudioClip sfxCharcoAgua;
        public AudioClip sfxCharcoBaba;

        [Header("Brazo")]
        public AudioClip sfxAgarrar;
        public AudioClip sfxSoltar;
        public AudioClip sfxLanzar;
        public AudioClip sfxCanasta;

        [Header("Lista y caja")]
        public AudioClip sfxListaOk;
        public AudioClip sfxCajaPago;
        public AudioClip sfxCajaVacia;
        public AudioClip sfxRivalDia;
        public AudioClip sfxRivalNoche;

        [Header("Tiempo y resultados")]
        public AudioClip sfxTic;
        public AudioClip sfxAviso30s;
        public AudioClip sfxTiempoDia;
        public AudioClip sfxTiempoNoche;
        public AudioClip sfxVictoria;
        public AudioClip sfxDerrota;

        [Header("Personajes")]
        public AudioClip vozPinguino;
        public AudioClip vozZorro;
        public AudioClip vozCerdo;

        [Header("Interfaz y flujo")]
        public AudioClip uiNavegar;
        public AudioClip uiConfirmar;
        public AudioClip uiPausa;
        public AudioClip sfxCuentaAtras;
        public AudioClip sfxControlesCambiados;
        public AudioClip sfxMando;

        public AudioClip Voice(CharacterId id)
        {
            switch (id)
            {
                case CharacterId.Zorro: return vozZorro;
                case CharacterId.Cerdo: return vozCerdo;
                default: return vozPinguino;
            }
        }

        /// <summary>Nombre de archivo (sin extensión) de un campo: sfxAviso30s → sfx_aviso_30s.</summary>
        public static string FileNameFor(string fieldName)
        {
            var builder = new StringBuilder(fieldName.Length + 8);
            for (int i = 0; i < fieldName.Length; i++)
            {
                char c = fieldName[i];
                bool startsNumber = char.IsDigit(c) && i > 0 && !char.IsDigit(fieldName[i - 1]);
                if (char.IsUpper(c) || startsNumber) builder.Append('_');
                builder.Append(char.ToLowerInvariant(c));
            }
            return builder.ToString();
        }
    }
}
