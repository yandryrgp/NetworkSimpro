#if UNITY_2017_1_OR_NEWER
using System;
#endif
namespace SimproNET
{
    /// <summary>
    /// Evento de conexión o desconexión de red
    /// </summary>
    public class ConnectionEventArgs : EventArgs
    {
        public ConnectionHandle Handle;
        public ConnectionEventArgs(ConnectionHandle h) => Handle = h;
    }

    /// <summary>
    /// Evento de recepción de datos de red
    /// </summary>
    public class ReceivedNetworkDataEventArgs : EventArgs
    {
        public ConnectionHandle Connection;
        public NetworkData Data;
        public byte Type;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="c"></param>
        /// <param name="d"></param>
        /// <param name="t"></param>
        public ReceivedNetworkDataEventArgs(ConnectionHandle c, NetworkData d, byte t)
        {
            Connection = c;
            Data = d;
            Type = t;
        }
    }
}