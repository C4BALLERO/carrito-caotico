using System;
using System.Collections.Generic;

namespace CarritoCaotico
{
    /// <summary>Resultado de una línea del recibo (un tipo de producto).</summary>
    public readonly struct ReceiptLine
    {
        public readonly ProductType Type;
        public readonly int Required;
        public readonly int Delivered;
        public readonly int Correct;
        public readonly int Missing;
        public readonly int Extra;

        public ReceiptLine(ProductType type, int required, int delivered)
        {
            Type = type;
            Required = Math.Max(0, required);
            Delivered = Math.Max(0, delivered);
            // Una unidad nunca cuenta a la vez como correcta y sobrante.
            Correct = Math.Min(Required, Delivered);
            Missing = Required - Correct;
            Extra = Delivered - Correct;
        }
    }

    /// <summary>Recibo final congelado de una partida.</summary>
    public sealed class ShoppingReceipt
    {
        public readonly List<ReceiptLine> Lines = new List<ReceiptLine>();
        public int Correct { get; internal set; }
        public int Missing { get; internal set; }
        public int Extra { get; internal set; }
        public float ElapsedSeconds { get; internal set; }
        public float RemainingSeconds { get; internal set; }
        public int Score { get; internal set; }
        public bool Delivered { get; internal set; }
        public bool TimedOut => !Delivered;
        public bool IsComplete => Delivered && Missing == 0;
        public string ResultLabel { get; internal set; }
    }

    /// <summary>
    /// Reglas puras de la lista de compras (sin dependencias de Unity) para poder probarlas.
    /// </summary>
    public static class ShoppingRules
    {
        public const int PointsPerCorrect = 100;
        public const int PenaltyPerMissing = 50;
        public const int PenaltyPerExtra = 25;
        public const int CompletionBonus = 300;
        public const int PointsPerSecondLeft = 5;

        /// <summary>
        /// Genera una lista aleatoria de entre minUnits y maxUnits unidades, eligiendo
        /// entre los tipos indicados y con un máximo de maxPerType unidades por tipo.
        /// </summary>
        public static void GenerateList(Random rng, int minUnits, int maxUnits, int maxPerType,
            IDictionary<ProductType, int> result, IReadOnlyList<ProductType> types = null)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (types == null) types = ProductCatalog.AllTypes;
            if (types.Count == 0) throw new ArgumentException("Se necesita al menos un tipo de producto.", nameof(types));

            result.Clear();
            maxPerType = Math.Max(1, maxPerType);
            int capacity = types.Count * maxPerType;
            minUnits = Math.Max(1, Math.Min(minUnits, capacity));
            maxUnits = Math.Max(minUnits, Math.Min(maxUnits, capacity));

            int total = rng.Next(minUnits, maxUnits + 1);
            int placed = 0;
            while (placed < total)
            {
                ProductType type = types[rng.Next(types.Count)];
                result.TryGetValue(type, out int current);
                // Se vuelve a elegir si este tipo ya alcanzó el límite permitido.
                if (current >= maxPerType) continue;
                result[type] = current + 1;
                placed++;
            }
        }

        /// <summary>
        /// Compara lo pedido con lo entregado por tipo y cantidad.
        /// Si delivered es false (tiempo agotado) la compra no cuenta y la puntuación es 0.
        /// </summary>
        public static ShoppingReceipt Evaluate(IReadOnlyDictionary<ProductType, int> required,
            IReadOnlyDictionary<ProductType, int> deliveredCounts, float elapsedSeconds, float remainingSeconds,
            bool delivered)
        {
            var receipt = new ShoppingReceipt
            {
                ElapsedSeconds = Math.Max(0f, elapsedSeconds),
                RemainingSeconds = Math.Max(0f, remainingSeconds),
                Delivered = delivered
            };

            foreach (ProductType type in ProductCatalog.AllTypes)
            {
                int req = 0;
                int del = 0;
                if (required != null) required.TryGetValue(type, out req);
                if (delivered && deliveredCounts != null) deliveredCounts.TryGetValue(type, out del);
                if (req == 0 && del == 0) continue;

                var line = new ReceiptLine(type, req, del);
                receipt.Lines.Add(line);
                receipt.Correct += line.Correct;
                receipt.Missing += line.Missing;
                receipt.Extra += line.Extra;
            }

            if (!delivered)
            {
                receipt.Score = 0;
                receipt.ResultLabel = "Compra no entregada";
                return receipt;
            }

            int score = receipt.Correct * PointsPerCorrect
                        - receipt.Missing * PenaltyPerMissing
                        - receipt.Extra * PenaltyPerExtra;
            if (receipt.Missing == 0)
                score += CompletionBonus + (int)Math.Ceiling(receipt.RemainingSeconds) * PointsPerSecondLeft;
            receipt.Score = Math.Max(0, score);

            if (receipt.Missing == 0 && receipt.Extra == 0) receipt.ResultLabel = "¡Compra perfecta!";
            else if (receipt.Missing == 0) receipt.ResultLabel = "Compra completa (con sobrantes)";
            else if (receipt.Correct > 0) receipt.ResultLabel = "Compra incompleta";
            else receipt.ResultLabel = "Compra fallida";

            return receipt;
        }
    }
}
