using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;
using UnityEngine;

namespace CarritoCaotico
{
    /// <summary>
    /// Conexión LAN entre dos PC con Unity Transport. Un PC es el anfitrión: simula la física y las
    /// reglas. El otro es el cliente: manda los controles de su jugador y muestra el estado que le
    /// llega. Sobrevive a las cargas de escena. Los mensajes del juego los procesa <see cref="LanSync"/>.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class LanSession : MonoBehaviour
    {
        public enum SessionStatus
        {
            Inactiva,
            /// <summary>Anfitrión esperando a que se una el otro PC.</summary>
            Esperando,
            /// <summary>Cliente intentando conectar.</summary>
            Conectando,
            Conectada
        }

        const int FragmentCapacity = 32 * 1024;

        /// <summary>Sesión del juego. Las pruebas pueden crear otras sesiones sueltas.</summary>
        public static LanSession Instance { get; private set; }

        /// <summary>true si hay una partida LAN con los dos PC conectados.</summary>
        public static bool Active => Instance != null && Instance.Status == SessionStatus.Conectada;
        public static bool IsHostActive => Active && Instance.IsHost;
        public static bool IsClientActive => Active && !Instance.IsHost;

        /// <summary>Último aviso (desconexión o error) para mostrarlo en el menú.</summary>
        public static string LastNotice { get; set; } = "";

        /// <summary>Cambió el estado de la sesión del juego (o se cerró).</summary>
        public static event Action AnyStatusChanged;

        NetworkDriver driver;
        NetworkConnection connection;
        NetworkPipeline reliablePipe;
        NetworkPipeline unreliablePipe;
        readonly NetWriter writer = new NetWriter(256);
        readonly NetReader reader = new NetReader();
        byte[] receiveBuffer = new byte[2048];
        bool closing;
        LanDiscovery announcer;

        public SessionStatus Status { get; private set; } = SessionStatus.Inactiva;
        public bool IsHost { get; private set; }
        /// <summary>Rol de este PC con el esquema Normal (Intercambiado lo cambia).</summary>
        public NetRole BaseRole { get; private set; }
        public ushort Port { get; private set; }
        public string PeerAddress { get; private set; } = "";
        /// <summary>Sesión de pruebas: no carga escenas ni controla el juego.</summary>
        public bool IsDetached { get; private set; }

        /// <summary>Anfitrión: número de la última carga de escena pedida al cliente.</summary>
        public int LoadSequence { get; private set; }
        /// <summary>Anfitrión: última carga que el cliente confirmó.</summary>
        public int PeerReadySequence { get; private set; } = -1;
        public bool PeerReady => PeerReadySequence >= LoadSequence;
        /// <summary>Cliente: carga que se está esperando confirmar.</summary>
        public int PendingLoadSequence { get; private set; }

        /// <summary>Mensaje del juego recibido (el lector empieza después del tipo).</summary>
        public event Action<LanMessage, NetReader> MessageReceived;
        public event Action StatusChanged;
        /// <summary>La conexión se perdió o el otro PC salió. El texto explica el motivo.</summary>
        public event Action<string> Disconnected;

        // ---------- Crear y cerrar ----------

        /// <summary>Crea una partida y espera al otro PC.</summary>
        public static LanSession Host(NetRole role, ushort port = LanProtocol.DefaultPort, bool loopbackOnly = false)
        {
            LanSession session = Create(false);
            session.StartHost(role, port, loopbackOnly);
            return session;
        }

        /// <summary>Se une a la partida de otro PC por su IP.</summary>
        public static LanSession Join(string address, ushort port = LanProtocol.DefaultPort)
        {
            LanSession session = Create(false);
            session.StartClient(address, port);
            return session;
        }

        /// <summary>Sesión independiente para pruebas: no sustituye a <see cref="Instance"/>.</summary>
        public static LanSession CreateDetached() => Create(true);

        static LanSession Create(bool detached)
        {
            if (!detached && Instance != null) Instance.Close(null);
            var go = new GameObject(detached ? "Sesion_LAN_Pruebas" : "Sesion_LAN");
            DontDestroyOnLoad(go);
            var session = go.AddComponent<LanSession>();
            session.IsDetached = detached;
            if (!detached)
            {
                Instance = session;
                LastNotice = "";
            }
            return session;
        }

        public bool StartHost(NetRole role, ushort port = LanProtocol.DefaultPort, bool loopbackOnly = false)
        {
            CreateDriver();
            NetworkEndpoint endpoint = (loopbackOnly ? NetworkEndpoint.LoopbackIpv4 : NetworkEndpoint.AnyIpv4).WithPort(port);
            if (driver.Bind(endpoint) != 0 || driver.Listen() != 0)
            {
                Fail($"No se pudo abrir el puerto {port}. ¿Hay otra partida abierta en este PC?");
                return false;
            }
            IsHost = true;
            BaseRole = role == NetRole.Ninguno ? NetRole.Empujador : role;
            Port = port;
            // Se anuncia en la red para que el otro PC la encuentre sin escribir la IP.
            if (!loopbackOnly && !IsDetached) announcer = LanDiscovery.StartAnnouncing(BaseRole, port);
            SetStatus(SessionStatus.Esperando);
            return true;
        }

        public bool StartClient(string address, ushort port = LanProtocol.DefaultPort)
        {
            address = (address ?? "").Trim();
            if (address.Equals("localhost", StringComparison.OrdinalIgnoreCase)) address = "127.0.0.1";
            if (!NetworkEndpoint.TryParse(address, port, out NetworkEndpoint endpoint, NetworkFamily.Ipv4))
            {
                Fail($"«{address}» no es una IP válida. Ejemplo: 192.168.1.20");
                return false;
            }
            CreateDriver();
            connection = driver.Connect(endpoint);
            IsHost = false;
            Port = port;
            PeerAddress = address;
            SetStatus(SessionStatus.Conectando);
            return true;
        }

        void CreateDriver()
        {
            var settings = new NetworkSettings(Allocator.Temp);
            settings.WithNetworkConfigParameters(connectTimeoutMS: 1000, maxConnectAttempts: 10,
                disconnectTimeoutMS: 6000, heartbeatTimeoutMS: 500);
            settings.WithFragmentationStageParameters(payloadCapacity: FragmentCapacity);
            driver = NetworkDriver.Create(settings);
            reliablePipe = driver.CreatePipeline(typeof(FragmentationPipelineStage), typeof(ReliableSequencedPipelineStage));
            unreliablePipe = driver.CreatePipeline(typeof(FragmentationPipelineStage));
        }

        /// <summary>Cierra la sesión del juego (si la hay).</summary>
        public static void Shutdown(string notice = null)
        {
            if (Instance != null) Instance.Close(notice);
        }

        /// <summary>Cierra esta sesión avisando al otro PC.</summary>
        public void Close(string notice)
        {
            if (closing) return;
            closing = true;
            StopAnnouncing();
            if (driver.IsCreated)
            {
                if (connection.IsCreated)
                {
                    writer.Clear();
                    writer.WriteByte((byte)LanMessage.Bye);
                    Send(writer, true);
                    driver.ScheduleFlushSend().Complete();
                    driver.Disconnect(connection);
                    driver.ScheduleUpdate().Complete();
                }
                driver.Dispose();
            }
            connection = default;
            if (!IsDetached && notice != null) LastNotice = notice;
            Status = SessionStatus.Inactiva;
            if (Instance == this)
            {
                Instance = null;
                GameInput.ResetLan();
                AnyStatusChanged?.Invoke();
            }
            StatusChanged?.Invoke();
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (!closing) Close(null);
        }

        void OnApplicationQuit()
        {
            Close(null);
        }

        void Fail(string message)
        {
            Debug.LogWarning("[LAN] " + message);
            Close(message);
        }

        void StopAnnouncing()
        {
            announcer?.Dispose();
            announcer = null;
        }

        void SetStatus(SessionStatus status)
        {
            if (Status == status) return;
            Status = status;
            if (status == SessionStatus.Conectada) StopAnnouncing();
            if (Instance == this)
            {
                GameInput.LanBaseRole = status == SessionStatus.Conectada ? BaseRole : NetRole.Ninguno;
                AnyStatusChanged?.Invoke();
            }
            StatusChanged?.Invoke();
        }

        // ---------- Bucle de red ----------

        void Update()
        {
            announcer?.Update(Time.unscaledTime);
            Pump();
        }

        void LateUpdate()
        {
            // Envía lo encolado este frame sin esperar al siguiente.
            if (driver.IsCreated && !closing) driver.ScheduleFlushSend().Complete();
        }

        /// <summary>Procesa la red: conexiones nuevas y mensajes recibidos.</summary>
        public void Pump()
        {
            if (!driver.IsCreated || closing) return;
            driver.ScheduleUpdate().Complete();

            if (IsHost)
            {
                NetworkConnection incoming;
                while ((incoming = driver.Accept()) != default)
                {
                    // Solo dos jugadores: se rechaza a cualquier otro.
                    if (connection.IsCreated) driver.Disconnect(incoming);
                    else connection = incoming;
                }
            }

            while (!closing && connection.IsCreated)
            {
                NetworkEvent.Type type = driver.PopEventForConnection(connection, out DataStreamReader stream);
                if (type == NetworkEvent.Type.Empty) break;
                switch (type)
                {
                    case NetworkEvent.Type.Connect:
                        if (!IsHost) SendHello();
                        break;
                    case NetworkEvent.Type.Data:
                        Receive(ref stream);
                        break;
                    case NetworkEvent.Type.Disconnect:
                        connection = default;
                        Lost(Status == SessionStatus.Conectando
                            ? $"No se encontró ninguna partida en {PeerAddress}:{Port}.\n{ConnectionHelp}"
                            : "Se perdió la conexión con el otro PC.");
                        break;
                }
            }
        }

        void Receive(ref DataStreamReader stream)
        {
            int length = stream.Length;
            if (length <= 0) return;
            if (receiveBuffer.Length < length) receiveBuffer = new byte[Mathf.NextPowerOfTwo(length)];
            stream.ReadBytes(new Span<byte>(receiveBuffer, 0, length));
            reader.Reset(receiveBuffer, 0, length);
            var type = (LanMessage)reader.ReadByte();

            switch (type)
            {
                case LanMessage.Hello:
                    if (IsHost) HandleHello();
                    return;
                case LanMessage.Welcome:
                    if (!IsHost) HandleWelcome();
                    return;
                case LanMessage.Reject:
                    Lost(reader.ReadString());
                    return;
                case LanMessage.Bye:
                    Lost("El otro PC salió de la partida.");
                    return;
                case LanMessage.SceneReady:
                    if (IsHost) PeerReadySequence = Mathf.Max(PeerReadySequence, reader.ReadInt());
                    break;
                case LanMessage.LoadScene:
                    if (!IsHost) HandleLoadScene();
                    break;
            }
            MessageReceived?.Invoke(type, reader);
        }

        void SendHello()
        {
            writer.Clear();
            writer.WriteByte((byte)LanMessage.Hello);
            writer.WriteInt(LanProtocol.Version);
            Send(writer, true);
        }

        void HandleHello()
        {
            int version = reader.ReadInt();
            if (version != LanProtocol.Version)
            {
                writer.Clear();
                writer.WriteByte((byte)LanMessage.Reject);
                writer.WriteString($"Versiones distintas del juego (anfitrión {LanProtocol.Version}, tú {version}).");
                Send(writer, true);
                driver.ScheduleFlushSend().Complete();
                driver.Disconnect(connection);
                connection = default;
                return;
            }
            PeerAddress = RemoteAddress();
            writer.Clear();
            writer.WriteByte((byte)LanMessage.Welcome);
            writer.WriteInt(LanProtocol.Version);
            writer.WriteByte((byte)BaseRole);
            Send(writer, true);
            // El cliente acaba de llegar: está en el menú, como el anfitrión.
            LoadSequence = 0;
            PeerReadySequence = 0;
            SetStatus(SessionStatus.Conectada);
        }

        void HandleWelcome()
        {
            int version = reader.ReadInt();
            var hostRole = (NetRole)reader.ReadByte();
            if (version != LanProtocol.Version)
            {
                Lost($"Versiones distintas del juego (anfitrión {version}, tú {LanProtocol.Version}).");
                return;
            }
            BaseRole = NetRoles.Other(hostRole);
            SetStatus(SessionStatus.Conectada);
        }

        void HandleLoadScene()
        {
            LoadSceneMessage message = LoadSceneMessage.Read(reader);
            PendingLoadSequence = message.Sequence;
            if (IsDetached) return;
            if (message.StartDirectly) GameSession.Begin(message.Level, message.Store);
            else
            {
                GameSession.ReturnToMenu();
                GameSession.SelectStore(message.Store);
            }
            LoadingScreen.LoadStore(message.Store, message.Heading);
        }

        string RemoteAddress()
        {
            if (!connection.IsCreated) return "";
            NetworkEndpoint endpoint = driver.GetRemoteEndpoint(connection);
            string text = endpoint.ToString();
            int colon = text.LastIndexOf(':');
            return colon > 0 ? text.Substring(0, colon) : text;
        }

        void Lost(string reason)
        {
            if (closing) return;
            Debug.Log("[LAN] " + reason);
            Disconnected?.Invoke(reason);
            Close(reason);
        }

        // ---------- Envío ----------

        /// <summary>Envía el contenido del escritor. Los mensajes fiables llegan siempre y en orden.</summary>
        public bool Send(NetWriter message, bool reliable)
        {
            if (!driver.IsCreated || !connection.IsCreated || message.Length == 0) return false;
            NetworkPipeline pipe = reliable ? reliablePipe : unreliablePipe;
            int status = driver.BeginSend(pipe, connection, out DataStreamWriter stream, message.Length);
            if (status != 0)
            {
                if (reliable) Debug.LogWarning($"[LAN] No se pudo enviar un mensaje fiable (código {status}).");
                return false;
            }
            stream.WriteBytes(new Span<byte>(message.Buffer, 0, message.Length));
            if (stream.HasFailedWrites)
            {
                driver.AbortSend(stream);
                return false;
            }
            return driver.EndSend(stream) >= 0;
        }

        /// <summary>Anfitrión: pide al cliente que cargue la escena y la carga aquí también.</summary>
        public void HostLoad(string scenePath, StoreId store, string heading)
        {
            LoadSequence++;
            var message = new LoadSceneMessage
            {
                Sequence = LoadSequence,
                Store = store,
                Level = GameSession.CurrentLevel,
                StartDirectly = GameSession.StartDirectly,
                Heading = heading
            };
            writer.Clear();
            message.Write(writer);
            Send(writer, true);
            if (!IsDetached) LoadingScreen.Load(scenePath, store, heading);
        }

        /// <summary>Cliente: avisa al anfitrión de que la escena pedida ya está cargada.</summary>
        public void SendSceneReady()
        {
            writer.Clear();
            writer.WriteByte((byte)LanMessage.SceneReady);
            writer.WriteInt(PendingLoadSequence);
            Send(writer, true);
        }

        // ---------- Utilidades ----------

        /// <summary>IP de este PC en la red local, la más probable primero (para decírsela al otro jugador).</summary>
        public static List<string> LocalAddresses()
        {
            var result = new List<string>();
            foreach (LocalAddress address in LanDiscovery.LocalAddresses())
                if (!result.Contains(address.Address)) result.Add(address.Address);
            return result;
        }

        /// <summary>IP principal de este PC (la de la wifi o el cable de casa) o vacío si no hay red.</summary>
        public static string PrimaryAddress()
        {
            List<string> addresses = LocalAddresses();
            return addresses.Count > 0 ? addresses[0] : "";
        }

        /// <summary>Consejos cuando el otro PC no conecta.</summary>
        public const string ConnectionHelp =
            "Si no conecta: los dos PC en la misma wifi · en Windows, la wifi como «Red privada» · " +
            "permite Carrito Caótico (o Unity) en el Firewall de Windows para redes privadas.";

        // ---------- Línea de comandos ----------

        /// <summary>
        /// Arranque rápido para probar en un solo PC:
        /// -lanHost Empujador|Agarrador · -lanJoin 127.0.0.1 · -lanPort 7777 · -lanLocal (solo 127.0.0.1)
        /// · -lanPlay N (el anfitrión empieza el nivel N al conectarse) · -lanStore Clasico|Halloween.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void FromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            string Value(string name)
            {
                for (int i = 0; i < args.Length - 1; i++)
                    if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
                return null;
            }
            bool Flag(string name) => Array.Exists(args, a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));

            ushort port = ushort.TryParse(Value("-lanPort"), out ushort parsed) ? parsed : LanProtocol.DefaultPort;
            string host = Value("-lanHost");
            string join = Value("-lanJoin");
            if (host != null)
            {
                NetRole role = string.Equals(host, "Agarrador", StringComparison.OrdinalIgnoreCase) ? NetRole.Agarrador : NetRole.Empujador;
                LanSession session = Host(role, port, Flag("-lanLocal"));
                if (int.TryParse(Value("-lanPlay"), out int level) && level > 0)
                {
                    StoreId store = string.Equals(Value("-lanStore"), "Halloween", StringComparison.OrdinalIgnoreCase) ? StoreId.Halloween : StoreId.Clasico;
                    session.StatusChanged += () =>
                    {
                        if (session.Status != SessionStatus.Conectada) return;
                        GameManager manager = FindAnyObjectByType<GameManager>();
                        if (manager != null) manager.StartGame(level, store);
                    };
                }
            }
            else if (join != null)
            {
                Join(join, port);
            }
        }
    }
}
