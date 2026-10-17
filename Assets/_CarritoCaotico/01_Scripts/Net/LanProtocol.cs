using System.Collections.Generic;
using UnityEngine;

namespace CarritoCaotico
{
    /// <summary>Rol del jugador de este PC en una partida LAN. Ninguno = los dos juegan en el mismo PC.</summary>
    public enum NetRole : byte
    {
        Ninguno = 0,
        Empujador = 1,
        Agarrador = 2
    }

    public static class NetRoles
    {
        public static NetRole Other(NetRole role) =>
            role == NetRole.Empujador ? NetRole.Agarrador : role == NetRole.Agarrador ? NetRole.Empujador : NetRole.Ninguno;

        public static string Name(NetRole role) =>
            role == NetRole.Empujador ? "Empujador" : role == NetRole.Agarrador ? "Agarrador" : "Local";
    }

    /// <summary>Tipos de mensaje. El primer byte de cada paquete.</summary>
    public enum LanMessage : byte
    {
        /// <summary>Cliente → anfitrión: versión del protocolo.</summary>
        Hello = 1,
        /// <summary>Anfitrión → cliente: versión y rol del anfitrión.</summary>
        Welcome = 2,
        /// <summary>Anfitrión → cliente: conexión rechazada (motivo).</summary>
        Reject = 3,
        /// <summary>Cierre ordenado.</summary>
        Bye = 4,
        /// <summary>Anfitrión → cliente: cargar una escena (nivel o menú).</summary>
        LoadScene = 10,
        /// <summary>Cliente → anfitrión: la escena pedida ya está cargada.</summary>
        SceneReady = 11,
        /// <summary>Anfitrión → cliente: nivel, esquema y lista de compras.</summary>
        LevelSetup = 12,
        /// <summary>Anfitrión → cliente: cambio de estado de la partida.</summary>
        State = 13,
        /// <summary>Anfitrión → cliente: fin de partida y datos del recibo.</summary>
        GameEnded = 14,
        /// <summary>Anfitrión → cliente: pausa activada o desactivada.</summary>
        Pause = 15,
        /// <summary>Anfitrión → cliente: aviso breve (rival, canasta vacía).</summary>
        Notice = 16,
        /// <summary>Anfitrión → cliente: estado del mundo (no fiable, ~50 por segundo).</summary>
        Snapshot = 20,
        /// <summary>Cliente → anfitrión: controles del jugador remoto (no fiable).</summary>
        Input = 21,
        /// <summary>Cliente → anfitrión: agarrar / soltar o lanzar.</summary>
        Grab = 22,
        /// <summary>Cliente → anfitrión: reintentar, siguiente nivel, menú o pausa.</summary>
        Request = 23
    }

    public enum LanRequest : byte
    {
        Restart = 1,
        NextLevel = 2,
        Menu = 3,
        TogglePause = 4,
        Resume = 5
    }

    public enum LanGrabAction : byte
    {
        /// <summary>Agarrar si la mano está vacía; soltar si sostiene algo.</summary>
        Toggle = 1,
        Throw = 2
    }

    public enum LanNotice : byte
    {
        RivalTookProduct = 1,
        EmptyCart = 2
    }

    public static class LanProtocol
    {
        /// <summary>Se sube al cambiar el formato de cualquier mensaje.</summary>
        public const int Version = 1;
        public const ushort DefaultPort = 7777;

        public static void WriteCounts(NetWriter writer, IReadOnlyList<(ProductType type, int count)> items)
        {
            int count = Mathf.Min(items.Count, 255);
            writer.WriteByte((byte)count);
            for (int i = 0; i < count; i++)
            {
                writer.WriteByte((byte)items[i].type);
                writer.WriteByte((byte)Mathf.Clamp(items[i].count, 0, 255));
            }
        }

        public static void ReadCounts(NetReader reader, List<(ProductType type, int count)> items)
        {
            items.Clear();
            int count = reader.ReadByte();
            for (int i = 0; i < count; i++)
            {
                var type = (ProductType)reader.ReadByte();
                int amount = reader.ReadByte();
                items.Add((type, amount));
            }
        }

        public static List<(ProductType type, int count)> ToList(IReadOnlyDictionary<ProductType, int> counts)
        {
            var list = new List<(ProductType, int)>();
            if (counts == null) return list;
            foreach (ProductType type in ProductCatalog.AllTypes)
                if (counts.TryGetValue(type, out int value) && value > 0) list.Add((type, value));
            return list;
        }

        public static Dictionary<ProductType, int> ToDictionary(IReadOnlyList<(ProductType type, int count)> items)
        {
            var counts = new Dictionary<ProductType, int>();
            foreach (var (type, count) in items)
                if (count > 0) counts[type] = count;
            return counts;
        }
    }

    public struct LoadSceneMessage
    {
        public int Sequence;
        public StoreId Store;
        public int Level;
        /// <summary>true: empezar el nivel al cargar. false: menú principal.</summary>
        public bool StartDirectly;
        public string Heading;

        public void Write(NetWriter writer)
        {
            writer.WriteByte((byte)LanMessage.LoadScene);
            writer.WriteInt(Sequence);
            writer.WriteByte((byte)Store);
            writer.WriteUShort((ushort)Mathf.Clamp(Level, 1, ushort.MaxValue));
            writer.WriteBool(StartDirectly);
            writer.WriteString(Heading);
        }

        public static LoadSceneMessage Read(NetReader reader) => new LoadSceneMessage
        {
            Sequence = reader.ReadInt(),
            Store = (StoreId)reader.ReadByte(),
            Level = reader.ReadUShort(),
            StartDirectly = reader.ReadBool(),
            Heading = reader.ReadString()
        };
    }

    public sealed class LevelSetupMessage
    {
        public int Level;
        public ControlScheme Scheme;
        public float Duration;
        public int MinUnits;
        public int MaxUnits;
        public int MaxPerType;
        public int RivalCount;
        public float Preparation;
        public readonly List<(ProductType type, int count)> Items = new List<(ProductType, int)>();

        public static LevelSetupMessage From(LevelConfig config, IReadOnlyDictionary<ProductType, int> required, float preparation)
        {
            var message = new LevelSetupMessage
            {
                Level = config.Level,
                Scheme = config.Scheme,
                Duration = config.Duration,
                MinUnits = config.MinUnits,
                MaxUnits = config.MaxUnits,
                MaxPerType = config.MaxPerType,
                RivalCount = config.RivalCount,
                Preparation = preparation
            };
            message.Items.AddRange(LanProtocol.ToList(required));
            return message;
        }

        public LevelConfig ToConfig() => new LevelConfig
        {
            Level = Level,
            Scheme = Scheme,
            Duration = Duration,
            MinUnits = MinUnits,
            MaxUnits = MaxUnits,
            MaxPerType = MaxPerType,
            RivalCount = RivalCount
        };

        public void Write(NetWriter writer)
        {
            writer.WriteByte((byte)LanMessage.LevelSetup);
            writer.WriteUShort((ushort)Mathf.Clamp(Level, 1, ushort.MaxValue));
            writer.WriteByte((byte)Scheme);
            writer.WriteFloat(Duration);
            writer.WriteByte((byte)MinUnits);
            writer.WriteByte((byte)MaxUnits);
            writer.WriteByte((byte)MaxPerType);
            writer.WriteByte((byte)RivalCount);
            writer.WriteFloat(Preparation);
            LanProtocol.WriteCounts(writer, Items);
        }

        public static LevelSetupMessage Read(NetReader reader)
        {
            var message = new LevelSetupMessage
            {
                Level = reader.ReadUShort(),
                Scheme = (ControlScheme)reader.ReadByte(),
                Duration = reader.ReadFloat(),
                MinUnits = reader.ReadByte(),
                MaxUnits = reader.ReadByte(),
                MaxPerType = reader.ReadByte(),
                RivalCount = reader.ReadByte(),
                Preparation = reader.ReadFloat()
            };
            LanProtocol.ReadCounts(reader, message.Items);
            return message;
        }
    }

    public sealed class GameEndedMessage
    {
        public GameState FinalState;
        public float Elapsed;
        public float Remaining;
        public readonly List<(ProductType type, int count)> Delivered = new List<(ProductType, int)>();

        public bool TimedOut => FinalState == GameState.TiempoAgotado;

        public void Write(NetWriter writer)
        {
            writer.WriteByte((byte)LanMessage.GameEnded);
            writer.WriteByte((byte)FinalState);
            writer.WriteFloat(Elapsed);
            writer.WriteFloat(Remaining);
            LanProtocol.WriteCounts(writer, Delivered);
        }

        public static GameEndedMessage Read(NetReader reader)
        {
            var message = new GameEndedMessage
            {
                FinalState = (GameState)reader.ReadByte(),
                Elapsed = reader.ReadFloat(),
                Remaining = reader.ReadFloat()
            };
            LanProtocol.ReadCounts(reader, message.Delivered);
            return message;
        }

        /// <summary>Recibo calculado con las mismas reglas que el anfitrión.</summary>
        public ShoppingReceipt ToReceipt(IReadOnlyDictionary<ProductType, int> required) =>
            TimedOut
                ? ShoppingRules.Evaluate(required, null, Elapsed, 0f, false)
                : ShoppingRules.Evaluate(required, LanProtocol.ToDictionary(Delivered), Elapsed, Remaining, true);
    }

    public struct InputMessage
    {
        public NetRole Role;
        public float Throttle;
        public float Steer;
        public bool Drift;
        /// <summary>Objetivo de la mano en el espacio local del carrito (el Agarrador remoto manda su mano).</summary>
        public float HandX;
        public float HandZ;

        public void Write(NetWriter writer)
        {
            writer.WriteByte((byte)LanMessage.Input);
            writer.WriteByte((byte)Role);
            writer.WriteFloat(Throttle);
            writer.WriteFloat(Steer);
            writer.WriteBool(Drift);
            writer.WriteFloat(HandX);
            writer.WriteFloat(HandZ);
        }

        public static InputMessage Read(NetReader reader) => new InputMessage
        {
            Role = (NetRole)reader.ReadByte(),
            Throttle = Mathf.Clamp(reader.ReadFloat(), -1f, 1f),
            Steer = Mathf.Clamp(reader.ReadFloat(), -1f, 1f),
            Drift = reader.ReadBool(),
            HandX = reader.ReadFloat(),
            HandZ = reader.ReadFloat()
        };
    }

    public struct GrabMessage
    {
        public LanGrabAction Action;
        public float HandX;
        public float HandZ;

        public void Write(NetWriter writer)
        {
            writer.WriteByte((byte)LanMessage.Grab);
            writer.WriteByte((byte)Action);
            writer.WriteFloat(HandX);
            writer.WriteFloat(HandZ);
        }

        public static GrabMessage Read(NetReader reader) => new GrabMessage
        {
            Action = (LanGrabAction)reader.ReadByte(),
            HandX = reader.ReadFloat(),
            HandZ = reader.ReadFloat()
        };
    }

    public struct ProductState
    {
        public const byte HeldFlag = 1;
        public const byte TakenFlag = 2;

        public ushort Index;
        public Vector3 Position;
        public Quaternion Rotation;
        public byte Flags;
    }

    public struct RivalState
    {
        public ushort Id;
        public Vector3 Position;
        public float Yaw;
        public Color32 Color;
    }

    public struct ObstacleState
    {
        public ushort Index;
        public Vector3 Position;
        public float Yaw;
    }

    /// <summary>
    /// Estado del mundo que el anfitrión manda al cliente. Los productos solo aparecen si se han
    /// movido hace poco o les toca el repaso periódico, así el paquete es pequeño casi siempre.
    /// </summary>
    public sealed class SnapshotMessage
    {
        public float HostTime;
        public GameState State;
        public bool Paused;
        public float Preparation;
        public float TimerRemaining;
        public bool TimerRunning;
        public int RivalsActive;
        public int RivalsTaken;
        public readonly List<(ProductType type, int count)> Contents = new List<(ProductType, int)>();
        public Vector3 CartPosition;
        public float CartYaw;
        public float HandX;
        public float HandZ;
        public int HeldIndex = -1;
        public readonly List<ProductState> Products = new List<ProductState>();
        public readonly List<RivalState> Rivals = new List<RivalState>();
        public readonly List<ObstacleState> Obstacles = new List<ObstacleState>();

        public void Clear()
        {
            Contents.Clear();
            Products.Clear();
            Rivals.Clear();
            Obstacles.Clear();
            HeldIndex = -1;
        }

        public void Write(NetWriter writer)
        {
            writer.WriteByte((byte)LanMessage.Snapshot);
            writer.WriteFloat(HostTime);
            writer.WriteByte((byte)State);
            writer.WriteBool(Paused);
            writer.WriteFloat(Preparation);
            writer.WriteFloat(TimerRemaining);
            writer.WriteBool(TimerRunning);
            writer.WriteByte((byte)Mathf.Clamp(RivalsActive, 0, 255));
            writer.WriteUShort((ushort)Mathf.Clamp(RivalsTaken, 0, ushort.MaxValue));
            LanProtocol.WriteCounts(writer, Contents);

            writer.WriteVector3(CartPosition);
            writer.WriteFloat(CartYaw);
            writer.WriteFloat(HandX);
            writer.WriteFloat(HandZ);
            writer.WriteShort((short)HeldIndex);

            writer.WriteUShort((ushort)Products.Count);
            foreach (ProductState product in Products)
            {
                writer.WriteUShort(product.Index);
                writer.WriteVector3(product.Position);
                writer.WriteRotation(product.Rotation);
                writer.WriteByte(product.Flags);
            }

            writer.WriteByte((byte)Mathf.Min(Rivals.Count, 255));
            for (int i = 0; i < Rivals.Count && i < 255; i++)
            {
                RivalState rival = Rivals[i];
                writer.WriteUShort(rival.Id);
                writer.WriteVector3(rival.Position);
                writer.WriteFloat(rival.Yaw);
                writer.WriteByte(rival.Color.r);
                writer.WriteByte(rival.Color.g);
                writer.WriteByte(rival.Color.b);
            }

            writer.WriteByte((byte)Mathf.Min(Obstacles.Count, 255));
            for (int i = 0; i < Obstacles.Count && i < 255; i++)
            {
                ObstacleState obstacle = Obstacles[i];
                writer.WriteUShort(obstacle.Index);
                writer.WriteVector3(obstacle.Position);
                writer.WriteFloat(obstacle.Yaw);
            }
        }

        /// <summary>Lee un paquete (sin el byte de tipo). Reutiliza las listas.</summary>
        public void Read(NetReader reader)
        {
            Clear();
            HostTime = reader.ReadFloat();
            State = (GameState)reader.ReadByte();
            Paused = reader.ReadBool();
            Preparation = reader.ReadFloat();
            TimerRemaining = reader.ReadFloat();
            TimerRunning = reader.ReadBool();
            RivalsActive = reader.ReadByte();
            RivalsTaken = reader.ReadUShort();
            LanProtocol.ReadCounts(reader, Contents);

            CartPosition = reader.ReadVector3();
            CartYaw = reader.ReadFloat();
            HandX = reader.ReadFloat();
            HandZ = reader.ReadFloat();
            HeldIndex = reader.ReadShort();

            int products = reader.ReadUShort();
            for (int i = 0; i < products && !reader.Failed; i++)
            {
                Products.Add(new ProductState
                {
                    Index = reader.ReadUShort(),
                    Position = reader.ReadVector3(),
                    Rotation = reader.ReadRotation(),
                    Flags = reader.ReadByte()
                });
            }

            int rivals = reader.ReadByte();
            for (int i = 0; i < rivals && !reader.Failed; i++)
            {
                Rivals.Add(new RivalState
                {
                    Id = reader.ReadUShort(),
                    Position = reader.ReadVector3(),
                    Yaw = reader.ReadFloat(),
                    Color = new Color32(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), 255)
                });
            }

            int obstacles = reader.ReadByte();
            for (int i = 0; i < obstacles && !reader.Failed; i++)
            {
                Obstacles.Add(new ObstacleState
                {
                    Index = reader.ReadUShort(),
                    Position = reader.ReadVector3(),
                    Yaw = reader.ReadFloat()
                });
            }
        }
    }
}
