using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;


#if UNITY_2017_1_OR_NEWER
using System;
using System.Threading;
using System.Collections.Generic;
#endif

namespace SimproNET
{
    /// <summary>
    /// Canal de mensaje de 
    /// </summary>
    public enum ENetChannel
    {
        /// <summary>
        /// Canal TCP confiable
        /// </summary>
        Reliable,
        /// <summary>
        /// Canal UDP no confiable
        /// </summary>
        Unreliable
    }

    /// <summary>
    /// Estado de la red
    /// </summary>
    public enum ENetworkState
    {
        /// <summary>
        /// Red cerrada
        /// </summary>
        Closed,
        /// <summary>
        /// Red iniciándose
        /// </summary>
        Startup,
        /// <summary>
        /// Red en funcionamiento
        /// </summary>
        Running,
        /// <summary>
        /// Red apagándose
        /// </summary>
        Shutdown
    }

    /// <summary>
    /// Flags de reporte de eventos de red
    /// </summary>
    [Flags]
    public enum ENetworkEventReportFlags : byte
    {
        /// <summary>
        /// Ninguno
        /// </summary>
        None = 0x00,
        /// <summary>
        /// Eventos de conexión/desconexión
        /// </summary>
        ConnectionStatus = 0x01,
        /// <summary>
        /// Eventos de recepción de mensajes
        /// </summary>
        ReceivedMessage = 0x02,
        /// <summary>
        /// Todos los eventos
        /// </summary>
        All = ConnectionStatus | ReceivedMessage
    }

    /// <summary>
    /// Manejador de conexión
    /// </summary>
    public struct ConnectionHandle
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="internalHandle"></param>
        public ConnectionHandle(ulong internalHandle)
        {
            Handle = internalHandle;
        }

        /// <summary>
        /// Obtiene el manejador interno
        /// </summary>
        /// <returns></returns>
        public ulong GetInternalHandle()
        {
            return Handle;
        }

        /// <summary>
        /// Indica si el manejador es válido
        /// </summary>
        /// <returns></returns>
        public bool IsValid()
        {
            return Handle != 0;
        }


        ulong Handle;
    }

    /// <summary>
    /// Evento de conexión o desconexión
    /// </summary>
    public struct ConnectionEvent
    {
        /// <summary>
        /// Tipo de evento
        /// </summary>
        public enum EType
        {
            /// <summary>
            /// Conectado
            /// </summary>
            Connected,
            /// <summary>
            /// Desconectado
            /// </summary>
            Disconnected,
            /// <summary>
            /// Reciviendo Mensaje
            /// </summary>
            ReceivedMessage
        }
        /// <summary>
        /// 
        /// </summary>
        public EType EventType;
        /// <summary>
        /// Manejador de conexión
        /// </summary>
        public ConnectionHandle Connection;
    }

    /// <summary>
    /// Servicio de red
    /// </summary>
    public class NetworkService
    {
        /// <summary>
        /// Aserción
        /// </summary>
        /// <param name="condition"></param>
        static void ASSERT(bool condition)
        {
#if UNITY_2017_1_OR_NEWER
            UnityEngine.Debug.Assert(condition);
#elif NET
            System.Diagnostics.Debug.Assert(condition);
#else
            if (!condition)
                throw new Exception("Assertion failed");
#endif

        }

        /// <summary>
        /// Clamp en
        /// </summary>
        /// <param name="value"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        private static int CLAMP(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        /// <summary>
        /// Tamaño del buffer de canal
        /// </summary>
        const int CHANNEL_BUFFER_SIZE = 2048;

        /// <summary>
        /// Timeout de apagado (ms)
        /// </summary>
#if DEBUG
        const int SHUTDOWN_TIMEOUT = 60000; // 60 segundos en modo DEBUG para permitir debugging sin desconexiones
#else
        const int SHUTDOWN_TIMEOUT = 2000;  // 2 segundos en modo normal
#endif

        /// <summary>
        /// Intervalo de ping (ms)
        /// </summary>
        const int PING_INTERVAL = 1000;

        /// <summary>
        /// Timeout de ping (s)
        /// </summary>
#if DEBUG
        const double PING_TIMEOUT = 300.0;  // 300 segundos (5 minutos) en modo DEBUG para permitir debugging
#else
        const double PING_TIMEOUT = 6000.0; // 6000 segundos en modo normal
#endif

        /// <summary>
        /// Umbral de advertencia de cola
        /// </summary>
        const int QUEUE_WARN_THRESHOLD = 1000;

        /// <summary>
        /// Tipos de mensaje
        /// </summary>
        enum EMessageType : byte
        {
            /// <summary>
            /// Inválido
            /// </summary>
            INVALID = 0,
            /// <summary>
            /// Ping
            /// </summary>
            Ping,
            /// <summary>
            /// Desconexión
            /// </summary>
            Disconnect,
            /// <summary>
            /// Información del cliente
            /// </summary>
            ClientInfo,
            /// <summary>
            /// Mensaje
            /// </summary>
            Message
        }

        /// <summary>
        /// Flags de reporte de eventos de red
        /// </summary>
        public volatile ENetworkEventReportFlags EventFlags = ENetworkEventReportFlags.ConnectionStatus;

        /// <summary>
        /// Estado de la red
        /// </summary>
        volatile ENetworkState State = ENetworkState.Closed;
        volatile bool bShutdown = true;
        volatile int NetworkErrorEmulationLevel = 0;    // 0 - 100
        volatile int NetworkErrorEmulationDelay = 0;    // milliseconds

        /// <summary>
        /// Servidor TCP (si es servidor)
        /// </summary>
        volatile TcpListener Server;
        /// <summary>
        /// Cliente UDP para recepción no confiable
        /// </summary>
        volatile UdpClient UnreliableReceive;
        /// <summary>
        /// Hilo de red
        /// </summary>
        Thread NetThread;
        /// <summary>
        /// Siguiente manejador de conexión
        /// </summary>
        ulong NextConnectionHandle = 1;
        /// <summary>
        /// Puerto confiable TCP
        /// </summary>
        int PortReliable;
        /// <summary>
        /// Puerto no confiable UDP
        /// </summary>
        int PortUnreliable;
        /// <summary>
        /// Dirección remota (si es cliente)
        /// </summary>
        IPAddress RemoteAddress;
        /// <summary>
        /// Puertos no confiables libres (si es servidor)
        /// </summary>
        ConcurrentQueue<int> FreeUnreliablePorts = new ConcurrentQueue<int>();
        /// <summary>
        /// Conexiones activas
        /// </summary>
        ConcurrentDictionary<ulong, Connection> Connections = new ConcurrentDictionary<ulong, Connection>();
        /// <summary>
        /// Eventos de conexión/desconexión
        /// </summary>
        ConcurrentQueue<ConnectionEvent> Events = new ConcurrentQueue<ConnectionEvent>();

        /// <summary>
        /// Generador de números aleatorios
        /// </summary>
        static readonly System.Random Rand = new System.Random();

        /// <summary>
        /// Destructor
        /// </summary>
        ~NetworkService()
        {
            if (State == ENetworkState.Running || State == ENetworkState.Startup)
            {
                Close();
            }
        }

        /// <summary>
        /// Inicia el servidor
        /// </summary>
        /// <param name="portReliable"></param>
        /// <param name="portUnreliable"></param>
        /// <param name="maxClients"></param>
        /// <returns></returns>
        public bool StartServer(int portReliable, int portUnreliable, int maxClients)
        {
            if (State != ENetworkState.Closed)
            {
                LogQueue.LogWarning("Cannot start Server, we're already a {}!", new object[] { State });
                return false;
            }

            ASSERT(Server == null);
            ASSERT(UnreliableReceive == null);
            ASSERT(Connections.Count == 0);

            PortReliable = portReliable;
            PortUnreliable = portUnreliable;

            while (FreeUnreliablePorts.TryDequeue(out int _)) { }
            for (int i = 1; i <= maxClients; ++i)
            {
                FreeUnreliablePorts.Enqueue(PortUnreliable + i);
            }

            while (Events.TryDequeue(out ConnectionEvent _)) { }

            Server = new TcpListener(new IPEndPoint(IPAddress.Any, portReliable));
            try
            {
                Server.Start(maxClients);
            }
            catch (Exception e)
            {
                LogQueue.LogWarning(e.Message);
                Server.Stop();
                Server = null;
                State = ENetworkState.Closed;
                return false;
            }
            UnreliableReceive = new UdpClient(portUnreliable);

            bShutdown = false;
            NetThread = new Thread(ServerThreadFunc);
            NetThread.Name = "Lightnet_ServerThread";
            NetThread.Start();

            State = ENetworkState.Running;
            return true;
        }
        /// <summary>
        /// Inicia el cliente
        /// </summary>
        /// <param name="address"></param>
        /// <param name="portReliable"></param>
        /// <param name="portUnreliable"></param>
        /// <returns></returns>
        public bool StartClient(IPAddress address, int portReliable, int portUnreliable)
        {
            if (State != ENetworkState.Closed)
            {
                LogQueue.LogWarning("Cannot start Client, we're already a {}!", new object[] { State });
                return false;
            }

            ASSERT(Server == null);
            ASSERT(UnreliableReceive == null);
            ASSERT(Connections.Count == 0);

            State = ENetworkState.Startup;
            RemoteAddress = address;
            PortReliable = portReliable;
            PortUnreliable = portUnreliable;
            while (Events.TryDequeue(out ConnectionEvent e)) { }

            bShutdown = false;
            NetThread = new Thread(ClientThreadFunc);
            NetThread.Name = "Lightnet_ClientThread";
            NetThread.Start();

            return true;
        }

        /// <summary>
        /// Obtiene el estado de la red
        /// </summary>
        /// <returns></returns>
        public ENetworkState GetState()
        {
            return State;
        }

        /// <summary>
        /// Indica si es servidor
        /// </summary>
        /// <returns></returns>
        public bool IsServer()
        {
            return Server != null;
        }

        /// <summary>
        /// Cierra la red
        /// </summary>
        /// <returns></returns>
        public bool Close()
        {
            if (State == ENetworkState.Closed)
            {
                LogQueue.LogWarning("Cannot close networking, already closed!");
                return false;
            }
            if (State == ENetworkState.Shutdown)
            {
                LogQueue.LogWarning("Cannot close networking, already shutting down...");
                return false;
            }

            ASSERT(!bShutdown);
            ASSERT(NetThread != null);
            ASSERT(NetThread.IsAlive);

            State = ENetworkState.Shutdown;
            foreach (KeyValuePair<ulong, Connection> conn in Connections)
            {
                conn.Value.SendDisconnect();
            }
            bShutdown = true;

            void AbortThreads()
            {
                foreach (KeyValuePair<ulong, Connection> conn in Connections)
                {
                    conn.Value.Shutdown();
                }
                Connections.Clear();

                if (Thread.CurrentThread != NetThread)
                {
                    if (!NetThread.Join(SHUTDOWN_TIMEOUT))
                    {
                        NetThread.Abort();
                    }
                    NetThread = null;
                }

                State = ENetworkState.Closed;
            }

            Thread abortThread = new Thread(AbortThreads);
            abortThread.Start();

            Server?.Stop();
            Server = null;
            UnreliableReceive?.Close();
            UnreliableReceive = null;

            return true;
        }

        /// <summary>
        /// Desconecta una conexión
        /// </summary>
        /// <param name="connection"></param>
        /// <returns></returns>
        public bool Disconnect(ConnectionHandle connection)
        {
            if ((State == ENetworkState.Startup || State == ENetworkState.Running) && !IsServer())
            {
                return Close();
            }

            if (!Connections.TryRemove(connection.GetInternalHandle(), out Connection conn))
            {
                LogQueue.LogWarning($"Cannot close an unknown connection. Already disconnected?");
                return false;
            }

            conn.SendDisconnect();
            conn.Shutdown();
            FreeUnreliablePorts.Enqueue(conn.RemoteUnreliable.Port);

            return true;
        }

        /// <summary>
        /// Configura la emulación de errores de red
        /// </summary>
        /// <param name="looseLevel"></param>
        /// <param name="maxSendDelay"></param>
        public void SetNetworkErrorEmulation(int looseLevel, int maxSendDelay)
        {
            NetworkErrorEmulationLevel = CLAMP(looseLevel, 0, 100);
            NetworkErrorEmulationDelay = Math.Min(maxSendDelay, 0);
        }

        /// <summary>
        /// Obtiene las conexiones activas
        /// </summary>
        /// <returns></returns>
        public ConnectionHandle[] GetConnections()
        {
            ConnectionHandle[] conns = new ConnectionHandle[Connections.Count];
            int i = 0;
            foreach (KeyValuePair<ulong, Connection> conn in Connections)
            {
                conns[i++] = new ConnectionHandle(conn.Key);
            }
            return conns;
        }

        /// <summary>
        /// Obtiene el nombre de la conexión
        /// </summary>
        /// <param name="handle"></param>
        /// <returns></returns>
        public string GetConnectionName(ConnectionHandle handle)
        {
            if (!handle.IsValid() || !Connections.ContainsKey(handle.GetInternalHandle()))
            {
                LogQueue.LogWarning("Given connection handle was invalid!");
                return "INVALID HANDLE";
            }
            return Connections[handle.GetInternalHandle()].ToString();
        }

        /// <summary>
        /// Indica si la conexión está viva
        /// </summary>
        /// <param name="handle"></param>
        /// <returns></returns>
        public bool IsAlive(ConnectionHandle handle)
        {
            return handle.IsValid() && Connections.ContainsKey(handle.GetInternalHandle()) && Connections[handle.GetInternalHandle()].IsAlive();
        }

        /// <summary>
        /// Obtiene el siguiente evento de conexión/desconexión
        /// </summary>
        /// <param name="outEvent"></param>
        /// <returns></returns>
        public bool GetNextEvent(out ConnectionEvent outEvent)
        {
            return Events.TryDequeue(out outEvent);
        }

        /// <summary>
        /// Envía un mensaje
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="channel"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public bool SendMessage(ConnectionHandle connection, ENetChannel channel, byte[] message)
        {
            if (!Connections.TryGetValue(connection.GetInternalHandle(), out Connection conn))
            {
                LogQueue.LogWarning("Given connection handle is invalid!");
                return false;
            }

            conn.SendMessage(channel, message);
            return true;
        }

        /// <summary>
        /// Envía un mensaje a todas las conexiones
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="message"></param>
        public void BroadcastMessage(ENetChannel channel, byte[] message)
        {
            foreach (KeyValuePair<ulong, Connection> conn in Connections)
            {
                if (conn.Value.IsAlive())
                {
                    conn.Value.SendMessage(channel, message);
                }
            }
        }

        /// <summary>
        /// Envía un mensaje a todas las conexiones
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="message"></param>
        public void BroadcastMessageExeption(ConnectionHandle connection, ENetChannel channel, byte[] message)
        {
            if (!Connections.TryGetValue(connection.GetInternalHandle(), out Connection connException))
            {
                LogQueue.LogWarning("Given connection handle is invalid!");
                return;
            }
            foreach (KeyValuePair<ulong, Connection> conn in Connections)
            {
                if (conn.Value.IsAlive() && connException != conn.Value)
                {
                    conn.Value.SendMessage(channel, message);
                }
            }
        }

        /// <summary>
        /// Obtiene el siguiente mensaje disponible para una conexión específica
        /// y un canal específico (Reliable o Unreliable).
        /// </summary>
        /// <param name="connection">
        /// Handle público de la conexión desde la cual se desea leer el mensaje.
        /// </param>
        /// <param name="channel">
        /// Canal de red desde el cual se intentará obtener el mensaje.
        /// </param>
        /// <param name="message">
        /// Mensaje recibido en formato byte[], si existe.
        /// </param>
        /// <returns>
        /// True si se obtuvo un mensaje; false si no hay mensajes o la conexión es inválida.
        /// </returns>
        public bool GetNextMessage(ConnectionHandle connection, ENetChannel channel, out byte[] message)
        {
            // Verifica que el handle corresponda a una conexión válida
            if (!Connections.TryGetValue(connection.GetInternalHandle(), out Connection conn))
            {
                message = null;

                // Se registra una advertencia si el handle no existe
                LogQueue.LogWarning("Given connection handle is invalid!");
                return false;
            }

            // Delegación directa al objeto Connection
            return conn.GetNextMessage(channel, out message);
        }

        /// <summary>
        /// Obtiene el siguiente mensaje disponible para una conexión específica,
        /// devolviendo además el canal por el cual fue recibido.
        /// </summary>
        /// <param name="connection">
        /// Handle público de la conexión.
        /// </param>
        /// <param name="message">
        /// Mensaje recibido en formato byte[].
        /// </param>
        /// <param name="channel">
        /// Canal por el cual se recibió el mensaje.
        /// </param>
        /// <returns>
        /// True si se obtuvo un mensaje; false si no hay mensajes o la conexión es inválida.
        /// </returns>
        public bool GetNextMessage(ConnectionHandle connection, out byte[] message, out ENetChannel channel)
        {
            // Verifica que la conexión exista
            if (!Connections.TryGetValue(connection.GetInternalHandle(), out Connection conn))
            {
                // Valores por defecto en caso de error
                channel = ENetChannel.Unreliable;
                message = null;

                LogQueue.LogWarning("Given connection handle is invalid!");
                return false;
            }

            // Obtiene el siguiente mensaje disponible desde cualquier canal
            return conn.GetNextMessage(out message, out channel);
        }

        /// <summary>
        /// Obtiene el siguiente mensaje disponible para una conexión específica,
        /// sin importar el canal.
        /// </summary>
        /// <param name="connection">
        /// Handle público de la conexión.
        /// </param>
        /// <param name="message">
        /// Mensaje recibido en formato byte[].
        /// </param>
        /// <returns>
        /// True si se obtuvo un mensaje; false si no hay mensajes o la conexión es inválida.
        /// </returns>
        public bool GetNextMessage(ConnectionHandle connection, out byte[] message)
        {
            // Validación del handle de conexión
            if (!Connections.TryGetValue(connection.GetInternalHandle(), out Connection conn))
            {
                message = null;

                LogQueue.LogWarning("Given connection handle is invalid!");
                return false;
            }

            // Obtiene el siguiente mensaje disponible de la conexión
            return conn.GetNextMessage(out message);
        }

        /// <summary>
        /// Obtiene el siguiente mensaje disponible desde cualquier conexión,
        /// devolviendo también la conexión y el canal de origen.
        /// </summary>
        /// <param name="message">
        /// Mensaje recibido en formato byte[].
        /// </param>
        /// <param name="connection">
        /// Handle de la conexión desde la cual proviene el mensaje.
        /// </param>
        /// <param name="channel">
        /// Canal por el cual fue recibido el mensaje.
        /// </param>
        /// <returns>
        /// True si se obtuvo un mensaje; false si no hay mensajes en ninguna conexión.
        /// </returns>
        public bool GetNextMessage(out byte[] message, out ConnectionHandle connection, out ENetChannel channel)
        {
            // Itera sobre todas las conexiones activas
            foreach (KeyValuePair<ulong, Connection> conn in Connections)
            {
                // Se construye el handle público a partir del identificador interno
                connection = new ConnectionHandle(conn.Key);

                // Se intenta obtener un mensaje de esta conexión
                if (conn.Value.GetNextMessage(out message, out channel))
                {
                    return true;
                }
            }

            // Valores por defecto si no se encontró ningún mensaje
            message = null;
            connection = new ConnectionHandle(0);
            channel = ENetChannel.Unreliable;
            return false;
        }

        /// <summary>
        /// Obtiene el siguiente mensaje disponible desde cualquier conexión,
        /// devolviendo también la conexión de origen.
        /// </summary>
        /// <param name="message">
        /// Mensaje recibido en formato byte[].
        /// </param>
        /// <param name="connection">
        /// Handle de la conexión desde la cual proviene el mensaje.
        /// </param>
        /// <returns>
        /// True si se obtuvo un mensaje; false si no hay mensajes disponibles.
        /// </returns>
        public bool GetNextMessage(out byte[] message, out ConnectionHandle connection)
        {
            // Recorre todas las conexiones registradas
            foreach (KeyValuePair<ulong, Connection> conn in Connections)
            {
                connection = new ConnectionHandle(conn.Key);

                // Intenta leer un mensaje de la conexión actual
                if (conn.Value.GetNextMessage(out message))
                {
                    return true;
                }
            }

            // Valores por defecto si no se encontró ningún mensaje
            message = null;
            connection = new ConnectionHandle(0);
            return false;
        }

        /// <summary>
        /// Obtiene el siguiente mensaje disponible desde cualquier conexión,
        /// sin devolver información de conexión ni canal.
        /// </summary>
        /// <param name="message">
        /// Mensaje recibido en formato byte[].
        /// </param>
        /// <returns>
        /// True si se obtuvo un mensaje; false si no hay mensajes disponibles.
        /// </returns>
        public bool GetNextMessage(out byte[] message)
        {
            // Itera por todas las conexiones activas
            foreach (KeyValuePair<ulong, Connection> conn in Connections)
            {
                // Intenta obtener el siguiente mensaje disponible
                if (conn.Value.GetNextMessage(out message))
                {
                    return true;
                }
            }

            // No se encontraron mensajes
            message = null;
            return false;
        }

        /// <summary>
        /// Agrega un evento a la cola si el flag está activo
        /// </summary>
        /// <param name="ev"></param>
        /// <param name="flag"></param>
        void AddEvent(ConnectionEvent ev, ENetworkEventReportFlags flag)
        {
            if ((EventFlags & flag) != 0)
            {
                Events.Enqueue(ev);
                if (Events.Count > QUEUE_WARN_THRESHOLD)
                {
                    LogQueue.LogWarning("NetworkService event queue has over {0} events queued! Do you poll them somewhere?");
                }
            }
        }

        /// <summary>
        /// Función del hilo servidor
        /// </summary>
        void ServerThreadFunc()
        {
            ASSERT(Server != null);
            ASSERT(State == ENetworkState.Running);
            ASSERT(Connections.Count == 0);

            while (!bShutdown)
            {
                if (Server.Pending())
                {
                    TcpClient client = null;
                    try
                    {
                        client = Server.AcceptTcpClient();
                    }
                    catch (Exception e)
                    {
                        LogQueue.LogWarning(e.Message);
                        continue;
                    }

                    ASSERT(client != null);
                    IPAddress address = ((IPEndPoint)client.Client.RemoteEndPoint).Address;

                    ASSERT(FreeUnreliablePorts.Count > 0);
                    ASSERT(FreeUnreliablePorts.TryDequeue(out int clientListenPort));

                    ulong handle = NextConnectionHandle++;
                    Connection conn = new Connection(this, handle, address, PortReliable, clientListenPort, client);
                    if (!Connections.TryAdd(handle, conn))
                    {
                        LogQueue.LogError("Connection handles are inconsistent! This should never happen!");
                        Close();
                        return;
                    }

                    LogQueue.LogInfo("New client '{0}' joined (handle: {1})", new object[] { address.ToString(), handle });
                    AddEvent(new ConnectionEvent
                    {
                        Connection = new ConnectionHandle(handle),
                        EventType = ConnectionEvent.EType.Connected
                    }, ENetworkEventReportFlags.ConnectionStatus);
                }

                foreach (KeyValuePair<ulong, Connection> conn in Connections)
                {
                    if (!conn.Value.IsAlive())
                    {
                        if (!conn.Value.IsIntentionalDisconnect())
                        {
                            LogQueue.LogInfo("Connection to '{0}' lost!", new object[] { conn.Value.GetRemoteAddress().ToString() });
                        }
                        conn.Value.Shutdown();
                        if (!Connections.TryRemove(conn.Key, out Connection c))
                        {
                            LogQueue.LogError("Inconsitent connection dictionary! This should never happen!");
                        }
                        AddEvent(new ConnectionEvent
                        {
                            Connection = new ConnectionHandle(conn.Key),
                            EventType = ConnectionEvent.EType.Disconnected
                        }, ENetworkEventReportFlags.ConnectionStatus);
                    }
                }

                ReceiveUnreliable();
            }
        }

        /// <summary>
        /// Función del hilo cliente
        /// </summary>
        void ClientThreadFunc()
        {
            const ulong handle = 1;

            ASSERT(Server == null);
            ASSERT(UnreliableReceive == null);
            ASSERT(State == ENetworkState.Startup);
            ASSERT(Connections.Count == 0);

            TcpClient client = null;
            try
            {
                client = new TcpClient();
                client.Connect(new IPEndPoint(RemoteAddress, PortReliable));
                if (!client.Connected)
                {
                    Connections.Clear();
                    LogQueue.LogWarning("Couldn't connect to Server '{0}'!", new object[] { RemoteAddress.ToString() });
                    Close();
                    return;
                }
            }
            catch (Exception e)
            {
                LogQueue.LogWarning(e.Message);
                Close();
                return;
            }

            Connections.TryAdd(handle, new Connection(this, handle, RemoteAddress, PortReliable, PortUnreliable, client));

            ASSERT(Connections.Count == 1);
            State = ENetworkState.Running;

            AddEvent(new ConnectionEvent
            {
                Connection = new ConnectionHandle(handle),
                EventType = ConnectionEvent.EType.Connected
            }, ENetworkEventReportFlags.ConnectionStatus);

            while (!bShutdown)
            {
                if (!Connections[handle].IsAlive())
                {
                    if (!Connections[handle].IsIntentionalDisconnect())
                    {
                        LogQueue.LogWarning("Connection to Server '{0}' lost!", new object[] { Connections[handle].GetRemoteAddress().ToString() });
                    }
                    Connections[handle].Shutdown();
                    Connections.Clear();

                    AddEvent(new ConnectionEvent
                    {
                        Connection = new ConnectionHandle(handle),
                        EventType = ConnectionEvent.EType.Disconnected
                    }, ENetworkEventReportFlags.ConnectionStatus);

                    Close();
                    return;
                }

                ReceiveUnreliable();
            }
        }

        /// <summary>
        /// Recibe paquetes UDP no confiables
        /// </summary>
        void ReceiveUnreliable()
        {
            if (UnreliableReceive == null)
            {
                return;
            }
            while (UnreliableReceive.Available > 0)
            {
                IPEndPoint sender = null;
                byte[] data = null;
                try
                {
                    data = UnreliableReceive.Receive(ref sender);
                }
                catch (Exception e)
                {
                    LogQueue.LogWarning("UDP Receiving failed: " + e.Message);
                    return;
                }

                Connection connection = null;
                ASSERT(data != null);
                ASSERT(sender != null);

                LogQueue.LogInfo("Received UDP package: {0}", new object[] { data.Length });

                int offset = 0;
                if (Server != null)
                {
                    ulong handle = BitConverter.ToUInt64(data, 0); offset += sizeof(ulong);
                    connection = Connections[handle];
                }
                else
                {
                    connection = Connections[1];
                }
                ASSERT(connection != null);

                lock (connection.GetUnreliableReceiveLock())
                {
                    Connection.ReceiveBuffer buffer = connection.GetUnreliableReceiveBuffer();

                    int maxReadSize = CHANNEL_BUFFER_SIZE - buffer.Head;
                    if (data.Length > maxReadSize)
                    {
                        LogQueue.LogError("Ran out of unreliable receive buffer memory! Message too large?");
                        return;
                    }

                    Array.Copy(data, offset, buffer.Buffer, buffer.Head, data.Length - offset);
                    buffer.Head += data.Length;
                }
            }
        }

        /// <summary>
        /// Manejador de conexión
        /// </summary>
        class Connection
        {
            public class ReceiveBuffer
            {
                public byte[] Buffer = new byte[CHANNEL_BUFFER_SIZE];
                public int Head;
            }

            public IPEndPoint RemoteUnreliable { get; private set; }
            IPEndPoint RemoteReliable;

            NetworkService Owner = null;
            ulong Handle = 0;
            ulong RemoteHandle = 0;

            volatile bool bIntentionalDisconnect;
            volatile bool bShutdown = false;
            volatile Thread ConnectionThread;
            volatile TcpClient Reliable;
            Thread PingThread;
            UdpClient UnrealiableSend;
            ulong UnreliableTime = 0;
            DateTime LastPing;

            object AliveLock = new object();
            object UnreliableReceiveLock = new object();

            ConcurrentQueue<byte[]> ReceivedReliableMessages = new ConcurrentQueue<byte[]>();
            ConcurrentSortedQueue<ulong, byte[]> ReceivedUnreliableMessages = new ConcurrentSortedQueue<ulong, byte[]>();

            ReceiveBuffer ReliableReceiveBuffer = new ReceiveBuffer();

            ReceiveBuffer UnreliableReceiveBuffer = new ReceiveBuffer();

            ConcurrentQueue<byte[]> ReliableSendBuffer = new ConcurrentQueue<byte[]>();
            ConcurrentQueue<byte[]> UnreliableSendBuffer = new ConcurrentQueue<byte[]>();

            public Connection(NetworkService owner, ulong handle, IPAddress address, int portReliable, int portUnreliable, TcpClient client)
            {
                ASSERT(owner != null);
                ASSERT(handle != 0);
                ASSERT(client != null);

                Owner = owner;
                Handle = handle;
                RemoteReliable = new IPEndPoint(address, portReliable);
                RemoteUnreliable = new IPEndPoint(address, portUnreliable);

                Reliable = client;
                LastPing = DateTime.Now;

                ConnectionThread = new Thread(ConnectionThreadFunc);
                ConnectionThread.Name = "Lightnet_" + ToString();
                ConnectionThread.Start();

                if (Owner.Server != null)
                {
                    SendClientInfo();
                }

                PingThread = new Thread(Ping);
                PingThread.Name = "Lightnet_Ping_" + ToString();
                PingThread.Start();
            }

            ~Connection()
            {
                if (!bShutdown)
                {
                    Shutdown();
                }
            }

            /// <summary>
            /// Obtiene la dirección remota
            /// </summary>
            /// <returns></returns>
            public IPAddress GetRemoteAddress()
            {
                return RemoteReliable.Address;
            }

            /// <summary>
            /// Indica si la conexión está viva
            /// </summary>
            /// <returns></returns>
            public bool IsAlive()
            {
                bool bAlive = false;
                lock (AliveLock)
                {
                    bAlive = !bIntentionalDisconnect && Reliable.Connected && ConnectionThread.IsAlive;
                }
                return bAlive;
            }

            /// <summary>
            /// Indica si la desconexión fue intencional
            /// </summary>
            /// <returns></returns>
            public bool IsIntentionalDisconnect()
            {
                return bIntentionalDisconnect;
            }

            /// <summary>
            /// Obtiene el buffer de recepción no confiable
            /// </summary>
            /// <returns></returns>
            public ReceiveBuffer GetUnreliableReceiveBuffer()
            {
                return UnreliableReceiveBuffer;
            }

            /// <summary>
            /// Obtiene el lock de recepción no confiable
            /// </summary>
            /// <returns></returns>
            public object GetUnreliableReceiveLock()
            {
                return UnreliableReceiveLock;
            }

            /// <summary>
            /// Apaga la conexión
            /// </summary>
            public void Shutdown()
            {
                if (bShutdown)
                {
                    LogQueue.LogWarning("Connection is already shut down!");
                    return;
                }

                bShutdown = true;
                if (!ConnectionThread.Join(SHUTDOWN_TIMEOUT))
                {
                    LogQueue.LogWarning("Shutdown timeout, aborting connection thread...");
                    ConnectionThread.Abort();
                }

                if (!PingThread.Join(SHUTDOWN_TIMEOUT))
                {
                    LogQueue.LogWarning("Shutdown timeout, aborting ping thread...");
                    PingThread.Abort();
                }
                PingThread = null;

                Reliable.Close();
                UnrealiableSend.Close();
            }
            /// <summary>
            /// Obtiene el siguiente mensaje recibido desde un canal específico
            /// (Reliable o Unreliable).
            /// </summary>
            /// <param name="channel">
            /// Canal del cual se desea leer el mensaje.
            /// </param>
            /// <param name="data">
            /// Datos del mensaje recibido.
            /// </param>
            /// <returns>
            /// True si se obtuvo un mensaje; false si no hay mensajes disponibles.
            /// </returns>
            public bool GetNextMessage(ENetChannel channel, out byte[] data)
            {
                // Selección explícita del canal confiable (TCP)
                if (channel == ENetChannel.Reliable)
                {
                    // Intenta extraer el siguiente mensaje de la cola confiable
                    return ReceivedReliableMessages.TryDequeue(out data);
                }
                // Selección explícita del canal no confiable (UDP)
                else if (channel == ENetChannel.Unreliable)
                {
                    // Intenta extraer el siguiente mensaje de la cola no confiable
                    return ReceivedUnreliableMessages.TryDequeue(out data);
                }

                // Canal desconocido (error de programación)
                LogQueue.LogError("Unknown ENetChannel '{0}'!", new object[] { (int)channel });
                data = null;
                return false;
            }

            /// <summary>
            /// Obtiene el siguiente mensaje disponible, sin importar el canal,
            /// e informa por cuál canal fue recibido.
            /// </summary>
            /// <param name="data">
            /// Datos del mensaje recibido.
            /// </param>
            /// <param name="channel">
            /// Canal desde el cual se recibió el mensaje.
            /// </param>
            /// <returns>
            /// True si se obtuvo un mensaje; false si no hay mensajes disponibles.
            /// </returns>
            public bool GetNextMessage(out byte[] data, out ENetChannel channel)
            {
                // Prioridad al canal confiable
                if (ReceivedReliableMessages.TryDequeue(out data))
                {
                    channel = ENetChannel.Reliable;
                    return true;
                }
                // Si no hay mensajes confiables, intenta con los no confiables
                else if (ReceivedUnreliableMessages.TryDequeue(out data))
                {
                    channel = ENetChannel.Unreliable;
                    return true;
                }

                // No se encontraron mensajes
                channel = ENetChannel.Unreliable;
                return false;
            }

            /// <summary>
            /// Obtiene el siguiente mensaje disponible desde cualquier canal,
            /// sin indicar el canal de origen.
            /// </summary>
            /// <param name="data">
            /// Datos del mensaje recibido.
            /// </param>
            /// <returns>
            /// True si se obtuvo un mensaje; false si no hay mensajes disponibles.
            /// </returns>
            public bool GetNextMessage(out byte[] data)
            {
                // Intenta primero en la cola confiable, luego en la no confiable
                return ReceivedReliableMessages.TryDequeue(out data) ||
                       ReceivedUnreliableMessages.TryDequeue(out data);
            }

            /// <summary>
            /// Envía un mensaje por el canal especificado (Reliable o Unreliable).
            /// </summary>
            /// <param name="channel">
            /// Canal de envío.
            /// </param>
            /// <param name="data">
            /// Datos del mensaje a enviar.
            /// </param>
            public void SendMessage(ENetChannel channel, byte[] data)
            {
                // Verifica que la conexión esté activa
                if (!IsAlive())
                {
                    LogQueue.LogWarning("Cannot send message on a dead connection!");
                    return;
                }

                // No se permiten mensajes vacíos
                if (data.Length == 0)
                {
                    LogQueue.LogWarning("Cannot send empty message!");
                    return;
                }

                // ENVÍO CONFIABLE (TCP)
                if (channel == ENetChannel.Reliable)
                {
                    // Tamaño máximo del paquete TCP
                    const ushort MaxPaketSize =
                        65535 -                 // Tamaño máximo TCP
                        sizeof(ushort);         // Longitud del mensaje

                    // Validación de tamaño
                    if (data.Length > MaxPaketSize)
                    {
                        LogQueue.LogError(
                            "Given message data of {0} bytes exceeds max message size of {1}",
                            new object[] { data.Length, MaxPaketSize });
                        return;
                    }

                    // Advertencia si el mensaje supera el buffer de recepción
                    if (data.Length > CHANNEL_BUFFER_SIZE)
                    {
                        LogQueue.LogWarning(
                            "Given message data of {0} bytes potentially exceeds receiving buffer size of {1}",
                            new object[] { data.Length, MaxPaketSize });
                    }

                    // Construcción del paquete TCP
                    int offset = 0;
                    byte[] sendData = new byte[sizeof(byte) + sizeof(ushort) + data.Length];
                    byte[] msgLength = BitConverter.GetBytes((ushort)data.Length);

                    sendData[0] = (byte)EMessageType.Message; offset += sizeof(byte);
                    Array.Copy(msgLength, 0, sendData, offset, msgLength.Length); offset += msgLength.Length;
                    Array.Copy(data, 0, sendData, offset, data.Length);

                    // Se encola para envío por el hilo de conexión
                    ReliableSendBuffer.Enqueue(sendData);
                }
                // ENVÍO NO CONFIABLE (UDP)
                else if (channel == ENetChannel.Unreliable)
                {
                    // Tamaño máximo del paquete UDP
                    const ushort MaxPaketSize =
                        65535 -                 // Tamaño máximo UDP
                        sizeof(ulong) -         // Timestamp
                        sizeof(ushort);         // Longitud del mensaje

                    // Validación de tamaño
                    if (data.Length > MaxPaketSize)
                    {
                        // TODO: fragmentación de mensajes grandes
                        LogQueue.LogError(
                            "Given message data of {0} bytes exceeds max message size of {1}",
                            new object[] { data.Length, MaxPaketSize });
                        return;
                    }

                    // Advertencia por posible overflow del buffer
                    if (data.Length > CHANNEL_BUFFER_SIZE)
                    {
                        LogQueue.LogWarning(
                            "Given message data of {0} bytes potentially exceeds receiving buffer size of {1}",
                            new object[] { data.Length, MaxPaketSize });
                    }

                    // Se encola el mensaje UDP
                    UnreliableSendBuffer.Enqueue(data);
                }
            }

            /// <summary>
            /// Devuelve una representación legible del estado de la conexión.
            /// </summary>
            public override string ToString()
            {
                return string.Format(
                    "{0}:{1}:{2} - {3}",
                    RemoteReliable.Address.ToString(),
                    RemoteReliable.Port,
                    RemoteUnreliable.Port,
                    IsAlive() ? "Alive" : "Dead");
            }

            /// <summary>
            /// Envía un mensaje de desconexión por el canal confiable.
            /// </summary>
            public void SendDisconnect()
            {
                ReliableSendBuffer.Enqueue(new byte[1] { (byte)EMessageType.Disconnect });
            }

            /// <summary>
            /// Envía un mensaje de ping para mantener viva la conexión.
            /// </summary>
            public void SendPing()
            {
                ReliableSendBuffer.Enqueue(new byte[1] { (byte)EMessageType.Ping });
            }

            /// <summary>
            /// Envía información inicial del cliente (puerto UDP y handle).
            /// </summary>
            public void SendClientInfo()
            {
                int offset = 0;

                byte[] portBytes = BitConverter.GetBytes(RemoteUnreliable.Port);
                byte[] handleBytes = BitConverter.GetBytes(Handle);

                byte[] sendData = new byte[
                    sizeof(byte) +
                    portBytes.Length +
                    handleBytes.Length];

                sendData[0] = (byte)EMessageType.ClientInfo; offset += sizeof(byte);
                Array.Copy(portBytes, 0, sendData, offset, portBytes.Length); offset += portBytes.Length;
                Array.Copy(handleBytes, 0, sendData, offset, handleBytes.Length);

                ReliableSendBuffer.Enqueue(sendData);
            }

            /// <summary>
            /// Hilo dedicado al envío periódico de pings.
            /// </summary>
            void Ping()
            {
                while (!bShutdown)
                {
                    SendPing();
                    LogQueue.LogInfo("Send ping to: {0}", new object[] { ToString() });
                    Thread.Sleep(PING_INTERVAL);
                }
            }

            /// <summary>
            /// Función del hilo de conexión
            /// </summary>
            void ConnectionThreadFunc()
            {
                ASSERT(UnrealiableSend == null);
                ASSERT(ReliableReceiveBuffer.Head == 0);
                ASSERT(UnreliableReceiveBuffer.Head == 0);

                UnrealiableSend = new UdpClient();
                UnrealiableSend.Connect(RemoteUnreliable);

                while (!bShutdown)
                {
                    if (!Reliable.Connected)
                    {
                        return;
                    }

                    if (!UnrealiableSend.Client.Connected)
                    {
                        LogQueue.LogWarning("UDP connection lost");
                        Reliable.Close();
                        return;
                    }

                    {
                        NetworkStream stream = null;
                        try
                        {
                            stream = Reliable.GetStream();
                        }
                        catch (Exception e)
                        {
                            LogQueue.LogWarning(e.Message);
                            Reliable.Close();
                            return;
                        }
                        while (stream.CanRead && stream.DataAvailable)
                        {
                            int maxReadSize = CHANNEL_BUFFER_SIZE - ReliableReceiveBuffer.Head;
                            if (maxReadSize == 0)
                            {
                                LogQueue.LogError("Ran out of reliable receive buffer memory! Message too large?");
                                ReliableReceiveBuffer.Head = 0;
                                Reliable.Close();
                                return;
                            }

                            int bytesRead;
                            try
                            {
                                bytesRead = stream.Read(ReliableReceiveBuffer.Buffer, ReliableReceiveBuffer.Head, maxReadSize);

                                if (bytesRead == 0)
                                {
                                    LogQueue.LogWarning("Connection to '{0}' lost!", new object[] { RemoteReliable.Address.ToString() });
                                    Reliable.Close();
                                    return;
                                }
                                LogQueue.LogInfo("Received {0} TCP bytes", new object[] { bytesRead });
                            }
                            catch (Exception e)
                            {
                                LogQueue.LogWarning(e.Message);
                                Reliable.Close();
                                return;
                            }
                            ReliableReceiveBuffer.Head += bytesRead;

                            if (ReliableReceiveBuffer.Head >= sizeof(byte))
                            {
                                int offset = 0;
                                EMessageType msgType = (EMessageType)ReliableReceiveBuffer.Buffer[0]; offset += sizeof(byte);
                                bool bResetBuffer = true;


                                switch (msgType)
                                {
                                    case EMessageType.Ping:
                                        {
                                            LogQueue.LogInfo("Received ping from'{0}'", new object[] { RemoteReliable.Address.ToString() });
                                            LastPing = DateTime.Now;
                                            break;
                                        }

                                    case EMessageType.Disconnect:
                                        {
                                            LogQueue.LogInfo("'{0}' disconnected.", new object[] { RemoteReliable.Address.ToString() });
                                            bIntentionalDisconnect = true;
                                            Reliable.Close();
                                            return;
                                        }

                                    case EMessageType.ClientInfo:
                                        {
                                            if (ReliableReceiveBuffer.Head >= sizeof(byte) + sizeof(int) + sizeof(ulong))
                                            {
                                                ASSERT(Owner.Server == null);

                                                int localUnreliablePort = BitConverter.ToInt32(ReliableReceiveBuffer.Buffer, offset); offset += sizeof(int);
                                                RemoteHandle = BitConverter.ToUInt64(ReliableReceiveBuffer.Buffer, offset);
                                                ASSERT(RemoteHandle != 0);
                                                Owner.UnreliableReceive = new UdpClient(localUnreliablePort);

                                                LogQueue.LogInfo("Received ClientInfo from'{0}'", new object[] { RemoteReliable.Address.ToString() });
                                            }
                                            else
                                            {
                                                bResetBuffer = false;
                                            }
                                            break;
                                        }

                                    case EMessageType.Message:
                                        {
                                            ushort msgSize = BitConverter.ToUInt16(ReliableReceiveBuffer.Buffer, offset); offset += sizeof(ushort);
                                            if (ReliableReceiveBuffer.Head >= sizeof(byte) + sizeof(ushort) + msgSize)
                                            {
                                                byte[] message = new byte[msgSize];
                                                Array.Copy(ReliableReceiveBuffer.Buffer, offset, message, 0, msgSize);

                                                ReceivedReliableMessages.Enqueue(message);
                                                if (ReceivedReliableMessages.Count >= QUEUE_WARN_THRESHOLD)
                                                {
                                                    LogQueue.LogWarning("The network data queue for reliable messages has over {0} messages queued! Do you poll them somewhere?");
                                                }

                                                if ((Owner.EventFlags & ENetworkEventReportFlags.ReceivedMessage) != 0)
                                                {
                                                    Owner.AddEvent(new ConnectionEvent
                                                    {
                                                        Connection = new ConnectionHandle(Handle),
                                                        EventType = ConnectionEvent.EType.ReceivedMessage
                                                    }, ENetworkEventReportFlags.ReceivedMessage);
                                                }
                                            }
                                            else
                                            {
                                                bResetBuffer = false;
                                            }
                                            break;
                                        }

                                    default:
                                        {
                                            LogQueue.LogError("Received message with invalid/unknown message type '{0}'! Ignoring...", new object[] { (byte)msgType });
                                            break;
                                        }
                                }

                                if (bResetBuffer)
                                {
                                    int remaning = CHANNEL_BUFFER_SIZE - ReliableReceiveBuffer.Head;
                                    if (remaning > 0)
                                    {
                                        Array.Copy(ReliableReceiveBuffer.Buffer, ReliableReceiveBuffer.Head, ReliableReceiveBuffer.Buffer, 0, remaning);
                                    }
                                    ReliableReceiveBuffer.Head = 0;
                                }
                            }
                        }
                        // Enviar TCP
                        try
                        {
                            stream = Reliable.GetStream();
                        }
                        catch (Exception e)
                        {
                            LogQueue.LogWarning(e.Message);
                            Reliable.Close();
                            return;
                        }

                        byte[] messageData;
                        while (ReliableSendBuffer.TryDequeue(out messageData))
                        {
                            Thread.Sleep(Rand.Next(0, Owner.NetworkErrorEmulationDelay));
                            try
                            {
                                LogQueue.LogInfo("Sending {0}", new object[] { ((EMessageType)messageData[0]).ToString() });
                                stream.Write(messageData, 0, messageData.Length);
                            }
                            catch (Exception e)
                            {
                                LogQueue.LogWarning(e.Message);
                                Reliable.Close();
                                return;
                            }
                        }
                    }

                    // Canal UDP
                    {
                        // Recivir Datos UDP
                        lock (UnreliableReceiveLock)
                        {
                            ulong timestamp;
                            ushort msgSize;
                            if (UnreliableReceiveBuffer.Head >= sizeof(ulong) + sizeof(ushort))
                            {
                                int offset = 0;
                                timestamp = BitConverter.ToUInt64(UnreliableReceiveBuffer.Buffer, offset); offset += sizeof(ulong);
                                msgSize = BitConverter.ToUInt16(UnreliableReceiveBuffer.Buffer, offset); offset += sizeof(ushort);

                                if (UnreliableReceiveBuffer.Head >= offset + msgSize)
                                {
                                    byte[] message = new byte[msgSize];
                                    Array.Copy(UnreliableReceiveBuffer.Buffer, offset, message, 0, msgSize);
                                    ReceivedUnreliableMessages.Enqueue(timestamp, message);
                                    if (ReceivedUnreliableMessages.GetCount() >= QUEUE_WARN_THRESHOLD)
                                    {
                                        LogQueue.LogWarning("The network data queue for unreliable messages has over {0} messages queued! Do you poll them somewhere?");
                                    }

                                    if ((Owner.EventFlags & ENetworkEventReportFlags.ReceivedMessage) != 0)
                                    {
                                        Owner.AddEvent(new ConnectionEvent
                                        {
                                            Connection = new ConnectionHandle(Handle),
                                            EventType = ConnectionEvent.EType.ReceivedMessage
                                        }, ENetworkEventReportFlags.ReceivedMessage);
                                    }

                                    int remanining = CHANNEL_BUFFER_SIZE - UnreliableReceiveBuffer.Head;
                                    if (remanining > 0)
                                    {
                                        Array.Copy(UnreliableReceiveBuffer.Buffer, UnreliableReceiveBuffer.Head, UnreliableReceiveBuffer.Buffer, 0, remanining);
                                    }
                                    UnreliableReceiveBuffer.Head = 0;
                                }
                            }
                        }

                        // SENDING - UDP
                        if (Owner.Server != null || RemoteHandle > 0)
                        {
                            byte[] messageData;
                            while (UnreliableSendBuffer.TryDequeue(out messageData))
                            {
                                byte[] timestamp = BitConverter.GetBytes(UnreliableTime++);
                                byte[] msgLength = BitConverter.GetBytes((ushort)messageData.Length);
                                byte[] sendData = new byte[(Owner.Server == null ? sizeof(ulong) : 0) + sizeof(ulong) + sizeof(ushort) + messageData.Length];

                                int offset = 0;
                                if (Owner.Server == null)
                                {
                                    byte[] handle = BitConverter.GetBytes(RemoteHandle);
                                    Array.Copy(handle, 0, sendData, offset, handle.Length); offset += handle.Length;
                                }
                                Array.Copy(timestamp, 0, sendData, offset, timestamp.Length); offset += timestamp.Length;
                                Array.Copy(msgLength, 0, sendData, offset, msgLength.Length); offset += msgLength.Length;
                                Array.Copy(messageData, 0, sendData, offset, messageData.Length);

                                if (Rand.Next(0, 100) > Owner.NetworkErrorEmulationLevel)
                                {
                                    Thread.Sleep(Rand.Next(0, Owner.NetworkErrorEmulationDelay));
                                    try
                                    {
                                        UnrealiableSend.Send(sendData, sendData.Length);
                                        LogQueue.LogInfo("Sending UDP package: {0}", new object[] { sendData.Length });
                                    }
                                    catch (Exception e)
                                    {
                                        LogQueue.LogWarning(e.Message);
                                        Reliable.Close();
                                        return;
                                    }
                                }
                            }
                        }
                    }

                    if ((DateTime.Now - LastPing).TotalSeconds >= PING_TIMEOUT)
                    {
                        LogQueue.LogWarning("Connection to '{0}' lost due to ping timeout!", new object[] { RemoteReliable.Address.ToString() });
                        Reliable.Close();
                        return;
                    }
                }
            }
        }
    }
}