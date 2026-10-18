using System;
using UnityEngine;

namespace CarritoCaotico
{
    /// <summary>
    /// Tripulación del carrito: todos los personajes están colocados en los dos puestos y solo se
    /// activa el elegido por cada jugador (<see cref="CharacterChoice"/>). Cambia al momento si
    /// se elige otro en el menú.
    /// </summary>
    public class CartCrew : MonoBehaviour
    {
        [Tooltip("Un Empujador por personaje, en el orden de CharacterId.")]
        [SerializeField] GameObject[] pushers = Array.Empty<GameObject>();
        [Tooltip("Un Agarrador por personaje, en el orden de CharacterId.")]
        [SerializeField] GameObject[] grabbers = Array.Empty<GameObject>();

        public GameObject ActivePusher => Get(pushers, CharacterChoice.Pusher);
        public GameObject ActiveGrabber => Get(grabbers, CharacterChoice.Grabber);

        void OnEnable()
        {
            CharacterChoice.Changed += Apply;
            Apply();
        }

        void OnDisable()
        {
            CharacterChoice.Changed -= Apply;
        }

        public void Apply()
        {
            Activate(pushers, CharacterChoice.Pusher);
            Activate(grabbers, CharacterChoice.Grabber);
        }

        static void Activate(GameObject[] options, CharacterId chosen)
        {
            for (int i = 0; i < options.Length; i++)
            {
                if (options[i] != null) options[i].SetActive(i == (int)chosen);
            }
        }

        static GameObject Get(GameObject[] options, CharacterId id)
        {
            int index = (int)id;
            return index < options.Length ? options[index] : null;
        }
    }
}
