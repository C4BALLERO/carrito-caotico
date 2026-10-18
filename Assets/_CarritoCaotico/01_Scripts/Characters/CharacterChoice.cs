using System;
using UnityEngine;

namespace CarritoCaotico
{
    /// <summary>
    /// Personajes jugables. Los números son estables (se guardan en PlayerPrefs): los nuevos se
    /// añaden al final. El orden del menú es el de <see cref="CharacterChoice.All"/>.
    /// </summary>
    public enum CharacterId
    {
        Pinguino,
        Zorro,
        Cerdo,
        // Personajes KayKit (CC0), los mismos que usan los compradores rivales.
        Caballero,
        Barbaro,
        Maga,
        Picaro,
        PicaroCapucha,
        Esqueleto,
        EsqueletoGuerrero,
        EsqueletoPicaro,
        EsqueletoMago,
    }

    /// <summary>
    /// Personaje elegido por cada jugador en el menú. Se guarda en PlayerPrefs y avisa con
    /// <see cref="Changed"/> para que el carrito cambie de personaje al momento.
    /// </summary>
    public static class CharacterChoice
    {
        const string PusherKey = "CarritoCaotico.PersonajeEmpujador";
        const string GrabberKey = "CarritoCaotico.PersonajeAgarrador";

        public const CharacterId DefaultPusher = CharacterId.Caballero;
        public const CharacterId DefaultGrabber = CharacterId.Maga;

        /// <summary>Orden del selector del menú: primero los personajes KayKit.</summary>
        public static readonly CharacterId[] All =
        {
            CharacterId.Caballero, CharacterId.Barbaro, CharacterId.Maga, CharacterId.Picaro, CharacterId.PicaroCapucha,
            CharacterId.Esqueleto, CharacterId.EsqueletoGuerrero, CharacterId.EsqueletoPicaro, CharacterId.EsqueletoMago,
            CharacterId.Pinguino, CharacterId.Zorro, CharacterId.Cerdo,
        };

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

        /// <summary>Siguiente (+1) o anterior (-1) personaje en el orden del menú, dando la vuelta.</summary>
        public static CharacterId Cycle(CharacterId current, int delta)
        {
            int count = All.Length;
            int index = Array.IndexOf(All, current);
            if (index < 0) index = 0;
            return All[((index + delta) % count + count) % count];
        }

        public static string DisplayName(CharacterId id)
        {
            switch (id)
            {
                case CharacterId.Zorro: return "Zorro";
                case CharacterId.Cerdo: return "Cerdo";
                case CharacterId.Caballero: return "Caballero";
                case CharacterId.Barbaro: return "Bárbaro";
                case CharacterId.Maga: return "Maga";
                case CharacterId.Picaro: return "Pícaro";
                case CharacterId.PicaroCapucha: return "Pícaro con capucha";
                case CharacterId.Esqueleto: return "Esqueleto";
                case CharacterId.EsqueletoGuerrero: return "Esqueleto guerrero";
                case CharacterId.EsqueletoPicaro: return "Esqueleto pícaro";
                case CharacterId.EsqueletoMago: return "Esqueleto mago";
                default: return "Pingüino";
            }
        }

        static CharacterId Load(string key, CharacterId fallback)
        {
            int value = PlayerPrefs.GetInt(key, (int)fallback);
            return Enum.IsDefined(typeof(CharacterId), value) ? (CharacterId)value : fallback;
        }

        static void Save(string key, CharacterId value)
        {
            if (PlayerPrefs.GetInt(key, -1) == (int)value) return;
            PlayerPrefs.SetInt(key, (int)value);
            Changed?.Invoke();
        }
    }
}
