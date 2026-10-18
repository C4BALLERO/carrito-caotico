using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace CarritoCaotico
{
    /// <summary>Partida LAN anunciada por un anfitrión de la misma red.</summary>
    public struct LanBeacon
    {
        public string Address;
        public ushort Port;
        public NetRole HostRole;
        public string HostName;
        public float SeenAt;
    }

    /// <summary>IP local con el nombre del adaptador (Wi-Fi, Ethernet…).</summary>
    public struct LocalAddress
    {
        public string Address;
        public string Adapter;
        public bool IsWireless;
        public int Score;
    }

    /// <summary>
    /// Descubrimiento de partidas en la red local: el anfitrión anuncia su partida por difusión UDP
    /// (puerto 47777) cada segundo y el otro PC la encuentra sin escribir la IP. Si la red bloquea
    /// la difusión, se puede seguir escribiendo la IP a mano.
    /// </summary>
    public sealed class LanDiscovery : IDisposable
    {
        public const ushort DiscoveryPort = 47777;
        const string Magic = "CARRITO";
        const float BeaconInterval = 1f;
        const float BeaconLifetime = 4f;

        UdpClient socket;
        readonly bool hosting;
        byte[] beacon;
        float nextBeacon;
        readonly Dictionary<string, LanBeacon> found = new Dictionary<string, LanBeacon>();
        readonly List<LanBeacon> foundList = new List<LanBeacon>();

        /// <summary>Partidas vistas en los últimos segundos, la más reciente primero.</summary>
        public IReadOnlyList<LanBeacon> Found => foundList;
        public bool IsRunning => socket != null;

        LanDiscovery(bool host)
        {
            hosting = host;
        }

        /// <summary>Anfitrión: anuncia la partida en la red.</summary>
        public static LanDiscovery StartAnnouncing(NetRole hostRole, ushort gamePort)
        {
            var discovery = new LanDiscovery(true) { beacon = BuildBeacon(hostRole, gamePort, Environment.MachineName) };
            try
            {
                discovery.socket = new UdpClient(AddressFamily.InterNetwork) { EnableBroadcast = true };
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[LAN] No se pudo anunciar la partida: " + exception.Message);
                discovery.socket = null;
            }
            return discovery;
        }

        /// <summary>Cliente: escucha los anuncios de partidas.</summary>
        public static LanDiscovery StartListening()
        {
            var discovery = new LanDiscovery(false);
            try
            {
                var udp = new UdpClient(AddressFamily.InterNetwork);
                udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udp.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));
                discovery.socket = udp;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[LAN] No se pueden buscar partidas en la red: " + exception.Message);
                discovery.socket = null;
            }
            return discovery;
        }

        /// <summary>Llamar cada frame: manda el anuncio o lee los recibidos.</summary>
        public void Update(float now)
        {
            if (socket == null) return;
            try
            {
                if (hosting)
                {
                    if (now < nextBeacon) return;
                    nextBeacon = now + BeaconInterval;
                    foreach (IPAddress target in BroadcastTargets())
                        socket.Send(beacon, beacon.Length, new IPEndPoint(target, DiscoveryPort));
                    return;
                }

                while (socket.Available > 0)
                {
                    var from = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = socket.Receive(ref from);
                    if (TryParseBeacon(data, from.Address.ToString(), now, out LanBeacon found))
                        this.found[found.Address] = found;
                }
                Refresh(now);
            }
            catch (SocketException exception)
            {
                Debug.LogWarning("[LAN] Error de descubrimiento: " + exception.Message);
            }
        }

        void Refresh(float now)
        {
            foundList.Clear();
            foreach (LanBeacon item in found.Values)
                if (now - item.SeenAt <= BeaconLifetime) foundList.Add(item);
            foundList.Sort((a, b) => b.SeenAt.CompareTo(a.SeenAt));
        }

        public void Dispose()
        {
            socket?.Close();
            socket = null;
        }

        // ---------- Formato del anuncio ----------

        public static byte[] BuildBeacon(NetRole hostRole, ushort gamePort, string hostName)
        {
            string name = string.IsNullOrEmpty(hostName) ? "PC" : hostName.Replace("|", " ");
            if (name.Length > 40) name = name.Substring(0, 40);
            return Encoding.UTF8.GetBytes($"{Magic}|{LanProtocol.Version}|{gamePort}|{(int)hostRole}|{name}");
        }

        /// <summary>Lee un anuncio. Ignora paquetes de otros programas o de otra versión del juego.</summary>
        public static bool TryParseBeacon(byte[] data, string senderAddress, float now, out LanBeacon beacon)
        {
            beacon = default;
            if (data == null || data.Length < Magic.Length || data.Length > 256) return false;
            string[] parts = Encoding.UTF8.GetString(data).Split('|');
            if (parts.Length != 5 || parts[0] != Magic) return false;
            if (!int.TryParse(parts[1], out int version) || version != LanProtocol.Version) return false;
            if (!ushort.TryParse(parts[2], out ushort port)) return false;
            if (!int.TryParse(parts[3], out int role) || role < 1 || role > 2) return false;
            beacon = new LanBeacon { Address = senderAddress, Port = port, HostRole = (NetRole)role, HostName = parts[4], SeenAt = now };
            return true;
        }

        // ---------- Direcciones locales ----------

        /// <summary>Difusión general y la de cada red local (más fiable con varios adaptadores).</summary>
        static IEnumerable<IPAddress> BroadcastTargets()
        {
            yield return IPAddress.Broadcast;
            foreach (NetworkInterface nic in SafeInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                foreach (UnicastIPAddressInformation info in nic.GetIPProperties().UnicastAddresses)
                {
                    if (info.Address.AddressFamily != AddressFamily.InterNetwork || info.IPv4Mask == null) continue;
                    byte[] ip = info.Address.GetAddressBytes();
                    byte[] mask = info.IPv4Mask.GetAddressBytes();
                    if (IPAddress.IsLoopback(info.Address)) continue;
                    var broadcast = new byte[4];
                    for (int i = 0; i < 4; i++) broadcast[i] = (byte)(ip[i] | ~mask[i]);
                    yield return new IPAddress(broadcast);
                }
            }
        }

        static NetworkInterface[] SafeInterfaces()
        {
            try
            {
                return NetworkInterface.GetAllNetworkInterfaces();
            }
            catch (Exception)
            {
                return Array.Empty<NetworkInterface>();
            }
        }

        /// <summary>
        /// IP de este PC ordenadas de más a menos probable para la wifi o el cable de casa: primero
        /// las de adaptadores reales con puerta de enlace; los virtuales (Hyper-V, VPN, Docker…) al final.
        /// </summary>
        public static List<LocalAddress> LocalAddresses()
        {
            var result = new List<LocalAddress>();
            foreach (NetworkInterface nic in SafeInterfaces())
            {
                try
                {
                    if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    IPInterfaceProperties properties = nic.GetIPProperties();
                    bool hasGateway = false;
                    foreach (GatewayIPAddressInformation gateway in properties.GatewayAddresses)
                        if (gateway.Address != null && !gateway.Address.Equals(IPAddress.Any)) hasGateway = true;

                    foreach (UnicastIPAddressInformation info in properties.UnicastAddresses)
                    {
                        if (info.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        string ip = info.Address.ToString();
                        if (ip.StartsWith("169.254.") || ip.StartsWith("127.")) continue;
                        bool wireless = nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211;
                        result.Add(new LocalAddress
                        {
                            Address = ip,
                            Adapter = wireless ? "Wi-Fi" : nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet ? "Cable" : nic.Name,
                            IsWireless = wireless,
                            Score = ScoreAddress(ip, nic.Name + " " + nic.Description, wireless, hasGateway)
                        });
                    }
                }
                catch (Exception)
                {
                    // Algunos adaptadores no dejan leer sus datos: se ignoran.
                }
            }
            result.Sort((a, b) => b.Score.CompareTo(a.Score));
            return result;
        }

        static readonly string[] VirtualHints = { "virtual", "hyper-v", "vethernet", "vmware", "virtualbox", "docker", "wsl", "tap", "tunnel", "vpn", "bluetooth", "loopback", "zerotier", "hamachi", "tailscale" };

        /// <summary>Puntuación de una IP local (más alto = más probable que sea la de la LAN de casa).</summary>
        public static int ScoreAddress(string ip, string adapterText, bool wireless, bool hasGateway)
        {
            int score = 0;
            string text = (adapterText ?? "").ToLowerInvariant();
            bool isVirtual = false;
            foreach (string hint in VirtualHints)
                if (text.Contains(hint)) isVirtual = true;
            if (isVirtual) score -= 100;
            if (hasGateway) score += 40;
            if (wireless) score += 10;
            if (ip.StartsWith("192.168.")) score += 30;
            else if (ip.StartsWith("10.")) score += 20;
            else if (IsPrivate172(ip)) score += 10;
            return score;
        }

        static bool IsPrivate172(string ip)
        {
            string[] parts = ip.Split('.');
            return parts.Length == 4 && parts[0] == "172" && int.TryParse(parts[1], out int second) && second >= 16 && second <= 31;
        }
    }
}
