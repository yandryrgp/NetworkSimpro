using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
#if UNITY_2017_1_OR_NEWER
using System;
#endif

namespace SimproNET
{
    /// <summary>
    /// Tipos de log soportados por el sistema
    /// </summary>
    public enum ELogType
    {
        /// <summary>
        /// Mensaje informativo
        /// </summary>
        Info,
        /// <summary>
        /// Mensaje de advertencia
        /// </summary>
        Warning,
        /// <summary>
        /// Mensaje de error
        /// </summary>
        Error
    }

    /// <summary>
    /// Estructura que representa un mensaje de log
    /// </summary>
    public struct LogMessage
    {
        /// <summary>
        /// Tipo de log (Info, Warning, Error)
        /// </summary>
        public ELogType Type;
        /// <summary>
        /// Mensaje formateado
        /// </summary>
        public string Message;
    }

    /// <summary>
    /// Cola de mensajes de log segura para múltiples hilos (thread-safe).
    /// 
    /// Permite enviar y recibir mensajes de log desde cualquier hilo,
    /// ideal para sistemas de red, simulación o procesamiento en background.
    /// 
    /// Todos los métodos de log rellenan automáticamente:
    /// - Nombre del archivo llamador
    /// - Número de línea
    /// 
    /// Ejemplo de uso:
    /// <code>
    /// LogQueue.LogInfo("Conexión establecida con el servidor");
    /// </code>
    /// </summary>
    public static class LogQueue
    {
        /// <summary>
        /// Cola concurrente interna donde se almacenan los mensajes
        /// </summary>
        static ConcurrentQueue<LogMessage> Logs = new ConcurrentQueue<LogMessage>();

        /// <summary>
        /// Método interno común para registrar mensajes
        /// </summary>
        static void Log(
            ELogType type,
            string format,
            object[] args = null,
            [CallerFilePath] string caller = null,
            [CallerLineNumber] int lineNumber = 0)
        {
            try
            {
                // Obtener solo el nombre del archivo (sin la ruta completa)
                caller = System.IO.Path.GetFileName(caller);

                // Evitar null en los argumentos
                args = args ?? Array.Empty<object>();

                LogMessage msg;
                msg.Type = type;

                // Formato final del mensaje con origen
                msg.Message =
                    string.Format("[{0}:{1}] ", caller, lineNumber) +
                    string.Format(format, args);

                // Encolar mensaje de forma segura
                Logs.Enqueue(msg);
            }
            catch (Exception)
            {
                // Se ignoran errores para evitar romper el flujo
                // del sistema por fallos de logging
            }
        }

        /// <summary>
        /// Obtiene el siguiente mensaje de log disponible (si existe)
        /// </summary>
        /// <param name="nextLogMessage">Mensaje extraído de la cola</param>
        /// <returns>true si se obtuvo un mensaje, false si la cola está vacía</returns>
        public static bool GetNext(out LogMessage nextLogMessage)
        {
            return Logs.TryDequeue(out nextLogMessage);
        }

        /// <summary>
        /// Registra un mensaje informativo
        /// </summary>
        public static void LogInfo(
            string format,
            object[] args = null,
            [CallerFilePath] string caller = null,
            [CallerLineNumber] int lineNumber = 0)
        {
            Log(ELogType.Info, format, args, caller, lineNumber);
        }

        /// <summary>
        /// Registra un mensaje de advertencia
        /// </summary>
        public static void LogWarning(
            string format,
            object[] args = null,
            [CallerFilePath] string caller = null,
            [CallerLineNumber] int lineNumber = 0)
        {
            Log(ELogType.Warning, format, args, caller, lineNumber);
        }

        /// <summary>
        /// Registra un mensaje de error
        /// </summary>
        public static void LogError(
            string format,
            object[] args = null,
            [CallerFilePath] string caller = null,
            [CallerLineNumber] int lineNumber = 0)
        {
            Log(ELogType.Error, format, args, caller, lineNumber);
        }
    }
}
