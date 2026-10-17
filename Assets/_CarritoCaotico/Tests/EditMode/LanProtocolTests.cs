using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace CarritoCaotico.Tests
{
    /// <summary>Serialización de los mensajes LAN y reparto de controles por rol.</summary>
    public class LanProtocolTests
    {
        [TearDown]
        public void TearDown()
        {
            GameInput.ResetLan();
            GameInput.Scheme = ControlScheme.Normal;
        }

        [Test]
        public void Writer_RoundTripsPrimitives()
        {
            var writer = new NetWriter(4);
            writer.WriteByte(250);
            writer.WriteBool(true);
            writer.WriteUShort(65000);
            writer.WriteShort(-1234);
            writer.WriteInt(-123456789);
            writer.WriteFloat(3.25f);
            writer.WriteVector3(new Vector3(1.5f, -2f, 99.125f));
            writer.WriteString("Súper Embrujado ñ");

            var reader = new NetReader(writer.ToArray());
            Assert.AreEqual(250, reader.ReadByte());
            Assert.IsTrue(reader.ReadBool());
            Assert.AreEqual(65000, reader.ReadUShort());
            Assert.AreEqual(-1234, reader.ReadShort());
            Assert.AreEqual(-123456789, reader.ReadInt());
            Assert.AreEqual(3.25f, reader.ReadFloat());
            Assert.AreEqual(new Vector3(1.5f, -2f, 99.125f), reader.ReadVector3());
            Assert.AreEqual("Súper Embrujado ñ", reader.ReadString());
            Assert.IsFalse(reader.Failed);
            Assert.AreEqual(0, reader.Remaining);
        }

        [Test]
        public void Reader_ShortPacketFailsWithoutThrowing()
        {
            var reader = new NetReader(new byte[] { 1, 2 });
            Assert.AreEqual(0, reader.ReadInt());
            Assert.IsTrue(reader.Failed);
            Assert.AreEqual(0f, reader.ReadFloat());
        }

        [Test]
        public void Rotation_IsCompressedWithSmallError()
        {
            Quaternion original = Quaternion.Euler(33f, -120f, 71f);
            var writer = new NetWriter();
            writer.WriteRotation(original);
            Assert.AreEqual(8, writer.Length, "Un cuaternión ocupa 8 bytes.");
            Quaternion decoded = new NetReader(writer.ToArray()).ReadRotation();
            Assert.Less(Quaternion.Angle(original, decoded), 0.05f);
        }

        [Test]
        public void Snapshot_RoundTripsWorldState()
        {
            var snapshot = new SnapshotMessage
            {
                HostTime = 12.5f,
                State = GameState.EnJuego,
                Paused = true,
                Preparation = 1.5f,
                TimerRemaining = 77.25f,
                TimerRunning = true,
                RivalsActive = 3,
                RivalsTaken = 4,
                CartPosition = new Vector3(-4f, 0.02f, -14f),
                CartYaw = 90f,
                HandX = 0.5f,
                HandZ = 1.25f,
                HeldIndex = 17
            };
            snapshot.Contents.Add((ProductType.Leche, 2));
            snapshot.Contents.Add((ProductType.Calabaza, 1));
            snapshot.Products.Add(new ProductState { Index = 17, Position = new Vector3(1f, 2f, 3f), Rotation = Quaternion.Euler(0f, 45f, 0f), Flags = ProductState.HeldFlag });
            snapshot.Products.Add(new ProductState { Index = 300, Position = new Vector3(-7f, 0.4f, 9f), Rotation = Quaternion.identity, Flags = ProductState.TakenFlag });
            snapshot.Rivals.Add(new RivalState { Id = 6, Position = new Vector3(5f, 0f, -2f), Yaw = 180f, Color = new Color32(249, 115, 22, 255) });
            snapshot.Obstacles.Add(new ObstacleState { Index = 1, Position = new Vector3(0f, 1.2f, 4f), Yaw = 270f });

            var writer = new NetWriter();
            snapshot.Write(writer);
            var reader = new NetReader(writer.ToArray());
            Assert.AreEqual(LanMessage.Snapshot, (LanMessage)reader.ReadByte());
            var decoded = new SnapshotMessage();
            decoded.Read(reader);

            Assert.IsFalse(reader.Failed);
            Assert.AreEqual(12.5f, decoded.HostTime);
            Assert.AreEqual(GameState.EnJuego, decoded.State);
            Assert.IsTrue(decoded.Paused);
            Assert.AreEqual(77.25f, decoded.TimerRemaining);
            Assert.AreEqual(3, decoded.RivalsActive);
            Assert.AreEqual(4, decoded.RivalsTaken);
            CollectionAssert.AreEqual(snapshot.Contents, decoded.Contents);
            Assert.AreEqual(snapshot.CartPosition, decoded.CartPosition);
            Assert.AreEqual(17, decoded.HeldIndex);
            Assert.AreEqual(2, decoded.Products.Count);
            Assert.AreEqual(300, decoded.Products[1].Index);
            Assert.AreEqual(ProductState.TakenFlag, decoded.Products[1].Flags);
            Assert.Less(Quaternion.Angle(snapshot.Products[0].Rotation, decoded.Products[0].Rotation), 0.05f);
            Assert.AreEqual(6, decoded.Rivals[0].Id);
            Assert.AreEqual((Color32)new Color32(249, 115, 22, 255), decoded.Rivals[0].Color);
            Assert.AreEqual(270f, decoded.Obstacles[0].Yaw);
        }

        [Test]
        public void LevelSetup_RebuildsConfigAndList()
        {
            var required = new Dictionary<ProductType, int> { { ProductType.Pan, 2 }, { ProductType.Leche, 1 } };
            var config = new LevelConfig { Level = 5, Scheme = ControlScheme.Intercambiado, Duration = 100f, MinUnits = 5, MaxUnits = 7, MaxPerType = 3, RivalCount = 2 };
            var writer = new NetWriter();
            LevelSetupMessage.From(config, required, 4f).Write(writer);

            var reader = new NetReader(writer.ToArray());
            Assert.AreEqual(LanMessage.LevelSetup, (LanMessage)reader.ReadByte());
            LevelSetupMessage decoded = LevelSetupMessage.Read(reader);
            LevelConfig rebuilt = decoded.ToConfig();

            Assert.AreEqual(5, rebuilt.Level);
            Assert.AreEqual(ControlScheme.Intercambiado, rebuilt.Scheme);
            Assert.AreEqual(100f, rebuilt.Duration);
            Assert.AreEqual(2, rebuilt.RivalCount);
            Assert.AreEqual(4f, decoded.Preparation);
            CollectionAssert.AreEquivalent(new[] { (ProductType.Leche, 1), (ProductType.Pan, 2) }, decoded.Items);
        }

        [Test]
        public void GameEnded_ClientReceiptMatchesHost()
        {
            var required = new Dictionary<ProductType, int> { { ProductType.Pan, 2 }, { ProductType.Leche, 1 } };
            var delivered = new Dictionary<ProductType, int> { { ProductType.Pan, 2 }, { ProductType.Queso, 1 } };
            ShoppingReceipt host = ShoppingRules.Evaluate(required, delivered, 40f, 50f, true);

            var message = new GameEndedMessage { FinalState = GameState.CompraEntregada, Elapsed = host.ElapsedSeconds, Remaining = host.RemainingSeconds };
            foreach (ReceiptLine line in host.Lines)
                if (line.Delivered > 0) message.Delivered.Add((line.Type, line.Delivered));
            var writer = new NetWriter();
            message.Write(writer);
            var reader = new NetReader(writer.ToArray());
            reader.ReadByte();
            ShoppingReceipt client = GameEndedMessage.Read(reader).ToReceipt(required);

            Assert.AreEqual(host.Score, client.Score);
            Assert.AreEqual(host.Correct, client.Correct);
            Assert.AreEqual(host.Missing, client.Missing);
            Assert.AreEqual(host.Extra, client.Extra);
            Assert.AreEqual(host.ResultLabel, client.ResultLabel);
        }

        [Test]
        public void LanRole_SwapsWithIntercambiado()
        {
            Assert.AreEqual(NetRole.Ninguno, GameInput.LanRole, "Sin LAN no hay rol.");
            GameInput.LanBaseRole = NetRole.Empujador;
            Assert.AreEqual(NetRole.Empujador, GameInput.LanRole);
            GameInput.Scheme = ControlScheme.Intercambiado;
            Assert.AreEqual(NetRole.Agarrador, GameInput.LanRole, "Intercambiado cambia de puesto a los PC.");
            GameInput.Scheme = ControlScheme.Invertido;
            Assert.AreEqual(NetRole.Empujador, GameInput.LanRole);
        }

        [Test]
        public void RemotePusher_DrivesOnlyWhenPusherIsRemote()
        {
            GameInput.LanBaseRole = NetRole.Agarrador;
            GameInput.SetRemotePusher(0.75f, -0.5f, true);
            Assert.AreEqual(0.75f, GameInput.PusherThrottle());
            Assert.AreEqual(-0.5f, GameInput.PusherSteer());
            Assert.IsTrue(GameInput.PusherDrift());

            // Con Intercambiado este PC pasa a empujar: deja de usar lo que llega por la red.
            GameInput.Scheme = ControlScheme.Intercambiado;
            Assert.AreEqual(0f, GameInput.PusherThrottle());
            Assert.AreEqual(Vector2.zero, GameInput.GrabberMove(), "El Agarrador ahora es remoto.");
            Assert.IsFalse(GameInput.GrabberGrabPressed());
        }

        [Test]
        public void LanLabels_ShowBothKeySets()
        {
            GameInput.LanBaseRole = NetRole.Empujador;
            Assert.AreEqual("W / ↑", GameInput.PusherKeyboardLabels(ControlScheme.Normal)[0]);
            // Intercambiado no cambia teclas en LAN.
            Assert.AreEqual("W / ↑", GameInput.PusherKeyboardLabels(ControlScheme.Intercambiado)[0]);
            Assert.AreEqual("Enter / Espacio", GameInput.GrabberKeyboardLabels(ControlScheme.Normal)[1]);
            StringAssert.Contains("1ª y 3ª persona", GameUI.ControlsText(ControlScheme.Normal));
            StringAssert.DoesNotContain("Agarrador", GameUI.ControlsText(ControlScheme.Normal).Split('\n')[0]);
        }
    }
}
