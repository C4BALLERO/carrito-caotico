using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace CarritoCaotico
{
    /// <summary>
    /// Puente entre la partida de esta escena y la <see cref="LanSession"/>.
    /// Anfitrión: simula todo, aplica los controles del jugador remoto y manda ~50 instantáneas por
    /// segundo (carrito, mano, productos que se mueven, rivales y obstáculos) más los eventos
    /// importantes por el canal fiable.
    /// Cliente: desactiva la física y las reglas locales, interpola las instantáneas con un pequeño
    /// retraso para que el movimiento sea suave y manda los controles de su jugador. La mano del
    /// Agarrador del cliente se mueve en su PC sin esperar a la red.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class LanSync : MonoBehaviour
    {
        const float SnapshotInterval = 0.02f;
        const float InputInterval = 1f / 60f;
        const float InterpolationDelay = 0.07f;
        const float RecentMoveWindow = 0.6f;
        const int RefreshPerSnapshot = 8;
        const float InputTimeout = 0.5f;

        public static LanSync Current { get; private set; }

        GameManager manager;
        LanSession session;
        CartController cart;
        Rigidbody cartBody;
        GrabberArm grabber;
        CameraController cameraRig;
        ShoppingList list;
        CartContents contents;
        GameTimer timer;
        RivalManager rivals;
        Checkout checkout;
        Product[] products = Array.Empty<Product>();
        MovingObstacle[] obstacles = Array.Empty<MovingObstacle>();
        readonly Dictionary<Product, int> productIndex = new Dictionary<Product, int>();

        readonly NetWriter writer = new NetWriter(4096);
        readonly SnapshotMessage snapshot = new SnapshotMessage();
        readonly Dictionary<ProductType, int> countBuffer = new Dictionary<ProductType, int>();
        bool isHost;
        float nextSnapshot;
        float nextInput;

        // Anfitrión: qué productos cambiaron hace poco.
        Vector3[] sentPositions;
        Quaternion[] sentRotations;
        byte[] sentFlags;
        float[] lastChange;
        int refreshCursor;
        float lastInputTime = -10f;

        // Cliente: interpolación.
        Track cartTrack;
        Track handTrack;
        Track[] productTracks;
        Track[] obstacleTracks;
        readonly Dictionary<int, Replica> replicas = new Dictionary<int, Replica>();
        readonly List<int> replicaRemovals = new List<int>();
        readonly HashSet<int> seenRivals = new HashSet<int>();
        float clockOffset;
        bool clockReady;
        int heldIndex = -1;

        public bool IsHost => isHost;
        public int ProductCount => products.Length;
        public int SnapshotsReceived { get; private set; }
        public int SnapshotsSent { get; private set; }
        /// <summary>Último tamaño de instantánea enviada, en bytes.</summary>
        public int LastSnapshotBytes { get; private set; }

        sealed class Replica
        {
            public RivalShopper Rival;
            public readonly Track Track = new Track();
        }

        // ---------- Arranque ----------

        /// <summary>Conecta la partida de esta escena con la sesión LAN.</summary>
        public static LanSync Attach(GameManager manager, LanSession session)
        {
            if (manager == null || session == null) return null;
            if (Current != null) Destroy(Current);
            var sync = manager.gameObject.AddComponent<LanSync>();
            sync.Init(manager, session);
            return sync;
        }

        void Init(GameManager owner, LanSession lanSession)
        {
            Current = this;
            manager = owner;
            session = lanSession;
            isHost = session.IsHost;
            cart = manager.Cart;
            cartBody = cart != null ? cart.Body : null;
            grabber = manager.Grabber;
            cameraRig = manager.CameraRig;
            list = manager.List;
            contents = manager.Contents;
            timer = manager.Timer;
            rivals = manager.Rivals;
            checkout = FindAnyObjectByType<Checkout>();

            products = SortByHierarchy(FindObjectsByType<Product>(FindObjectsInactive.Include));
            obstacles = SortByHierarchy(FindObjectsByType<MovingObstacle>(FindObjectsInactive.Include));
            for (int i = 0; i < products.Length; i++) productIndex[products[i]] = i;

            session.MessageReceived += OnMessage;
            session.Disconnected += OnDisconnected;
            manager.LevelConfigured += OnLevelConfigured;

            if (isHost) InitHost();
            else InitClient();
            ApplyRoles();
        }

        void InitHost()
        {
            int count = products.Length;
            sentPositions = new Vector3[count];
            sentRotations = new Quaternion[count];
            sentFlags = new byte[count];
            lastChange = new float[count];
            for (int i = 0; i < count; i++)
            {
                sentPositions[i] = products[i].transform.position;
                sentRotations[i] = products[i].transform.rotation;
                lastChange[i] = -10f;
            }

            manager.StateChanged += OnHostStateChanged;
            manager.GameEnded += OnHostGameEnded;
            manager.PauseChanged += OnHostPauseChanged;
            if (rivals != null) rivals.ProductTaken += OnHostProductTaken;
            if (checkout != null) checkout.EmptyCartRejected += OnHostEmptyCart;
        }

        void InitClient()
        {
            // El cliente no simula: todo se mueve con lo que manda el anfitrión.
            if (cart != null) cart.enabled = false;
            if (cartBody != null)
            {
                cartBody.isKinematic = true;
                cartBody.interpolation = RigidbodyInterpolation.None;
            }
            if (contents != null && contents.TryGetComponent(out Collider basket)) basket.enabled = false;
            if (checkout != null && checkout.TryGetComponent(out Collider zone)) zone.enabled = false;
            if (rivals != null) rivals.enabled = false;
            if (grabber != null) grabber.SetLocalActionsEnabled(false);
            if (list != null) list.SetRemoteCounts(Array.Empty<(ProductType, int)>());

            foreach (MovingObstacle obstacle in obstacles)
            {
                obstacle.enabled = false;
                if (obstacle.TryGetComponent(out Rigidbody body)) body.interpolation = RigidbodyInterpolation.None;
            }
            foreach (Product product in products)
            {
                if (product.Body == null) continue;
                product.Body.isKinematic = true;
                product.Body.interpolation = RigidbodyInterpolation.None;
            }

            cartTrack = new Track();
            handTrack = new Track();
            productTracks = new Track[products.Length];
            for (int i = 0; i < productTracks.Length; i++) productTracks[i] = new Track();
            obstacleTracks = new Track[obstacles.Length];
            for (int i = 0; i < obstacleTracks.Length; i++) obstacleTracks[i] = new Track();

            session.SendSceneReady();
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
            if (session != null)
            {
                session.MessageReceived -= OnMessage;
                session.Disconnected -= OnDisconnected;
            }
            if (manager != null)
            {
                manager.LevelConfigured -= OnLevelConfigured;
                manager.StateChanged -= OnHostStateChanged;
                manager.GameEnded -= OnHostGameEnded;
                manager.PauseChanged -= OnHostPauseChanged;
            }
            if (rivals != null) rivals.ProductTaken -= OnHostProductTaken;
            if (checkout != null) checkout.EmptyCartRejected -= OnHostEmptyCart;
            GameInput.SetRemotePusher(0f, 0f, false);
        }

        /// <summary>Orden estable en los dos PC: el de la jerarquía de la escena (igual en ambos).</summary>
        static T[] SortByHierarchy<T>(T[] items) where T : Component
        {
            var keyed = new List<(string key, T item)>(items.Length);
            foreach (T item in items) keyed.Add((HierarchyKey(item.transform), item));
            keyed.Sort((a, b) => string.CompareOrdinal(a.key, b.key));
            var sorted = new T[keyed.Count];
            for (int i = 0; i < sorted.Length; i++) sorted[i] = keyed[i].item;
            return sorted;
        }

        static string HierarchyKey(Transform t)
        {
            var indices = new List<int>();
            for (Transform current = t; current != null; current = current.parent) indices.Add(current.GetSiblingIndex());
            var builder = new StringBuilder(indices.Count * 5);
            for (int i = indices.Count - 1; i >= 0; i--) builder.Append(indices[i].ToString("D5")).Append('/');
            return builder.ToString();
        }

        /// <summary>Cámara según el rol de este PC: primera persona solo para el Empujador.</summary>
        void ApplyRoles()
        {
            if (cameraRig != null) cameraRig.SetFirstPersonAllowed(GameInput.LanRole == NetRole.Empujador);
        }

        void OnLevelConfigured(LevelConfig config)
        {
            // Con Intercambiado los PC cambian de puesto en este nivel.
            ApplyRoles();
            if (isHost && list != null)
            {
                writer.Clear();
                LevelSetupMessage.From(config, list.Required, manager.PreparationRemaining).Write(writer);
                session.Send(writer, true);
            }
        }

        void OnDisconnected(string reason)
        {
            session = null;
            if (manager == null) return;
            if (manager.State != GameState.Menu || manager.IsAwaitingNetwork) manager.HandleLanLost();
        }

        /// <summary>Cliente: pide algo al anfitrión (reintentar, siguiente nivel, menú, pausa).</summary>
        public static void SendRequest(LanRequest request)
        {
            LanSync sync = Current;
            if (sync == null || sync.session == null) return;
            sync.writer.Clear();
            sync.writer.WriteByte((byte)LanMessage.Request);
            sync.writer.WriteByte((byte)request);
            sync.session.Send(sync.writer, true);
        }

        // ---------- Mensajes ----------

        void OnMessage(LanMessage type, NetReader reader)
        {
            if (isHost) HandleHostMessage(type, reader);
            else HandleClientMessage(type, reader);
        }

        void HandleHostMessage(LanMessage type, NetReader reader)
        {
            NetRole remote = NetRoles.Other(GameInput.LanRole);
            switch (type)
            {
                case LanMessage.Input:
                {
                    InputMessage input = InputMessage.Read(reader);
                    // Durante un cambio de puesto pueden llegar controles del rol anterior.
                    if (input.Role != remote) return;
                    lastInputTime = Time.unscaledTime;
                    if (remote == NetRole.Empujador) GameInput.SetRemotePusher(input.Throttle, input.Steer, input.Drift);
                    else if (grabber != null && grabber.InputEnabled)
                        grabber.SetHandLocalTarget(new Vector3(input.HandX, 0f, input.HandZ), false);
                    break;
                }
                case LanMessage.Grab:
                {
                    GrabMessage grab = GrabMessage.Read(reader);
                    if (remote != NetRole.Agarrador || grabber == null || !grabber.InputEnabled) return;
                    // Se agarra donde está la mano en el PC del Agarrador.
                    grabber.SetHandLocalTarget(new Vector3(grab.HandX, 0f, grab.HandZ), true);
                    if (grab.Action == LanGrabAction.Throw) grabber.ThrowHeld();
                    else if (grabber.HeldProduct != null) grabber.DropHeld();
                    else grabber.TryGrabNearest();
                    break;
                }
                case LanMessage.Request:
                    HandleRequest((LanRequest)reader.ReadByte());
                    break;
            }
        }

        void HandleRequest(LanRequest request)
        {
            bool inLevel = manager.State == GameState.Preparacion || manager.State == GameState.EnJuego;
            switch (request)
            {
                case LanRequest.Restart:
                    if (inLevel || manager.IsGameOver) manager.Restart();
                    break;
                case LanRequest.NextLevel:
                    if (manager.CanAdvance) manager.NextLevel();
                    break;
                case LanRequest.Menu:
                    if (manager.State != GameState.Menu) manager.GoToMenu();
                    break;
                case LanRequest.TogglePause:
                    if (inLevel) manager.SetPaused(!manager.IsPaused);
                    break;
                case LanRequest.Resume:
                    manager.SetPaused(false);
                    break;
            }
        }

        void HandleClientMessage(LanMessage type, NetReader reader)
        {
            switch (type)
            {
                case LanMessage.LevelSetup:
                {
                    LevelSetupMessage setup = LevelSetupMessage.Read(reader);
                    manager.ApplyRemoteLevel(setup.ToConfig(), setup.Items, setup.Preparation);
                    break;
                }
                case LanMessage.State:
                    manager.ApplyRemoteState((GameState)reader.ReadByte());
                    break;
                case LanMessage.GameEnded:
                {
                    GameEndedMessage ended = GameEndedMessage.Read(reader);
                    manager.ApplyRemoteEnd(ended.FinalState, ended.ToReceipt(list != null ? list.Required : null));
                    break;
                }
                case LanMessage.Pause:
                    manager.ApplyRemotePause(reader.ReadBool());
                    break;
                case LanMessage.Notice:
                {
                    var notice = (LanNotice)reader.ReadByte();
                    int index = reader.ReadUShort();
                    if (notice == LanNotice.RivalTookProduct && rivals != null && index < products.Length) rivals.RaiseRemoteTaken(products[index]);
                    else if (notice == LanNotice.EmptyCart && checkout != null) checkout.RaiseRemoteEmptyCart();
                    break;
                }
                case LanMessage.Snapshot:
                    snapshot.Read(reader);
                    if (!reader.Failed) ApplySnapshot();
                    break;
            }
        }

        // ---------- Anfitrión: eventos ----------

        void OnHostStateChanged(GameState state)
        {
            // El fin de partida va con el recibo (GameEnded).
            if (state == GameState.Menu || manager.IsGameOver) return;
            writer.Clear();
            writer.WriteByte((byte)LanMessage.State);
            writer.WriteByte((byte)state);
            session?.Send(writer, true);
        }

        void OnHostGameEnded(ShoppingReceipt receipt)
        {
            if (receipt == null) return;
            var message = new GameEndedMessage
            {
                FinalState = manager.State,
                Elapsed = receipt.ElapsedSeconds,
                Remaining = receipt.RemainingSeconds
            };
            foreach (ReceiptLine line in receipt.Lines)
                if (line.Delivered > 0) message.Delivered.Add((line.Type, line.Delivered));
            writer.Clear();
            message.Write(writer);
            session?.Send(writer, true);
        }

        void OnHostPauseChanged(bool paused)
        {
            writer.Clear();
            writer.WriteByte((byte)LanMessage.Pause);
            writer.WriteBool(paused);
            session?.Send(writer, true);
        }

        void OnHostProductTaken(Product product) => SendNotice(LanNotice.RivalTookProduct, product);

        void OnHostEmptyCart() => SendNotice(LanNotice.EmptyCart, null);

        void SendNotice(LanNotice notice, Product product)
        {
            writer.Clear();
            writer.WriteByte((byte)LanMessage.Notice);
            writer.WriteByte((byte)notice);
            writer.WriteUShort(product != null && productIndex.TryGetValue(product, out int index) ? (ushort)index : (ushort)0);
            session?.Send(writer, true);
        }

        // ---------- Bucle ----------

        void Update()
        {
            if (session == null) return;
            if (isHost) UpdateHost();
            else UpdateClient();
        }

        void LateUpdate()
        {
            if (session == null || isHost) return;
            // La mano local va por delante de la red: el producto sostenido la sigue aquí.
            if (GameInput.LanRole == NetRole.Agarrador && heldIndex >= 0 && heldIndex < products.Length && grabber != null && grabber.GrabPoint != null)
                products[heldIndex].transform.position = grabber.GrabPoint.position;
        }

        void UpdateHost()
        {
            if (Time.unscaledTime - lastInputTime > InputTimeout) GameInput.SetRemotePusher(0f, 0f, false);
            if (manager.State == GameState.Menu || Time.unscaledTime < nextSnapshot) return;
            nextSnapshot = Time.unscaledTime + SnapshotInterval;
            SendSnapshot();
        }

        void SendSnapshot()
        {
            SnapshotMessage s = snapshot;
            s.Clear();
            float now = Time.unscaledTime;
            s.HostTime = now;
            s.State = manager.State;
            s.Paused = manager.IsPaused;
            s.Preparation = manager.PreparationRemaining;
            s.TimerRemaining = timer != null ? timer.Remaining : 0f;
            s.TimerRunning = timer != null && timer.IsRunning;
            s.RivalsActive = rivals != null ? rivals.ActiveCount : 0;
            s.RivalsTaken = rivals != null ? rivals.TakenCount : 0;
            if (contents != null)
            {
                contents.FillCounts(countBuffer);
                s.Contents.AddRange(LanProtocol.ToList(countBuffer));
            }

            if (cart != null)
            {
                s.CartPosition = cart.transform.position;
                s.CartYaw = cart.transform.eulerAngles.y;
            }
            if (grabber != null)
            {
                Vector3 hand = grabber.HandLocalPosition;
                s.HandX = hand.x;
                s.HandZ = hand.z;
                s.HeldIndex = grabber.HeldProduct != null && productIndex.TryGetValue(grabber.HeldProduct, out int held) ? held : -1;
            }

            int count = products.Length;
            for (int i = 0; i < count; i++)
            {
                Product product = products[i];
                if (product == null) continue;
                Transform t = product.transform;
                Vector3 position = t.position;
                Quaternion rotation = t.rotation;
                byte flags = (byte)((product.IsHeld && !product.IsTakenByRival ? ProductState.HeldFlag : 0)
                                    | (product.IsTakenByRival ? ProductState.TakenFlag : 0));
                bool moved = (position - sentPositions[i]).sqrMagnitude > 1e-6f
                             || Quaternion.Angle(rotation, sentRotations[i]) > 0.3f
                             || flags != sentFlags[i];
                if (moved)
                {
                    lastChange[i] = now;
                    sentPositions[i] = position;
                    sentRotations[i] = rotation;
                    sentFlags[i] = flags;
                }
                // Repaso periódico: corrige al cliente aunque se pierda algún paquete.
                bool refresh = count > 0 && ((i - refreshCursor + count) % count) < RefreshPerSnapshot;
                if (moved || now - lastChange[i] < RecentMoveWindow || refresh)
                    s.Products.Add(new ProductState { Index = (ushort)i, Position = position, Rotation = rotation, Flags = flags });
            }
            if (count > 0) refreshCursor = (refreshCursor + RefreshPerSnapshot) % count;

            if (rivals != null)
            {
                foreach (RivalShopper rival in rivals.Rivals)
                {
                    if (rival == null) continue;
                    s.Rivals.Add(new RivalState
                    {
                        Id = (ushort)rival.NetId,
                        Position = rival.transform.position,
                        Yaw = rival.transform.eulerAngles.y,
                        Color = rival.TintColor
                    });
                }
            }

            for (int i = 0; i < obstacles.Length; i++)
            {
                if (obstacles[i] == null) continue;
                Transform t = obstacles[i].transform;
                s.Obstacles.Add(new ObstacleState { Index = (ushort)i, Position = t.position, Yaw = t.eulerAngles.y });
            }

            writer.Clear();
            s.Write(writer);
            if (session.Send(writer, false))
            {
                SnapshotsSent++;
                LastSnapshotBytes = writer.Length;
            }
        }

        void UpdateClient()
        {
            float now = Time.unscaledTime;
            if (clockReady) ApplyInterpolatedWorld(now + clockOffset - InterpolationDelay);

            NetRole role = GameInput.LanRole;
            bool playing = manager.State == GameState.EnJuego && !manager.IsPaused;

            if (now >= nextInput)
            {
                nextInput = now + InputInterval;
                var input = new InputMessage { Role = role };
                if (role == NetRole.Empujador && playing)
                {
                    input.Throttle = GameInput.PusherThrottle();
                    input.Steer = GameInput.PusherSteer();
                    input.Drift = GameInput.PusherDrift();
                }
                if (role == NetRole.Agarrador && grabber != null)
                {
                    Vector3 target = grabber.HandLocalTarget;
                    input.HandX = target.x;
                    input.HandZ = target.z;
                }
                writer.Clear();
                input.Write(writer);
                session.Send(writer, false);
            }

            if (role == NetRole.Agarrador && grabber != null && grabber.InputEnabled && playing)
            {
                if (GameInput.GrabberGrabPressed()) SendGrab(LanGrabAction.Toggle);
                else if (GameInput.GrabberThrowPressed()) SendGrab(LanGrabAction.Throw);
            }
        }

        void SendGrab(LanGrabAction action)
        {
            Vector3 hand = grabber.HandLocalTarget;
            writer.Clear();
            new GrabMessage { Action = action, HandX = hand.x, HandZ = hand.z }.Write(writer);
            session.Send(writer, true);
        }

        // ---------- Cliente: instantáneas ----------

        void ApplySnapshot()
        {
            SnapshotMessage s = snapshot;
            float sample = s.HostTime - Time.unscaledTime;
            if (!clockReady || Mathf.Abs(sample - clockOffset) > 0.3f)
            {
                clockOffset = sample;
                clockReady = true;
            }
            else
            {
                clockOffset = Mathf.Lerp(clockOffset, sample, 0.05f);
            }

            float time = s.HostTime;
            cartTrack.Push(time, s.CartPosition, Quaternion.Euler(0f, s.CartYaw, 0f));
            handTrack.Push(time, new Vector3(s.HandX, 0f, s.HandZ), Quaternion.identity);
            heldIndex = s.HeldIndex;

            foreach (ProductState product in s.Products)
                if (product.Index < productTracks.Length) productTracks[product.Index].Push(time, product.Position, product.Rotation);

            seenRivals.Clear();
            foreach (RivalState rival in s.Rivals)
            {
                seenRivals.Add(rival.Id);
                if (!replicas.TryGetValue(rival.Id, out Replica replica))
                {
                    replica = CreateReplica(rival);
                    if (replica == null) continue;
                    replicas[rival.Id] = replica;
                }
                replica.Track.Push(time, rival.Position, Quaternion.Euler(0f, rival.Yaw, 0f));
            }
            replicaRemovals.Clear();
            foreach (var pair in replicas)
                if (!seenRivals.Contains(pair.Key)) replicaRemovals.Add(pair.Key);
            foreach (int id in replicaRemovals)
            {
                if (replicas[id].Rival != null) Destroy(replicas[id].Rival.gameObject);
                replicas.Remove(id);
            }

            foreach (ObstacleState obstacle in s.Obstacles)
                if (obstacle.Index < obstacleTracks.Length) obstacleTracks[obstacle.Index].Push(time, obstacle.Position, Quaternion.Euler(0f, obstacle.Yaw, 0f));

            manager.ApplyRemotePreparation(s.Preparation);
            if (timer != null) timer.ApplyRemote(s.TimerRemaining, s.TimerRunning && !s.Paused);
            if (list != null) list.SetRemoteCounts(s.Contents);
            if (rivals != null) rivals.ApplyRemoteCounts(s.RivalsActive, s.RivalsTaken);
            SnapshotsReceived++;
        }

        Replica CreateReplica(RivalState state)
        {
            if (rivals == null || rivals.Prefab == null) return null;
            RivalShopper rival = Instantiate(rivals.Prefab, state.Position, Quaternion.Euler(0f, state.Yaw, 0f));
            rival.name = $"Comprador_Rival_{state.Id}";
            rival.InitReplica(state.Color);
            if (rival.TryGetComponent(out Rigidbody body)) body.interpolation = RigidbodyInterpolation.None;
            return new Replica { Rival = rival };
        }

        void ApplyInterpolatedWorld(float renderTime)
        {
            if (cart != null && cartTrack.Valid)
            {
                cartTrack.Sample(renderTime, out Vector3 position, out Quaternion rotation);
                cart.transform.SetPositionAndRotation(position, rotation);
            }

            if (grabber != null)
            {
                Product held = heldIndex >= 0 && heldIndex < products.Length ? products[heldIndex] : null;
                grabber.SetDisplayHeld(held);
                // El Empujador ve la mano que mueve el Agarrador del otro PC.
                if (GameInput.LanRole != NetRole.Agarrador && handTrack.Valid)
                {
                    handTrack.Sample(renderTime, out Vector3 hand, out _);
                    grabber.SetHandLocalTarget(hand, true);
                }
            }

            for (int i = 0; i < productTracks.Length; i++)
            {
                Track track = productTracks[i];
                if (!track.Valid || !track.IsMovingAt(renderTime)) continue;
                track.Sample(renderTime, out Vector3 position, out Quaternion rotation);
                products[i].transform.SetPositionAndRotation(position, rotation);
            }

            foreach (Replica replica in replicas.Values)
            {
                if (replica.Rival == null || !replica.Track.Valid) continue;
                replica.Track.Sample(renderTime, out Vector3 position, out Quaternion rotation);
                replica.Rival.transform.SetPositionAndRotation(position, rotation);
            }

            for (int i = 0; i < obstacleTracks.Length; i++)
            {
                if (!obstacleTracks[i].Valid || obstacles[i] == null) continue;
                obstacleTracks[i].Sample(renderTime, out Vector3 position, out Quaternion rotation);
                obstacles[i].transform.SetPositionAndRotation(position, rotation);
            }
        }

        /// <summary>Historial corto de posiciones con hora del anfitrión; se interpola entre dos muestras.</summary>
        sealed class Track
        {
            const int Size = 8;
            readonly float[] times = new float[Size];
            readonly Vector3[] positions = new Vector3[Size];
            readonly Quaternion[] rotations = new Quaternion[Size];
            int head = -1;
            int count;

            public bool Valid => count > 0;

            public void Push(float time, Vector3 position, Quaternion rotation)
            {
                if (count > 0)
                {
                    float newest = times[head];
                    if (time <= newest) return; // paquete viejo o repetido
                    // Tras un rato sin datos (objeto quieto), se repite la última muestra para no deslizar.
                    if (time - newest > 0.25f) Add(time - SnapshotInterval, positions[head], rotations[head]);
                }
                Add(time, position, rotation);
            }

            void Add(float time, Vector3 position, Quaternion rotation)
            {
                head = (head + 1) % Size;
                times[head] = time;
                positions[head] = position;
                rotations[head] = rotation;
                if (count < Size) count++;
            }

            /// <summary>true mientras haya muestras más nuevas que la hora de dibujo (el objeto aún se mueve).</summary>
            public bool IsMovingAt(float renderTime) => count > 0 && times[head] >= renderTime - 0.5f;

            public void Sample(float time, out Vector3 position, out Quaternion rotation)
            {
                if (count == 1 || time >= times[head])
                {
                    position = positions[head];
                    rotation = rotations[head];
                    return;
                }
                for (int k = 0; k < count - 1; k++)
                {
                    int newer = (head - k + Size) % Size;
                    int older = (head - k - 1 + Size) % Size;
                    if (time < times[older]) continue;
                    float span = times[newer] - times[older];
                    float t = span > 1e-5f ? (time - times[older]) / span : 1f;
                    position = Vector3.Lerp(positions[older], positions[newer], t);
                    rotation = Quaternion.Slerp(rotations[older], rotations[newer], t);
                    return;
                }
                int oldest = (head - (count - 1) + Size) % Size;
                position = positions[oldest];
                rotation = rotations[oldest];
            }
        }
    }
}
