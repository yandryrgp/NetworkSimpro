# SimproNET

SimproNET es una librería de red ligera en **C# (.NET / Unity)** orientada a simuladores y juegos multijugador **server-authoritative**, con soporte para **TCP y UDP**, mensajes tipados y un modelo de actualización compatible con el ciclo `Update()` de Unity.

---

## 🎯 Objetivos del proyecto

* Comunicación cliente–servidor simple y controlada
* Arquitectura clara y extensible
* Bajo acoplamiento con Unity
* Evitar hilos visibles y lógica compleja
* Ideal para simuladores, juegos tácticos y proyectos técnicos

---

## 🧱 Arquitectura general

SimproNET está dividida en capas bien definidas:

```
[ NetworkManager ]  ← API pública, eventos, lógica de alto nivel
        |
        v
[ NetworkService ]  ← TCP / UDP, sockets, colas, estados
        |
        v
[ Socket Layer ]    ← Comunicación de bajo nivel
```

### Componentes principales

| Componente       | Responsabilidad                                |
| ---------------- | ---------------------------------------------- |
| `NetworkManager` | Gestión de mensajes, eventos y flujo principal |
| `NetworkService` | Manejo de sockets, conexiones y colas          |
| `NetworkData`    | Base para mensajes serializables               |
| `Serialization`  | Serialización / deserialización binaria        |
| `Events`         | Eventos de conexión y recepción de datos       |
| `UNITY`          | Adaptación específica para Unity               |

---

## 🔁 Modelo de ejecución

SimproNET utiliza un **modelo pull-based**, pensado para ejecutarse desde el `Update()` de Unity o un loop manual en .NET.

No crea hilos visibles ni callbacks asíncronos complejos.

---

## 🔄 Diagrama de flujo principal

### Ciclo de `NetworkManager.Update()`

```
┌──────────────────────────┐
│ NetworkManager.Update()  │
└─────────────┬────────────┘
              │
              v
   ┌──────────────────────┐
   │ Leer eventos de red  │
   │ (connect / disconnect)
   └─────────────┬────────┘
                 │
                 v
      ┌───────────────────┐
      │ ¿Estado Running?  │─── NO ──► Fin
      └─────────┬─────────┘
                │ SI
                v
   ┌────────────────────────┐
   │ Leer mensajes entrantes │
   └─────────────┬──────────┘
                 │
                 v
   ┌────────────────────────┐
   │ Resolver tipo (ID[0])  │
   └─────────────┬──────────┘
                 │
                 v
   ┌────────────────────────┐
   │ Deserializar NetworkData│
   └─────────────┬──────────┘
                 │
        ┌────────▼────────┐
        │ ¿Es Servidor?   │
        └───────┬─────────┘
                │ SI
                v
   ┌────────────────────────┐
   │ Broadcast (excepto src)│
   └─────────────┬──────────┘
                 │
                 v
   ┌────────────────────────┐
   │ Evento DataReceived    │
   └────────────────────────┘
```

---

## 📦 Sistema de mensajes

### NetworkData

Todos los mensajes de red deben heredar de `NetworkData`.

Responsabilidades:

* Serializar datos a `byte[]`
* Deserializar desde `byte[]`

Ejemplo conceptual:

* Byte 0 → ID del mensaje
* Bytes restantes → payload

Los tipos se registran mediante:

```
RegisterNetworkData<T>(byte id)
```

Internamente se usa un array de 256 posiciones para resolución rápida por ID.

---

## 🌐 Modelo cliente–servidor

* **Servidor**

  * Recibe mensajes
  * Procesa lógica
  * Reenvía información relevante

* **Cliente**

  * Envía inputs o solicitudes
  * Recibe estados y eventos

El servidor es siempre la autoridad.

---

## 🎮 Integración con Unity

SimproNET detecta automáticamente el entorno Unity mediante directivas de compilación:

```
#if UNITY_2017_1_OR_NEWER
```

Ventajas:

* El núcleo funciona en .NET puro
* La capa Unity solo adapta el ciclo de vida
* Fácil reutilización en herramientas externas o servidores dedicados

---

## ✅ Ventajas

* Arquitectura simple y comprensible
* Fácil de extender
* Bajo coste de CPU
* Ideal para simuladores y juegos tácticos
* Sin dependencias externas pesadas

---

## ⚠️ Consideraciones

* No incluye seguridad avanzada por defecto
* El rebroadcast del servidor es automático
* No implementa predicción ni interpolación

Estas decisiones son intencionales para mantener el núcleo ligero y controlable.

---

## 📌 Casos de uso recomendados

* Simuladores técnicos o militares
* Juegos multijugador pequeños/medios
* Prototipos de red
* IA distribuida
* Herramientas de entrenamiento

---

## 📄 Licencia

Definida por el autor del proyecto.

---

## ✍️ Autor

SimproNET es un framework de red diseñado para control total del flujo y la lógica, priorizando claridad y extensibilidad sobre automatismos opacos.
