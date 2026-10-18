using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace CarritoCaotico.Tests
{
    /// <summary>Daño por choque, fin por explosión, anuncios de partida LAN y elección de la IP local.</summary>
    public class HealthAndDiscoveryTests
    {
        [Test]
        public void ImpactDamage_GrowsWithSpeedAndIsCapped()
        {
            Assert.AreEqual(0f, CartHealth.DamageForImpact(1f, 2.5f, 9f, 30f), "Un roce no hace daño.");
            Assert.AreEqual(0f, CartHealth.DamageForImpact(2.5f, 2.5f, 9f, 30f));
            Assert.AreEqual(13.5f, CartHealth.DamageForImpact(4f, 2.5f, 9f, 30f), 0.001f);
            Assert.AreEqual(30f, CartHealth.DamageForImpact(20f, 2.5f, 9f, 30f), "Un golpe nunca quita más del máximo.");
        }

        [Test]
        public void ExplodedCart_ReceiptIsNotDelivered()
        {
            var required = new Dictionary<ProductType, int> { { ProductType.Leche, 2 } };
            var message = new GameEndedMessage { FinalState = GameState.CarritoDestruido, Elapsed = 50f, Remaining = 40f };
            message.Delivered.Add((ProductType.Leche, 2));
            ShoppingReceipt receipt = message.ToReceipt(required);
            Assert.IsFalse(receipt.Delivered, "Si el carrito explota la compra no cuenta.");
            Assert.AreEqual(0, receipt.Score);
            Assert.AreEqual(40f, receipt.RemainingSeconds, "Se conserva el tiempo que quedaba.");
            Assert.IsFalse(message.WasDelivered);
        }

        [Test]
        public void Snapshot_CarriesCartHealth()
        {
            var snapshot = new SnapshotMessage { CartHealth = 0.42f };
            var writer = new NetWriter();
            snapshot.Write(writer);
            var reader = new NetReader(writer.ToArray());
            reader.ReadByte();
            var decoded = new SnapshotMessage();
            decoded.Read(reader);
            Assert.IsFalse(reader.Failed);
            Assert.AreEqual(0.42f, decoded.CartHealth, 1f / 255f);
        }

        [Test]
        public void Beacon_RoundTripsAndRejectsForeignPackets()
        {
            byte[] data = LanDiscovery.BuildBeacon(NetRole.Agarrador, 7777, "PC|Salon");
            Assert.IsTrue(LanDiscovery.TryParseBeacon(data, "192.168.0.58", 3f, out LanBeacon beacon));
            Assert.AreEqual("192.168.0.58", beacon.Address);
            Assert.AreEqual(7777, beacon.Port);
            Assert.AreEqual(NetRole.Agarrador, beacon.HostRole);
            Assert.AreEqual("PC Salon", beacon.HostName, "El separador no puede romper el anuncio.");

            Assert.IsFalse(LanDiscovery.TryParseBeacon(System.Text.Encoding.UTF8.GetBytes("HOLA|2|7777|1|PC"), "1.1.1.1", 0f, out _));
            Assert.IsFalse(LanDiscovery.TryParseBeacon(System.Text.Encoding.UTF8.GetBytes("CARRITO|999|7777|1|PC"), "1.1.1.1", 0f, out _), "Otra versión del juego.");
            Assert.IsFalse(LanDiscovery.TryParseBeacon(System.Text.Encoding.UTF8.GetBytes("CARRITO|2|7777|9|PC"), "1.1.1.1", 0f, out _), "Rol desconocido.");
            Assert.IsFalse(LanDiscovery.TryParseBeacon(null, "1.1.1.1", 0f, out _));
        }

        [Test]
        public void AddressScore_PrefersHomeWifiOverVirtualAdapters()
        {
            int wifi = LanDiscovery.ScoreAddress("192.168.0.38", "Wi-Fi Intel(R) Wi-Fi 6", true, true);
            int cable = LanDiscovery.ScoreAddress("10.0.0.5", "Ethernet Realtek", false, true);
            int hyperV = LanDiscovery.ScoreAddress("172.20.16.1", "vEthernet (WSL) Hyper-V Virtual Ethernet Adapter", false, false);
            int vpn = LanDiscovery.ScoreAddress("10.8.0.2", "TAP-Windows Adapter V9 (VPN)", false, true);
            Assert.Greater(wifi, cable);
            Assert.Greater(cable, hyperV);
            Assert.Greater(cable, vpn);
            Assert.Less(hyperV, 0, "Los adaptadores virtuales van al final.");
        }

        [Test]
        public void Listener_FindsAnnouncedGame()
        {
            LanDiscovery listener = LanDiscovery.StartListening();
            if (!listener.IsRunning) Assert.Ignore("El puerto de descubrimiento está ocupado en este PC.");
            try
            {
                byte[] beacon = LanDiscovery.BuildBeacon(NetRole.Empujador, 7777, "Anfitrion");
                using (var sender = new UdpClient(AddressFamily.InterNetwork))
                    sender.Send(beacon, beacon.Length, new IPEndPoint(IPAddress.Loopback, LanDiscovery.DiscoveryPort));

                for (int i = 0; i < 50 && listener.Found.Count == 0; i++)
                {
                    Thread.Sleep(20);
                    listener.Update(1f);
                }
                Assert.AreEqual(1, listener.Found.Count, "El anuncio debería llegar.");
                Assert.AreEqual("127.0.0.1", listener.Found[0].Address);
                Assert.AreEqual(NetRole.Empujador, listener.Found[0].HostRole);

                // Los anuncios caducan si el anfitrión deja de mandarlos.
                listener.Update(10f);
                Assert.AreEqual(0, listener.Found.Count);
            }
            finally
            {
                listener.Dispose();
            }
        }
    }
}
