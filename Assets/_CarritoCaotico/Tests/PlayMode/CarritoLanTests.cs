using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CarritoCaotico.Tests
{
    /// <summary>
    /// Juego en LAN por la red real (127.0.0.1): una sesión del juego y otra suelta que hace de
    /// otro PC. También la vista en primera persona del Empujador.
    /// </summary>
    public class CarritoLanTests
    {
        const string ScenePath = "Assets/_CarritoCaotico/00_Scenes/CarritoCaotico_Prueba.unity";

        LanSession other;
        Keyboard keyboard;
        CameraView previousView;
        InputSettings.EditorInputBehaviorInPlayMode previousBehavior;
        InputSettings.BackgroundBehavior previousBackground;

        [SetUp]
        public void SetUp()
        {
            previousView = CameraController.PreferredView;
            previousBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            previousBackground = InputSystem.settings.backgroundBehavior;
        }

        [TearDown]
        public void TearDown()
        {
            if (other != null) other.Close(null);
            other = null;
            LanSession.Shutdown();
            GameInput.ResetLan();
            GameInput.Scheme = ControlScheme.Normal;
            GameSession.ReturnToMenu();
            Time.timeScale = 1f;
            CameraController.PreferredView = previousView;
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            keyboard = null;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousBehavior;
            InputSystem.settings.backgroundBehavior = previousBackground;
        }

        static IEnumerator WaitUntil(System.Func<bool> condition, float timeout, string message)
        {
            float elapsed = 0f;
            while (!condition() && elapsed < timeout)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsTrue(condition(), message);
        }

        static void RequireScene()
        {
            if (!File.Exists(ScenePath)) Assert.Ignore("La escena de prueba no está generada.");
        }

        [UnityTest]
        public IEnumerator FirstPerson_OnlyForPusherAndHidesItsBody()
        {
            RequireScene();
            GameSession.SetSeed(1);
            GameSession.Begin(1, StoreId.Clasico);
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#endif
            yield return null;
            var cameraRig = Object.FindAnyObjectByType<CameraController>();
            var cart = Object.FindAnyObjectByType<CartController>();
            // Cuerpo del Empujador elegido (personaje con modelo 3D).
            Renderer head = Object.FindAnyObjectByType<CartCrew>().ActivePusher.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.IsNotNull(head);

            // Sin LAN (un solo PC) no hay primera persona.
            cameraRig.SetView(CameraView.PrimeraPersona);
            Assert.AreEqual(CameraView.TerceraPersona, cameraRig.View);

            cameraRig.SetFirstPersonAllowed(true);
            cameraRig.SetView(CameraView.PrimeraPersona);
            yield return null;
            Assert.AreEqual(CameraView.PrimeraPersona, cameraRig.View);
            Vector3 eye = cart.transform.TransformPoint(cameraRig.EyeOffset);
            Assert.Less(Vector3.Distance(cameraRig.transform.position, eye), 0.05f, "La cámara debería estar en los ojos del Empujador.");
            Assert.AreEqual(ShadowCastingMode.ShadowsOnly, head.shadowCastingMode, "En primera persona no se ve su cabeza.");
            Assert.AreEqual(CameraView.PrimeraPersona, CameraController.PreferredView, "La vista elegida se recuerda.");

            // El Agarrador siempre juega en tercera persona.
            cameraRig.SetFirstPersonAllowed(false);
            yield return null;
            Assert.AreEqual(CameraView.TerceraPersona, cameraRig.View);
            Assert.AreEqual(ShadowCastingMode.On, head.shadowCastingMode);
            Assert.Greater(Vector3.Distance(cameraRig.transform.position, cart.transform.position), 3f);
        }

        [UnityTest]
        public IEnumerator Host_RunsLevelAndObeysRemotePusher()
        {
            RequireScene();
            const ushort port = 47811;
            LanSession host = LanSession.Host(NetRole.Agarrador, port, true);
            other = LanSession.CreateDetached();
            other.StartClient("127.0.0.1", port);

            LevelSetupMessage setup = null;
            var snapshot = new SnapshotMessage();
            int snapshots = 0;
            var states = new List<GameState>();
            other.MessageReceived += (type, reader) =>
            {
                if (type == LanMessage.LevelSetup) setup = LevelSetupMessage.Read(reader);
                else if (type == LanMessage.State) states.Add((GameState)reader.ReadByte());
                else if (type == LanMessage.Snapshot)
                {
                    snapshot.Read(reader);
                    snapshots++;
                }
            };

            yield return WaitUntil(() => host.Status == LanSession.SessionStatus.Conectada && other.Status == LanSession.SessionStatus.Conectada, 5f, "Los dos PC deberían conectarse.");
            Assert.AreEqual(NetRole.Empujador, other.BaseRole, "El cliente toma el rol que no eligió el anfitrión.");
            Assert.AreEqual(NetRole.Agarrador, GameInput.LanRole);

            GameSession.SetSeed(1);
            GameSession.Begin(1, StoreId.Clasico);
            LanSync previous = LanSync.Current;
            host.HostLoad(ScenePath, StoreId.Clasico, "Prueba LAN");
            yield return WaitUntil(() => LanSync.Current != null && LanSync.Current != previous, 8f, "La escena debería cargarse con LanSync.");
            var manager = LanSync.Current.GetComponent<GameManager>();
            Assert.IsTrue(manager.IsAwaitingNetwork, "El anfitrión espera a que el otro PC cargue.");
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(GameState.Menu, manager.State);

            other.SendSceneReady();
            yield return WaitUntil(() => manager.State == GameState.EnJuego, 10f, "Con el otro PC listo empieza el nivel.");
            yield return WaitUntil(() => states.Count >= 2, 2f, "El cliente recibe los cambios de estado.");
            Assert.IsNotNull(setup, "El cliente recibe la lista.");
            CollectionAssert.AreEquivalent(LanProtocol.ToList(manager.List.Required), setup.Items);
            CollectionAssert.AreEqual(new[] { GameState.Preparacion, GameState.EnJuego }, states);
            Assert.Greater(snapshots, 10, "Llegan instantáneas.");

            // El Empujador del otro PC acelera.
            var cart = manager.Cart;
            Vector3 start = cart.transform.position;
            var writer = new NetWriter();
            float t = 0f;
            while (t < 1.5f)
            {
                writer.Clear();
                new InputMessage { Role = NetRole.Empujador, Throttle = 1f }.Write(writer);
                other.Send(writer, false);
                t += Time.deltaTime;
                yield return null;
            }
            float advanced = Vector3.Dot(cart.transform.position - start, cart.transform.forward);
            Assert.Greater(advanced, 1f, "El carrito del anfitrión obedece al Empujador remoto.");
            yield return new WaitForSeconds(0.2f);
            Assert.Less(Vector3.Distance(snapshot.CartPosition, cart.transform.position), 0.6f, "La instantánea sigue al carrito.");

            // Pausa pedida desde el otro PC.
            writer.Clear();
            writer.WriteByte((byte)LanMessage.Request);
            writer.WriteByte((byte)LanRequest.TogglePause);
            other.Send(writer, true);
            yield return WaitUntil(() => manager.IsPaused, 2f, "El otro PC puede pausar la partida.");
        }

        [UnityTest]
        public IEnumerator Host_GrabsAndThrowsForRemoteGrabber()
        {
            RequireScene();
            const ushort port = 47813;
            LanSession host = LanSession.Host(NetRole.Empujador, port, true);
            other = LanSession.CreateDetached();
            other.StartClient("127.0.0.1", port);
            yield return WaitUntil(() => host.Status == LanSession.SessionStatus.Conectada && other.Status == LanSession.SessionStatus.Conectada, 5f, "Los dos PC deberían conectarse.");
            Assert.AreEqual(NetRole.Agarrador, other.BaseRole);

            GameSession.SetSeed(1);
            GameSession.Begin(1, StoreId.Clasico);
            LanSync previous = LanSync.Current;
            host.HostLoad(ScenePath, StoreId.Clasico, "Prueba LAN");
            yield return WaitUntil(() => LanSync.Current != null && LanSync.Current != previous, 8f, "La escena debería cargarse.");
            other.SendSceneReady();
            var manager = LanSync.Current.GetComponent<GameManager>();
            yield return WaitUntil(() => manager.State == GameState.EnJuego, 10f, "Empieza el nivel.");

            // La mano del Agarrador remoto: a la derecha del carrito, con un producto justo ahí.
            GrabberArm grabber = manager.Grabber;
            var handTarget = new Vector3(1.5f, 0f, 1.2f);
            var writer = new NetWriter();
            for (int i = 0; i < 20; i++)
            {
                writer.Clear();
                new InputMessage { Role = NetRole.Agarrador, HandX = handTarget.x, HandZ = handTarget.z }.Write(writer);
                other.Send(writer, false);
                yield return null;
            }
            Assert.AreEqual(handTarget.x, grabber.HandLocalTarget.x, 0.01f, "El anfitrión mueve la mano adonde dice el otro PC.");

            Product product = Object.FindObjectsByType<Product>()[0];
            Vector3 handWorld = manager.Cart.transform.TransformPoint(new Vector3(handTarget.x, 1.05f, handTarget.z));
            product.Body.isKinematic = true;
            product.transform.position = handWorld;
            product.Body.position = handWorld;
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();

            writer.Clear();
            new GrabMessage { Action = LanGrabAction.Toggle, HandX = handTarget.x, HandZ = handTarget.z }.Write(writer);
            other.Send(writer, true);
            yield return WaitUntil(() => grabber.HeldProduct == product, 2f, "Agarrar llega por la red y se resuelve en el anfitrión.");

            writer.Clear();
            new GrabMessage { Action = LanGrabAction.Throw, HandX = handTarget.x, HandZ = handTarget.z }.Write(writer);
            other.Send(writer, true);
            yield return WaitUntil(() => grabber.HeldProduct == null, 2f, "Lanzar también.");
            Assert.IsFalse(product.IsHeld);
        }

        [UnityTest]
        public IEnumerator Client_MirrorsHostAndSendsItsControls()
        {
            RequireScene();
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            keyboard = InputSystem.AddDevice<Keyboard>("TecladoPruebaLAN");
            keyboard.MakeCurrent();

            const ushort port = 47812;
            other = LanSession.CreateDetached();
            Assert.IsTrue(other.StartHost(NetRole.Agarrador, port, true));
            var inputs = new List<InputMessage>();
            var requests = new List<LanRequest>();
            other.MessageReceived += (type, reader) =>
            {
                if (type == LanMessage.Input) inputs.Add(InputMessage.Read(reader));
                else if (type == LanMessage.Request) requests.Add((LanRequest)reader.ReadByte());
            };
            LanSession client = LanSession.Join("127.0.0.1", port);
            yield return WaitUntil(() => client.Status == LanSession.SessionStatus.Conectada, 5f, "El cliente debería conectarse.");
            Assert.AreEqual(NetRole.Empujador, GameInput.LanRole);

            // El anfitrión pide cargar el nivel 2.
            GameSession.Begin(2, StoreId.Clasico);
            other.HostLoad(ScenePath, StoreId.Clasico, "Prueba LAN");
            yield return WaitUntil(() => other.PeerReady, 8f, "El cliente confirma que cargó la escena.");
            var manager = LanSync.Current.GetComponent<GameManager>();
            Assert.IsTrue(manager.IsLanClient);
            Assert.IsTrue(manager.IsAwaitingNetwork);
            Assert.IsFalse(manager.Cart.enabled, "El cliente no simula el carrito.");
            Assert.IsTrue(manager.Cart.Body.isKinematic);

            var writer = new NetWriter();
            var setup = new LevelSetupMessage { Level = 2, Scheme = ControlScheme.Normal, Duration = 90f, MinUnits = 3, MaxUnits = 5, MaxPerType = 2, Preparation = 3f };
            setup.Items.Add((ProductType.Leche, 2));
            setup.Items.Add((ProductType.Pan, 1));
            setup.Write(writer);
            other.Send(writer, true);
            writer.Clear();
            writer.WriteByte((byte)LanMessage.State);
            writer.WriteByte((byte)GameState.EnJuego);
            other.Send(writer, true);
            yield return WaitUntil(() => manager.State == GameState.EnJuego, 3f, "El cliente sigue el estado del anfitrión.");

            Assert.AreEqual(2, manager.List.GetRequired(ProductType.Leche));
            Assert.AreEqual(2, manager.CurrentLevel);
            Assert.IsTrue(Object.FindAnyObjectByType<GameUI>().IsListHidden, "El PC del Empujador no ve la lista.");
            Assert.IsTrue(manager.CameraRig.FirstPersonAllowed, "El Empujador puede usar primera persona.");

            // Instantáneas: el carrito avanza 3 m y hay una leche en la canasta.
            Transform cartTransform = manager.Cart.transform;
            Vector3 target = cartTransform.position + cartTransform.forward * 3f;
            float yaw = cartTransform.eulerAngles.y;
            for (int i = 0; i < 15; i++)
            {
                var snapshot = new SnapshotMessage
                {
                    HostTime = Time.unscaledTime,
                    State = GameState.EnJuego,
                    TimerRemaining = 80f,
                    TimerRunning = true,
                    CartPosition = Vector3.Lerp(cartTransform.position, target, (i + 1) / 15f),
                    CartYaw = yaw
                };
                snapshot.Contents.Add((ProductType.Leche, 1));
                writer.Clear();
                snapshot.Write(writer);
                other.Send(writer, false);
                yield return new WaitForSeconds(0.02f);
            }
            yield return WaitUntil(() => Vector3.Distance(cartTransform.position, target) < 0.3f, 2f, "El carrito del cliente sigue las instantáneas.");
            Assert.AreEqual(1, manager.List.GetCollected(ProductType.Leche));
            Assert.AreEqual(80f, manager.Timer.Remaining, 0.5f);

            // El jugador del cliente pulsa W: el anfitrión recibe el acelerador.
            inputs.Clear();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return new WaitForSeconds(0.3f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Assert.IsTrue(inputs.Exists(m => m.Role == NetRole.Empujador && m.Throttle > 0.9f), "El acelerador llega al anfitrión.");

            // Fin de partida con recibo.
            var ended = new GameEndedMessage { FinalState = GameState.CompraEntregada, Elapsed = 30f, Remaining = 60f };
            ended.Delivered.Add((ProductType.Leche, 2));
            ended.Delivered.Add((ProductType.Pan, 1));
            writer.Clear();
            ended.Write(writer);
            other.Send(writer, true);
            yield return WaitUntil(() => manager.State == GameState.CompraEntregada, 2f, "El cliente muestra el final.");
            Assert.IsTrue(manager.LastReceipt.IsComplete);
            Assert.IsNotNull(GameObject.Find("Recibo"), "El cliente también ve el recibo.");

            // Reintentar en el cliente se lo pide al anfitrión.
            manager.Restart();
            yield return WaitUntil(() => requests.Contains(LanRequest.Restart), 2f, "Reintentar se pide al anfitrión.");
        }
    }
}
