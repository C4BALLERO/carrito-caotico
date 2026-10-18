using System;
using UnityEngine;

namespace CarritoCaotico
{
    /// <summary>Personajes jugables. El orden es estable: se guarda como número en PlayerPrefs.</summary>
    public enum CharacterId
    {
        Pinguino,
        Zorro,
        Cerdo,
    }

    /// <summary>
    /// Personaje elegido por cada jugador en el menú. Se guarda en PlayerPrefs y avisa con
    /// <see cref="Changed"/> para que el carrito cambie de personaje al momento.
    /// </summary>
    public static class CharacterChoice
    {
        const string PusherKey = "CarritoCaotico.PersonajeEmpujador";
        const string GrabberKey = "CarritoCaotico.PersonajeAgarrador";

        public const CharacterId DefaultPusher = CharacterId.Pinguino;
        public const CharacterId DefaultGrabber = CharacterId.Zorro;

        public static readonly CharacterId[] All = { CharacterId.Pinguino, CharacterId.Zorro, CharacterId.Cerdo };

        public static event Action Changed;

        public static CharacterId Pusher
        {
            get => Load(PusherKey, DefaultPusher);
            set => Save(PusherKey, value);
        }

        public static CharacterId Grabber
        {
            get => Load(GrabberKey, DefaultGrabber);
            set => Save(GrabberKey, value);
        }

        /// <summary>Siguiente (+1) o anterior (-1) personaje, dando la vuelta.</summary>
        public static CharacterId Cycle(CharacterId current, int delta)
        {
            int count = All.Length;
            return (CharacterId)((((int)current + delta) % count + count) % count);
        }

        public static string DisplayName(CharacterId id)
        {
            switch (id)
            {
                case CharacterId.Zorro: return "Zorro";
                case CharacterId.Cerdo: return "Cerdo";
                default: return "Pingüino";
            }
        }

        static CharacterId Load(string key, CharacterId fallback)
        {
            int value = PlayerPrefs.GetInt(key, (int)fallback);
            return value >= 0 && value < All.Length ? (CharacterId)value : fallback;
        }

        static void Save(string key, CharacterId value)
        {
            if (PlayerPrefs.GetInt(key, -1) == (int)value) return;
            PlayerPrefs.SetInt(key, (int)value);
            Changed?.Invoke();
        }
    }
}
