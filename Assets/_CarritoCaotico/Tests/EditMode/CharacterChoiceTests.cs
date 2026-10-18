using NUnit.Framework;

namespace CarritoCaotico.Tests
{
    /// <summary>Pruebas del selector de personajes (sin escena).</summary>
    public class CharacterChoiceTests
    {
        [Test]
        public void Cycle_WrapsAroundInBothDirections()
        {
            Assert.AreEqual(CharacterId.Zorro, CharacterChoice.Cycle(CharacterId.Pinguino, 1));
            Assert.AreEqual(CharacterChoice.All[0], CharacterChoice.Cycle(CharacterId.Cerdo, 1), "Después del último vuelve al primero.");
            Assert.AreEqual(CharacterId.Cerdo, CharacterChoice.Cycle(CharacterChoice.All[0], -1));
        }

        [Test]
        public void KayKitCharacters_AreFirstAndTheDefault()
        {
            Assert.AreEqual(CharacterId.Caballero, CharacterChoice.All[0], "Los personajes KayKit van primero en el menú.");
            Assert.AreEqual(12, CharacterChoice.All.Length);
            Assert.AreEqual(CharacterId.Caballero, CharacterChoice.DefaultPusher);
            Assert.AreEqual(CharacterId.Maga, CharacterChoice.DefaultGrabber);
            var seen = new System.Collections.Generic.HashSet<CharacterId>(CharacterChoice.All);
            foreach (CharacterId id in System.Enum.GetValues(typeof(CharacterId)))
                Assert.IsTrue(seen.Contains(id), $"{id} no está en el menú.");
        }

        [Test]
        public void EveryCharacter_HasDistinctName()
        {
            var names = new System.Collections.Generic.HashSet<string>();
            foreach (CharacterId id in CharacterChoice.All)
                Assert.IsTrue(names.Add(CharacterChoice.DisplayName(id)), $"Nombre repetido: {id}");
        }

        [Test]
        public void Choice_IsSavedAndNotifies()
        {
            CharacterId saved = CharacterChoice.Grabber;
            int notifications = 0;
            void Count() => notifications++;
            CharacterChoice.Changed += Count;
            try
            {
                CharacterChoice.Grabber = CharacterChoice.Cycle(saved, 1);
                Assert.AreEqual(CharacterChoice.Cycle(saved, 1), CharacterChoice.Grabber);
                Assert.AreEqual(1, notifications);
                CharacterChoice.Grabber = CharacterChoice.Grabber;
                Assert.AreEqual(1, notifications, "Elegir el mismo personaje no avisa.");
            }
            finally
            {
                CharacterChoice.Changed -= Count;
                CharacterChoice.Grabber = saved;
            }
        }
    }
}
