#if UNITY_2017_1_OR_NEWER
using Newtonsoft.Json;
using System;
using UnityEngine;

namespace SimproNET.Unity
{
    /// <summary>
    /// Componente Unity para integrar el <see cref="NetworkManager"/> en un GameObject.
    /// Gestiona la inicialización, actualización y cierre de la red automáticamente.
    /// </summary>
    public class NetworkComponent_UNITY : MonoBehaviour
    {
        [Header("SimproNet Config")]
        [SerializeField] int portReliable;
        [SerializeField] int portUnreliable;
        [SerializeField] string ipServer;

        [Header("Prueba de envio")]
        [SerializeField] private string msg;

        private string nameSpace = "NetworkManager";

        /// <summary>
        /// Evento disparado cuando un cliente se conecta.
        /// </summary>
        public event EventHandler<TcpMessage> ReciveTcpMessage;

        /// <summary>
        /// Instancia del <see cref="NetworkManager"/> que maneja la red.
        /// Se inicializa en Awake y se usa durante la ejecución.
        /// </summary>
        public NetworkManager Manager { get; private set; }

        /// <summary>
        /// Inicialización del componente y del NetworkManager.
        /// Se registra eventos de conexión y desconexión de clientes.
        /// </summary>
        void Awake()
        {
            Manager = new NetworkManager();

            // Evento de conexión de un cliente
            Manager.ClientConnected += Manager_ClientConnected;

            // Evento de desconexión de un cliente
            Manager.ClientDisconnected += Manager_ClientDisconnected;

            Manager.DataReceived += Manager_DataReceived;

            Manager.RegisterNetworkData<NetMessageJson>(1);

            LoadConfig();

        }

        /// <summary>
        /// Cargar datos de configuracion (config.ini)
        /// </summary>
        private void LoadConfig()
        {
            portReliable = Configuracion.Instance.port_networkTCP;
            portUnreliable = Configuracion.Instance.port_networkUDP;
            ipServer = Configuracion.Instance.ip_network;
        }

        [ContextMenu("Start Server")]
        public void StartServer()
        {
            if (Manager.StartServer(portReliable, portUnreliable, 10))
                Debug.Log($"[{nameSpace}] Servidor iniciado  ({portReliable}:{portUnreliable})");
        }

        [ContextMenu("Start Client")]
        public void StartClient()
        {
            if (Manager.StartClient(ipServer, portReliable, portUnreliable))
                Debug.Log($"[{nameSpace}] Cliente conectado a server ({ipServer}:{portReliable}:{portUnreliable})");
        }

        [ContextMenu("Send Msg")]
        public void SendMsgTest()
        {
            var json = TcpMessage.Create(MessageTypeTCP.Play, msg);

            var data = new NetMessageJson { Message = json.ToJson() };

            Manager.Broadcast(ENetChannel.Reliable, data);
        }


        /// <summary>
        /// Actualiza el NetworkManager cada frame.
        /// Permite procesar eventos de red y mensajes entrantes.
        /// </summary>
        void Update()
        {
            Manager.Update();
        }

        /// <summary>
        /// Se llama cuando la aplicación cierra.
        /// Cierra el NetworkManager correctamente para liberar sockets y recursos.
        /// </summary>
        void OnApplicationQuit()
        {
            Manager.Close();
        }


        private void Manager_DataReceived(object sender, ReceivedNetworkDataEventArgs e)
        {
            if (e.Data is NetMessageJson msg)
            {
                var tcpMessage = TcpMessage.FromJson(msg.Message);
                ReciveTcpMessage?.Invoke(this, tcpMessage);
            }
        }

        private void Manager_ClientDisconnected(object sender, ConnectionEventArgs e)
        {
            if (Manager.IsServer)
                Debug.Log($"[{nameSpace}] [SERVER] Cliente desconectado");
            else
                Debug.Log($"[{nameSpace}] [CLIENT] Desconectado al servidor");
        }

        private void Manager_ClientConnected(object sender, ConnectionEventArgs e)
        {
            if (Manager.IsServer)
                Debug.Log($"[{nameSpace}] [SERVER] Cliente conectado");
            else
                Debug.Log($"[{nameSpace}] [CLIENT] Conectado al servidor");

            var b = Manager.GetConnectionName(e.Handle);
            Debug.Log($"[{nameSpace}] {b[0]}");
        }


    }

    [NetworkDataID(1)]
    public class NetMessageJson : NetworkData
    {
        public string SendName = "PlayerConsole";
        public string Message;
    }
    /// <summary>
    /// Tipos de mensajes TCP disponibles.
    /// </summary>
    public enum MessageTypeTCP
    {
        SendDatalectronic, // Recepcion de cambios de la electronica
        Play,              // Comando de reproducción
        Pause,             // Comando de pausa
        Reset,             // Comando de reinicio
        Finish,            // Comando de finalización
        SendExercise,      // Envío de ejercicio        
        Close,             // Comando de cierre
        Data,              // Comando enviar datos del ejercicio
        VistaPlanta,       // Comando cambiar vista en planta
        VistaMira,         // Comando cambari vista a mira 
        Explosion,         // Comando activar explosiones
        Fuego,             // Comando activar fuego
        CortinasHumo,      // Comando activar humo
        Bengala,           // Comando activra bengala
        Lluvia,            // Comadno activar lluvia
        Niebla,            // Comando activar niebal
        Resultado,         // Comando mostrar resultado
        Impacto,           // Comadno enviar impacto
        Disparo,           // Comando enviar disparo
        Disconnect,
        LoadScene
    }

    public class TcpMessage
    {
        public MessageTypeTCP Type { get; set; }
        public string Payload { get; set; } = null;

        public Guid MessageId { get; set; } = Guid.NewGuid();
        public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
        }

        //===== Deserializar un mensaje JSON a objeto TcpMessage =====
        public static TcpMessage FromJson(string json)
        {
            return JsonConvert.DeserializeObject<TcpMessage>(json);
        }

        //===== Crear un mensaje con objeto como payload (auto serializa) =====
        public static TcpMessage Create<T>(MessageTypeTCP type, T? payloadObject)
        {
            return new TcpMessage
            {
                Type = type,
                Payload = JsonConvert.SerializeObject(payloadObject)
            };
        }

        //===== Obtener el objeto original desde el payload (auto deserializa) =====
        public T GetPayload<T>()
        {
            return JsonConvert.DeserializeObject<T>(Payload);
        }
    }
}
#endif
