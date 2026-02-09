#if UNITY_2017_1_OR_NEWER
using System;
using System.Collections.Generic;
#endif
namespace SimproNET
{
    /// <summary>
    /// Cola ordenada segura para hilos (thread-safe).
    /// Los elementos encolados se insertan inmediatamente en la posición
    /// correcta según su clave (TKey), manteniendo el orden.
    /// </summary>
    /// <typeparam name="TKey">Tipo de clave para ordenar, debe ser struct (valor)</typeparam>
    /// <typeparam name="TValue">Tipo de valor a almacenar, debe ser clase (referencia)</typeparam>
    public class ConcurrentSortedQueue<TKey, TValue> where TKey : struct where TValue : class
    {
        // Lista interna ordenada por clave
        private SortedList<TKey, TValue> List = new SortedList<TKey, TValue>();

        // Lock para sincronización thread-safe
        private readonly object Lock = new object();

        /// <summary>
        /// Agrega un nuevo elemento a la cola en la posición ordenada
        /// según su clave. Si la clave ya existe, no se agrega.
        /// </summary>
        /// <param name="key">Clave para ordenar</param>
        /// <param name="value">Valor a encolar</param>
        /// <returns>True si se agregó correctamente; false si la clave ya existía</returns>
        public bool Enqueue(TKey key, TValue value)
        {
            lock (Lock) // Se asegura acceso exclusivo
            {
                try
                {
                    List.Add(key, value); // Inserción ordenada
                    return true;
                }
                catch (ArgumentException)
                {
                    // La clave ya existe
                    LogQueue.LogError(
                        "Cannot add element ({}, {}) to list, key already exists!",
                        new object[] { key, value });
                    return false;
                }
            }
        }

        /// <summary>
        /// Intenta sacar el primer elemento (el de menor clave) de la cola.
        /// </summary>
        /// <param name="value">Valor extraído</param>
        /// <returns>True si se extrajo un elemento; false si la cola estaba vacía</returns>
        public bool TryDequeue(out TValue value)
        {
            lock (Lock)
            {
                if (List.Values.Count == 0)
                {
                    value = null;
                    return false;
                }

                value = List.Values[0]; // Primer elemento (menor clave)
                List.RemoveAt(0); // Eliminación del elemento extraído
            }
            return true;
        }

        /// <summary>
        /// Limpia la cola completamente.
        /// </summary>
        public void Clear()
        {
            lock (Lock)
            {
                List.Clear();
            }
        }

        /// <summary>
        /// Obtiene la cantidad de elementos actualmente en la cola.
        /// </summary>
        /// <returns>Número de elementos</returns>
        public int GetCount()
        {
            int count;
            lock (Lock)
            {
                count = List.Count;
            }
            return count;
        }
    }
}
