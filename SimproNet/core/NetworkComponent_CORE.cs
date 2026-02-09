using SimproNET;
#if UNITY_2017_1_OR_NEWER
using System;
using System.Threading;
#endif
namespace SimproNET_CORE
{
    /// <summary>
    /// Componente para integrar NetworkManager en aplicaciones Core (sin Unity).
    /// Gestiona inicialización, actualización periódica y cierre del NetworkManager automáticamente.
    /// 
    /// <para>
    /// <b>Ejemplo de uso:</b>
    /// <code>
    /// using (var network = new NetworkComponent_CORE())
    /// {
    ///     network.Manager.ClientConnected += (s, e) =&gt; Console.WriteLine($"Cliente conectado: {e.Handle}");
    ///     network.Manager.DataReceived += (s, e) =&gt; Console.WriteLine($"Mensaje recibido: {e.Type}");
    ///     
    ///     network.Manager.StartServer(12345, 12346, 10);
    ///     
    ///     Console.WriteLine("Servidor iniciado. Presiona ENTER para salir...");
    ///     Console.ReadLine();
    /// }
    /// </code>
    /// </para>
    /// </summary>
    public class NetworkComponent_CORE : IDisposable
    {
        /// <summary>
        /// Instancia del NetworkManager que maneja la red.
        /// </summary>
        public NetworkManager Manager { get; private set; }

        /// <summary>
        /// Hilo de actualización periódica del NetworkManager.
        /// </summary>
        private Thread updateThread;

        /// <summary>
        /// Señal para detener el hilo de actualización.
        /// </summary>
        private bool shutdown = false;

        /// <summary>
        /// Intervalo de actualización en milisegundos.
        /// </summary>
        private readonly int updateIntervalMs;

        /// <summary>
        /// Constructor. Inicia el NetworkManager y el hilo de actualización.
        /// </summary>
        /// <param name="updateIntervalMs">Intervalo de actualización en ms (por defecto 16ms ≈ 60fps)</param>
        public NetworkComponent_CORE(int updateIntervalMs = 16)
        {
            this.updateIntervalMs = updateIntervalMs;
            Manager = new NetworkManager();

            // Crear y arrancar el hilo de actualización
            updateThread = new Thread(UpdateLoop)
            {
                IsBackground = true,
                Name = "NetworkManagerUpdateThread"
            };
            updateThread.Start();
        }

        /// <summary>
        /// Loop de actualización periódica.
        /// Llama a Manager.Update() según el intervalo definido.
        /// </summary>
        private void UpdateLoop()
        {
            while (!shutdown)
            {
                try
                {
                    Manager.Update();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error en NetworkManager Update: {ex.Message}");
                }

                Thread.Sleep(updateIntervalMs);
            }
        }

        /// <summary>
        /// Cierra el NetworkManager y detiene el hilo de actualización.
        /// </summary>
        public void Close()
        {
            shutdown = true;
            updateThread?.Join();
            Manager.Close();
        }

        /// <summary>
        /// Implementación de IDisposable.
        /// </summary>
        public void Dispose()
        {
            Close();
        }
    }
}
