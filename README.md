# NetworkSimpro (SimproNET)

Biblioteca de red para Unity que ofrece un `NetworkManager` con soporte para TCP/UDP, mensajes tipados y un componente listo para usar en GameObjects. Incluye serialización automática de mensajes con IDs y utilidades para integrar en proyectos Unity 2017.1 o superior. 【F:SimproNet/NetworkManager.cs†L1-L175】【F:SimproNet/NetworkService.cs†L1-L120】【F:SimproNet/unity/NetworkComponent_UNITY.cs†L1-L120】【F:SimproNet/Serialization.cs†L90-L160】

## Características

- **Cliente/servidor TCP y UDP** con canales confiables y no confiables (`Reliable` / `Unreliable`).【F:SimproNet/NetworkService.cs†L11-L38】
- **Eventos de conexión y recepción de datos** para reaccionar a la red desde el manager principal.【F:SimproNet/NetworkManager.cs†L14-L60】
- **Mensajes tipados** con registro por ID (0-255) y deserialización automática por reflexión.【F:SimproNet/NetworkManager.cs†L31-L107】【F:SimproNet/Serialization.cs†L90-L160】
- **Componente Unity** que inicializa y actualiza la red automáticamente (`NetworkComponent_UNITY`).【F:SimproNet/unity/NetworkComponent_UNITY.cs†L8-L120】
- **Mensajes JSON de ejemplo** para comandos de tipo TCP (`TcpMessage`, `MessageTypeTCP`).【F:SimproNet/unity/NetworkComponent_UNITY.cs†L120-L220】

## Estructura del proyecto

```
/
├─ SimproNet/                  # Núcleo de la librería
│  ├─ NetworkManager.cs        # API principal y eventos
│  ├─ NetworkService.cs        # Implementación TCP/UDP
│  ├─ Serialization.cs         # Serialización de NetworkData
│  └─ unity/                   # Integración con Unity
│     └─ NetworkComponent_UNITY.cs
└─ Manager_Nerwork.cs          # Ejemplo de uso en escena Unity
```

## Requisitos

- Unity 2017.1 o superior (el código está protegido con `UNITY_2017_1_OR_NEWER`).【F:SimproNet/NetworkManager.cs†L1-L3】【F:SimproNet/unity/NetworkComponent_UNITY.cs†L1-L4】
- Newtonsoft.Json para serializar mensajes JSON en el ejemplo de Unity.【F:SimproNet/unity/NetworkComponent_UNITY.cs†L1-L4】【F:SimproNet/unity/NetworkComponent_UNITY.cs†L150-L210】

## Uso básico

### 1) Crear el manager y registrar tipos de mensaje

```csharp
var manager = new SimproNET.NetworkManager();
manager.RegisterNetworkData<NetMessageJson>(1);
```

> El registro por ID permite que el primer byte del payload indique el tipo de mensaje y se deserialice automáticamente al recibirlo.【F:SimproNet/NetworkManager.cs†L31-L107】【F:SimproNet/Serialization.cs†L126-L160】

### 2) Iniciar servidor o cliente

```csharp
// Servidor
manager.StartServer(tcpPort, udpPort, maxClients);

// Cliente
manager.StartClient("127.0.0.1", tcpPort, udpPort);
```

Las APIs de inicio validan la IP y exponen el estado a través de `State` e `IsServer`.【F:SimproNet/NetworkManager.cs†L31-L85】

### 3) Procesar eventos y mensajes

```csharp
manager.ClientConnected += (sender, args) => { /* ... */ };
manager.ClientDisconnected += (sender, args) => { /* ... */ };
manager.DataReceived += (sender, args) =>
{
    // args.Data contiene el NetworkData deserializado
};

// En el Update de Unity:
manager.Update();
```

`Update()` consume eventos de conexión y mensajes entrantes desde el servicio de red y dispara los eventos adecuados.【F:SimproNet/NetworkManager.cs†L87-L132】

### 4) Enviar mensajes

```csharp
manager.Send(connection, ENetChannel.Reliable, networkData);
manager.Broadcast(ENetChannel.Unreliable, networkData);
```

Incluye un broadcast que excluye al remitente en modo servidor (`BroadcastExeption`).【F:SimproNet/NetworkManager.cs†L148-L174】

## Integración con Unity (componente)

Agrega `NetworkComponent_UNITY` a un GameObject para inicializar y actualizar el manager automáticamente. El componente:

- Carga configuración local, registra el tipo `NetMessageJson` y engancha eventos de conexión/datos.【F:SimproNet/unity/NetworkComponent_UNITY.cs†L24-L93】
- Expone `StartServer`, `StartClient` y `SendMsgTest` desde el menú de contexto de Unity.【F:SimproNet/unity/NetworkComponent_UNITY.cs†L53-L83】
- Llama a `Manager.Update()` en cada frame y a `Manager.Close()` al cerrar la aplicación.【F:SimproNet/unity/NetworkComponent_UNITY.cs†L92-L118】

Ejemplo de escucha en un MonoBehaviour:

```csharp
public class Manager_Network : MonoBehaviour
{
    private NetworkComponent_UNITY _netManager;

    private void Awake()
    {
        _netManager = gameObject.AddComponent<NetworkComponent_UNITY>();
        _netManager.ReciveTcpMessage += OnTcpMessage;
    }

    private void OnTcpMessage(object sender, TcpMessage e)
    {
        // Manejar comandos
    }
}
```

Ejemplo basado en `Manager_Nerwork.cs` del repositorio.【F:Manager_Nerwork.cs†L1-L45】

## Mensajes JSON (ejemplo)

`TcpMessage` encapsula un tipo (`MessageTypeTCP`) y un payload JSON, permitiendo serializar y deserializar fácilmente mensajes de control (play/pause/etc.).【F:SimproNet/unity/NetworkComponent_UNITY.cs†L120-L210】

```csharp
var json = TcpMessage.Create(MessageTypeTCP.Play, new { level = 1 });
var data = new NetMessageJson { Message = json.ToJson() };
manager.Broadcast(ENetChannel.Reliable, data);
```

## Cierre de la red

Llama a `Close()` cuando la aplicación finalice para liberar sockets y desconectar clientes correctamente.【F:SimproNet/NetworkManager.cs†L172-L175】
