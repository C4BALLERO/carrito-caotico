using System.Collections.Generic;
using NUnit.Framework;

namespace CarritoCaotico.Tests
{
    /// <summary>Pruebas de la progresión de niveles y del cambio de controles.</summary>
    public class LevelProgressionTests
    {
        [Test]
        public void Level1_IsTheOriginalChallenge()
        {
            LevelConfig config = LevelProgression.Get(1, 123);

            Assert.AreEqual(120f, config.Duration);
            Assert.AreEqual(3, config.MinUnits);
            Assert.AreEqual(5, config.MaxUnits);
            Assert.AreEqual(2, config.MaxPerType);
            Assert.AreEqual(0, config.RivalCount);
            Assert.AreEqual(ControlScheme.Normal, config.Scheme);
        }

        [Test]
        public void Difficulty_NeverDecreases()
        {
            LevelConfig previous = LevelProgression.Get(1, 7);
            for (int level = 2; level <= 25; level++)
            {
                LevelConfig current = LevelProgression.Get(level, 7);
                Assert.GreaterOrEqual(current.MinUnits, previous.MinUnits, $"Nivel {level}: unidades mínimas");
                Assert.GreaterOrEqual(current.MaxUnits, previous.MaxUnits, $"Nivel {level}: unidades máximas");
                Assert.GreaterOrEqual(current.RivalCount, previous.RivalCount, $"Nivel {level}: rivales");
                Assert.GreaterOrEqual(current.RivalSpeed, previous.RivalSpeed, $"Nivel {level}: velocidad de rivales");
                Assert.LessOrEqual(current.Duration, previous.Duration, $"Nivel {level}: duración");
                Assert.LessOrEqual(current.MaxUnits, LevelProgression.MaxListUnits);
                Assert.LessOrEqual(current.RivalCount, LevelProgression.MaxRivals);
                Assert.LessOrEqual(current.MinUnits, current.MaxUnits);
                previous = current;
            }
            Assert.Greater(LevelProgression.Get(10, 7).RivalCount, 0);
        }

        [Test]
        public void ControlSchemes_NormalBeforeLevel4_AndRandomAfter()
        {
            var seen = new HashSet<ControlScheme>();
            for (int seed = 1; seed <= 40; seed++)
            {
                for (int level = 1; level < LevelProgression.ControlSwapStartLevel; level++)
                    Assert.AreEqual(ControlScheme.Normal, LevelProgression.Get(level, seed).Scheme);

                for (int level = LevelProgression.ControlSwapStartLevel; level <= 20; level++)
                    seen.Add(LevelProgression.Get(level, seed).Scheme);
            }
            Assert.Contains(ControlScheme.Intercambiado, new List<ControlScheme>(seen));
            Assert.Contains(ControlScheme.Invertido, new List<ControlScheme>(seen));
        }

        [Test]
        public void SameSeedAndLevel_GiveSameScheme()
        {
            for (int level = 1; level <= 15; level++)
                Assert.AreEqual(LevelProgression.Get(level, 99).Scheme, LevelProgression.Get(level, 99).Scheme);
        }

        [Test]
        public void Generate_UsesOnlyTheStoreProducts()
        {
            var halloween = new[] { ProductType.Calabaza, ProductType.Vela, ProductType.Pocion, ProductType.Murcielago };
            var list = new Dictionary<ProductType, int>();
            for (int seed = 1; seed <= 200; seed++)
            {
                ShoppingRules.GenerateList(new System.Random(seed), 3, 8, 3, list, halloween);
                foreach (ProductType type in list.Keys)
                    CollectionAssert.Contains(halloween, type);
            }
        }

        [Test]
        public void Stores_HaveDistinctScenesAndLoadingImages()
        {
            Assert.AreNotEqual(StoreCatalog.SceneName(StoreId.Clasico), StoreCatalog.SceneName(StoreId.Halloween));
            Assert.AreNotEqual(StoreCatalog.LoadingPrefix(StoreId.Clasico), StoreCatalog.LoadingPrefix(StoreId.Halloween));
            foreach (StoreId store in StoreCatalog.All)
                Assert.Greater(StoreCatalog.Tips(store).Length, 0);
        }

        [Test]
        public void KeyLabels_ExistForEveryScheme()
        {
            foreach (ControlScheme scheme in new[] { ControlScheme.Normal, ControlScheme.Intercambiado, ControlScheme.Invertido })
            {
                Assert.AreEqual(4, GameInput.PusherKeyLabels(scheme).Length);
                Assert.AreEqual(3, GameInput.GrabberKeyLabels(scheme).Length);
            }
            Assert.AreNotEqual(GameInput.PusherKeyLabels(ControlScheme.Normal)[0], GameInput.PusherKeyLabels(ControlScheme.Intercambiado)[0]);
        }
    }
}
