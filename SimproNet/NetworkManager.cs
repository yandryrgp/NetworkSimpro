#if UNITY_2017_1_OR_NEWER
using System;
#endif
using System.Net;

namespace SimproNET
{
    /// <summary>
    /// Administrador principal de red.
    /// Gestiona clientes/servidor, conexiones y el envío/recepción de mensajes tipados.
    /// </summary>
    public class NetworkManager
    {
        /// <summary>
        /// Evento disparado cuando un cliente se conecta.
        /// </summary>
        public event EventHandler<ConnectionEventArgs> ClientConnected;

        /// <summary>
        /// Evento disparado cuando un cliente se desconecta.
        /// </summary>
        public event EventHandler<ConnectionEventArgs> ClientDisconnected;

        /// <summary>
        /// Evento disparado cuando se recibe un mensaje de red tipado.
        /// </summary>
        public event EventHandler<ReceivedNetworkDataEventArgs> DataReceived;

        /// <summary>
        /// Array que almacena los tipos de datos de red registrados por ID (0-255).
        /// </summary>
        private readonly Type[] networkDataTypes = new Type[256];

        /// <summary>
        /// Servicio de red subyacente que maneja TCP/UDP.
        /// </summary>
        private readonly NetworkService net = new NetworkService();

        /// <summary>
        /// Estado actual de la red.
        /// </summary>
        public ENetworkState State => net.GetState();

        /// <summary>
        /// Indica si el manager está funcionando como servidor.
        /// </summary>
        public bool IsServer => net.IsServer();

        /// <summary>
        /// Registra un tipo de dato de red con un ID único (0-255).
        /// </summary>
        /// <typeparam name="T">Tipo que hereda de NetworkData</typeparam>
        /// <param name="id">ID único del tipo de dato</param>
        /// <exception cref="InvalidOperationException">Si el ID ya está registrado</exception>
        public void RegisterNetworkData<T>(byte id) where T : NetworkData
        {
            if (networkDataTypes[id] != null)
                throw new InvalidOperationException($"ID {id} already registered");

            networkDataTypes[id] = typeof(T);
        }

        /// <summary>
        /// Inicia un servidor TCP/UDP con un número máximo de clientes.
        /// </summary>
        public bool StartServer(int tcp, int udp, int maxClients)
            => net.StartServer(tcp, udp, maxClients);

        /// <summary>
        /// Conecta este manager como cliente a un servidor remoto.
        /// </summary>
        /// <param name="ip">IP del servidor</param>
        /// <param name="tcp">Puerto TCP</param>
        /// <param name="udp">Puerto UDP</param>
        /// <returns>True si la conexión se inició correctamente</returns>
        public bool StartClient(string ip, int tcp, int udp)
        {
            if (!IPAddress.TryParse(ip, out var address))
                throw new ArgumentException("Invalid IP");

            return net.StartClient(address, tcp, udp);
        }

        /// <summary>
        /// Actualiza el manager de red.
        /// Procesa eventos de conexión/desconexión y mensajes entrantes.
        /// </summary>
        public void Update()
        {
            // Procesar eventos de conexión/desconexión
            while (net.GetNextEvent(out ConnectionEvent e))
            {
                if (e.EventType == ConnectionEvent.EType.Connected)
                    ClientConnected?.Invoke(this, new ConnectionEventArgs(e.Connection));

                if (e.EventType == ConnectionEvent.EType.Disconnected)
                    ClientDisconnected?.Invoke(this, new ConnectionEventArgs(e.Connection));
            }

            // Si la red no está corriendo, no procesar mensajes
            if (net.GetState() != ENetworkState.Running)
                return;

            // Procesar mensajes entrantes
            while (net.GetNextMessage(out byte[] msg, out ConnectionHandle conn, out ENetChannel ch))
            {
                byte type = msg[0]; // Primer byte: ID del tipo de mensaje
                var dataType = networkDataTypes[type];

                if (dataType == null)
                    continue; // Si no hay tipo registrado, ignorar

                // Crear instancia del tipo correcto y deserializar
                var data = (NetworkData)Activator.CreateInstance(dataType);
                data.Deserialize(msg);

                if (IsServer)
                    BroadcastExeption(conn, ch, data);

                // Disparar evento de datos recibidos
                DataReceived?.Invoke(this,
                    new ReceivedNetworkDataEventArgs(conn, data, type));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="handle"></param>
        /// <returns></returns>
        public string GetConnectionName(ConnectionHandle handle)
        {
            if (net.GetState() != ENetworkState.Running)
                return "";

            return net.GetConnectionName(handle);
        }

        /// <summary>
        /// Obtiene la lista de conexiones activas.
        /// </summary>
        /// <returns>Arreglo de ConnectionHandle de clientes conectados</returns>
        public ConnectionHandle[] GetConnections()
        {
            if (net.GetState() != ENetworkState.Running)
                return new ConnectionHandle[0];

            return net.GetConnections();
        }

        /// <summary>
        /// Envía un mensaje a un cliente específico.
        /// </summary>
        /// <param name="conn">Conexión destino</param>
        /// <param name="ch">Canal de red (Reliable / Unreliable)</param>
        /// <param name="data">Datos serializables</param>
        public void Send(ConnectionHandle conn, ENetChannel ch, NetworkData data)
            => net.SendMessage(conn, ch, data.Serialize());

        /// <summary>
        /// Envía un mensaje a todos los clientes conectados.
        /// </summary>
        /// <param name="ch">Canal de red</param>
        /// <param name="data">Datos serializables</param>
        public void Broadcast(ENetChannel ch, NetworkData data)
            => net.BroadcastMessage(ch, data.Serialize());

        /// <summary>
        /// Envía un mensaje a todos los clientes conectados execto el q envia.
        /// </summary>
        /// <param name="conn">Conexión a evitar</param>
        /// <param name="ch">Canal de red</param>
        /// <param name="data">Datos serializables</param>
        public void BroadcastExeption(ConnectionHandle conn, ENetChannel ch, NetworkData data)
            => net.BroadcastMessageExeption(conn, ch, data.Serialize());

        /// <summary>
        /// Cierra la red, desconectando clientes y liberando recursos.
        /// </summary>
        public void Close() => net.Close();
    }
}
