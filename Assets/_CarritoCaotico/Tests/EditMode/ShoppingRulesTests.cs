using System.Collections.Generic;
using NUnit.Framework;

namespace CarritoCaotico.Tests
{
    /// <summary>Pruebas de la lógica pura de la lista de compras y la evaluación en caja.</summary>
    public class ShoppingRulesTests
    {
        static Dictionary<ProductType, int> Counts(params (ProductType type, int count)[] items)
        {
            var result = new Dictionary<ProductType, int>();
            foreach (var (type, count) in items) result[type] = count;
            return result;
        }

        [Test]
        public void Generate_RespectsUnitBoundsAndMaxPerType()
        {
            var list = new Dictionary<ProductType, int>();
            for (int seed = 1; seed <= 500; seed++)
            {
                ShoppingRules.GenerateList(new System.Random(seed), 3, 5, 2, list);

                int total = 0;
                foreach (var pair in list)
                {
                    Assert.That(pair.Value, Is.InRange(1, 2), $"Semilla {seed}: {pair.Key} = {pair.Value}");
                    total += pair.Value;
                }
                Assert.That(total, Is.InRange(3, 5), $"Semilla {seed}: total {total}");
            }
        }

        [Test]
        public void Generate_EventuallyUsesAllTypes()
        {
            var seen = new HashSet<ProductType>();
            var list = new Dictionary<ProductType, int>();
            for (int seed = 1; seed <= 400; seed++)
            {
                ShoppingRules.GenerateList(new System.Random(seed), 3, 5, 2, list);
                seen.UnionWith(list.Keys);
            }
            Assert.AreEqual(ProductCatalog.AllTypes.Length, seen.Count);
        }

        [Test]
        public void Generate_SameSeedGivesSameList()
        {
            var a = new Dictionary<ProductType, int>();
            var b = new Dictionary<ProductType, int>();
            ShoppingRules.GenerateList(new System.Random(42), 3, 5, 2, a);
            ShoppingRules.GenerateList(new System.Random(42), 3, 5, 2, b);
            CollectionAssert.AreEquivalent(a, b);
        }

        [Test]
        public void Evaluate_RepeatedQuantities_CountsCorrectAndMissing()
        {
            var required = Counts((ProductType.Leche, 2), (ProductType.Lata, 1));
            var delivered = Counts((ProductType.Leche, 1), (ProductType.Lata, 1));

            ShoppingReceipt receipt = ShoppingRules.Evaluate(required, delivered, 60f, 60f, true);

            Assert.AreEqual(2, receipt.Correct);
            Assert.AreEqual(1, receipt.Missing);
            Assert.AreEqual(0, receipt.Extra);
            Assert.IsFalse(receipt.IsComplete);
            Assert.AreEqual("Compra incompleta", receipt.ResultLabel);
        }

        [Test]
        public void Evaluate_ExtraUnits_AreNeverCountedTwice()
        {
            var required = Counts((ProductType.Sandia, 1));
            var delivered = Counts((ProductType.Sandia, 3));

            ShoppingReceipt receipt = ShoppingRules.Evaluate(required, delivered, 30f, 90f, true);

            Assert.AreEqual(1, receipt.Correct);
            Assert.AreEqual(2, receipt.Extra);
            Assert.AreEqual(0, receipt.Missing);
            // Cada unidad entregada es correcta o sobrante, nunca ambas.
            Assert.AreEqual(3, receipt.Correct + receipt.Extra);
            Assert.IsTrue(receipt.IsComplete);
        }

        [Test]
        public void Evaluate_UnrequestedType_IsExtra()
        {
            var required = Counts((ProductType.Cereal, 1));
            var delivered = Counts((ProductType.Cereal, 1), (ProductType.Huevos, 2));

            ShoppingReceipt receipt = ShoppingRules.Evaluate(required, delivered, 50f, 70f, true);

            Assert.AreEqual(1, receipt.Correct);
            Assert.AreEqual(2, receipt.Extra);
            Assert.AreEqual(2, receipt.Lines.Count);
            Assert.AreEqual("Compra completa (con sobrantes)", receipt.ResultLabel);
        }

        [Test]
        public void Evaluate_PerfectPurchase_GetsBonus()
        {
            var required = Counts((ProductType.PapelHigienico, 2), (ProductType.Lata, 1));
            var delivered = Counts((ProductType.PapelHigienico, 2), (ProductType.Lata, 1));

            ShoppingReceipt receipt = ShoppingRules.Evaluate(required, delivered, 80f, 40f, true);

            int expected = 3 * ShoppingRules.PointsPerCorrect + ShoppingRules.CompletionBonus + 40 * ShoppingRules.PointsPerSecondLeft;
            Assert.AreEqual(expected, receipt.Score);
            Assert.AreEqual("¡Compra perfecta!", receipt.ResultLabel);
        }

        [Test]
        public void Evaluate_TimeUp_ScoresZeroAndIgnoresBasket()
        {
            var required = Counts((ProductType.Leche, 1));
            var delivered = Counts((ProductType.Leche, 1));

            ShoppingReceipt receipt = ShoppingRules.Evaluate(required, delivered, 120f, 0f, false);

            Assert.IsTrue(receipt.TimedOut);
            Assert.AreEqual(0, receipt.Score);
            Assert.AreEqual(0, receipt.Correct);
            Assert.AreEqual(1, receipt.Missing);
        }

        [Test]
        public void Evaluate_ScoreNeverNegative()
        {
            var required = Counts((ProductType.Leche, 2));
            var delivered = Counts((ProductType.Huevos, 2));

            ShoppingReceipt receipt = ShoppingRules.Evaluate(required, delivered, 100f, 20f, true);

            Assert.AreEqual(0, receipt.Score);
            Assert.AreEqual("Compra fallida", receipt.ResultLabel);
        }

        [Test]
        public void TimerFormat_ShowsMinutesAndSeconds()
        {
            Assert.AreEqual("2:00", GameTimer.Format(120f));
            Assert.AreEqual("0:05", GameTimer.Format(4.2f));
            Assert.AreEqual("0:00", GameTimer.Format(-3f));
        }
    }
}
