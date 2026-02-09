using System.Reflection;
using System.Text;

#if UNITY_2017_1_OR_NEWER
using System;
using UnityEngine;
using System.IO;
using Vector3 = UnityEngine.Vector3;
using Vector2 = UnityEngine.Vector2;
using Vector4 = UnityEngine.Vector4;
using Quaternion = UnityEngine.Quaternion;
#endif
namespace SimproNET
{
#if UNITY_2017_1_OR_NEWER
    /// <summary>
    /// Helper estático para serialización y deserialización manual de datos
    /// comunes de Unity hacia/desde byte[].
    /// Diseñado para uso en red (TCP / UDP).
    /// </summary>
    public static class SerializationHelper
    {
        /// <summary>
        /// Lee un valor booleano desde un buffer de bytes.
        /// </summary>
        public static void FromBytes(byte[] data, ref int offset, out bool value)
        {
            Debug.Assert(offset + sizeof(byte) <= data.Length);
            value = data[offset] != 0;
            offset += sizeof(byte);
        }

        /// <summary>
        /// Lee un valor float desde un buffer de bytes.
        /// </summary>
        public static void FromBytes(byte[] data, ref int offset, out float value)
        {
            Debug.Assert(offset + sizeof(float) <= data.Length);
            value = BitConverter.ToSingle(data, offset);
            offset += sizeof(float);
        }

        /// <summary>
        /// Lee un Vector3 desde un buffer de bytes (x, y, z).
        /// </summary>
        public static void FromBytes(byte[] data, ref int offset, out Vector3 vector)
        {
            FromBytes(data, ref offset, out vector.x);
            FromBytes(data, ref offset, out vector.y);
            FromBytes(data, ref offset, out vector.z);
        }

        /// <summary>
        /// Lee un Quaternion desde un buffer de bytes (x, y, z, w).
        /// </summary>
        public static void FromBytes(byte[] data, ref int offset, out Quaternion quat)
        {
            FromBytes(data, ref offset, out quat.x);
            FromBytes(data, ref offset, out quat.y);
            FromBytes(data, ref offset, out quat.z);
            FromBytes(data, ref offset, out quat.w);
        }

        /// <summary>
        /// Escribe un ulong en el buffer de bytes.
        /// </summary>
        public static void ToBytes(ulong value, byte[] data, ref int offset)
        {
            Debug.Assert(offset + sizeof(float) < data.Length);
            byte[] buffer = BitConverter.GetBytes(value);
            Array.Copy(buffer, 0, data, offset, buffer.Length);
            offset += buffer.Length;
        }

        /// <summary>
        /// Escribe un booleano en el buffer de bytes.
        /// </summary>
        public static void ToBytes(bool value, byte[] data, ref int offset)
        {
            Debug.Assert(offset + sizeof(bool) <= data.Length);
            data[offset] = value ? (byte)1 : (byte)0;
            offset += sizeof(byte);
        }

        /// <summary>
        /// Escribe un float en el buffer de bytes.
        /// </summary>
        public static void ToBytes(float value, byte[] data, ref int offset)
        {
            Debug.Assert(offset + sizeof(float) <= data.Length);
            byte[] buffer = BitConverter.GetBytes(value);
            Array.Copy(buffer, 0, data, offset, buffer.Length);
            offset += buffer.Length;
        }

        /// <summary>
        /// Escribe un Vector3 en el buffer de bytes (x, y, z).
        /// </summary>
        public static void ToBytes(in Vector3 vector, byte[] data, ref int offset)
        {
            ToBytes(vector.x, data, ref offset);
            ToBytes(vector.y, data, ref offset);
            ToBytes(vector.z, data, ref offset);
        }

        /// <summary>
        /// Escribe un Quaternion en el buffer de bytes (x, y, z, w).
        /// </summary>
        public static void ToBytes(in Quaternion quat, byte[] data, ref int offset)
        {
            ToBytes(quat.x, data, ref offset);
            ToBytes(quat.y, data, ref offset);
            ToBytes(quat.z, data, ref offset);
            ToBytes(quat.w, data, ref offset);
        }
    }
#endif

    /// <summary>
    /// Atributo que define el identificador único de un tipo de dato de red.
    /// Se utiliza para identificar el tipo al serializar/deserializar.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class NetworkDataIDAttribute : Attribute
    {
        /// <summary>
        /// ID único asociado a este tipo de dato de red.
        /// </summary>
        public byte ID { get; }

        /// <summary>
        /// Constructor del atributo.
        /// </summary>
        /// <param name="id">Identificador único del mensaje</param>
        public NetworkDataIDAttribute(byte id)
        {
            ID = id;
        }
    }

    /// <summary>
    /// Clase base abstracta para cualquier estructura de datos
    /// enviada a través de la red.
    /// Implementa serialización y deserialización automática
    /// usando reflexión.
    /// </summary>
    public abstract class NetworkData
    {
        /// <summary>
        /// Serializa el objeto actual a un arreglo de bytes.
        /// El primer byte siempre corresponde al NetworkDataID.
        /// </summary>
        /// <returns>Datos serializados</returns>
        public virtual byte[] Serialize()
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter bw = new BinaryWriter(ms))
            {
                // Primer byte: ID del tipo de mensaje
                var attr = GetType().GetCustomAttribute<NetworkDataIDAttribute>();
                byte id = attr != null ? attr.ID : (byte)0;
                bw.Write(id);

                // Obtiene todos los campos públicos de instancia
                var fields = GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
                foreach (var f in fields)
                {
                    object value = f.GetValue(this);

                    // Serialización por tipo
                    switch (value)
                    {
                        case string s:
                            byte[] strBytes = Encoding.UTF8.GetBytes(s);
                            bw.Write((short)strBytes.Length);
                            bw.Write(strBytes);
                            break;

                        case int i:
                            bw.Write(i);
                            break;

                        case float fval:
                            bw.Write(fval);
                            break;

                        case byte b:
                            bw.Write(b);
                            break;

                        case bool bo:
                            bw.Write((byte)(bo ? 1 : 0));
                            break;

#if UNITY_2017_1_OR_NEWER
                        case Vector2 v2:
                            bw.Write(v2.x);
                            bw.Write(v2.y);
                            break;

                        case Vector3 v3:
                            bw.Write(v3.x);
                            bw.Write(v3.y);
                            bw.Write(v3.z);
                            break;

                        case Vector4 v4:
                            bw.Write(v4.x);
                            bw.Write(v4.y);
                            bw.Write(v4.z);
                            bw.Write(v4.w);
                            break;

                        case Quaternion q:
                            bw.Write(q.x);
                            bw.Write(q.y);
                            bw.Write(q.z);
                            bw.Write(q.w);
                            break;

                        case Color c:
                            bw.Write(c.r);
                            bw.Write(c.g);
                            bw.Write(c.b);
                            bw.Write(c.a);
                            break;

                        case Color32 c32:
                            bw.Write(c32.r);
                            bw.Write(c32.g);
                            bw.Write(c32.b);
                            bw.Write(c32.a);
                            break;

                        case Bounds b:
                            // center
                            bw.Write(b.center.x);
                            bw.Write(b.center.y);
                            bw.Write(b.center.z);
                            // size
                            bw.Write(b.size.x);
                            bw.Write(b.size.y);
                            bw.Write(b.size.z);
                            break;

                        case Rect r:
                            bw.Write(r.x);
                            bw.Write(r.y);
                            bw.Write(r.width);
                            bw.Write(r.height);
                            break;
#endif
                        default:
                            // Tipo no soportado explícitamente
                            throw new Exception($"Tipo no soportado: {f.FieldType}");
                    }
                }

                return ms.ToArray();
            }
        }

        /// <summary>
        /// Deserializa un arreglo de bytes y rellena los campos públicos
        /// del objeto actual.
        /// </summary>
        /// <param name="data">Datos recibidos desde la red</param>
        public virtual void Deserialize(byte[] data)
        {
            using (MemoryStream ms = new MemoryStream(data))
            using (BinaryReader br = new BinaryReader(ms))
            {
                // ID ya consumido externamente
                br.ReadByte();

                var fields = GetType().GetFields(
                    BindingFlags.Public | BindingFlags.Instance);

                foreach (var f in fields)
                {
                    Type t = f.FieldType;

                    if (t == typeof(string))
                    {
                        short len = br.ReadInt16();
                        byte[] strBytes = br.ReadBytes(len);
                        f.SetValue(this, Encoding.UTF8.GetString(strBytes));
                    }
                    else if (t == typeof(int))
                    {
                        f.SetValue(this, br.ReadInt32());
                    }
                    else if (t == typeof(float))
                    {
                        f.SetValue(this, br.ReadSingle());
                    }
                    else if (t == typeof(double))
                    {
                        f.SetValue(this, br.ReadDouble());
                    }
                    else if (t == typeof(byte))
                    {
                        f.SetValue(this, br.ReadByte());
                    }
                    else if (t == typeof(bool))
                    {
                        f.SetValue(this, br.ReadByte() == 1);
                    }

#if UNITY_2017_1_OR_NEWER
                    else if (t == typeof(Vector2))
                    {
                        f.SetValue(this, new Vector2(
                            br.ReadSingle(),
                            br.ReadSingle()));
                    }
                    else if (t == typeof(Vector3))
                    {
                        f.SetValue(this, new Vector3(
                            br.ReadSingle(),
                            br.ReadSingle(),
                            br.ReadSingle()));
                    }
                    else if (t == typeof(Vector4))
                    {
                        f.SetValue(this, new Vector4(
                            br.ReadSingle(),
                            br.ReadSingle(),
                            br.ReadSingle(),
                            br.ReadSingle()));
                    }
                    else if (t == typeof(Quaternion))
                    {
                        f.SetValue(this, new Quaternion(
                            br.ReadSingle(),
                            br.ReadSingle(),
                            br.ReadSingle(),
                            br.ReadSingle()));
                    }
                    else if (t == typeof(Color))
                    {
                        f.SetValue(this, new Color(
                            br.ReadSingle(),
                            br.ReadSingle(),
                            br.ReadSingle(),
                            br.ReadSingle()));
                    }
                    else if (t == typeof(Color32))
                    {
                        f.SetValue(this, new Color32(
                            br.ReadByte(),
                            br.ReadByte(),
                            br.ReadByte(),
                            br.ReadByte()));
                    }
#endif
                    else
                    {
                        throw new Exception($"Tipo no soportado: {t}");
                    }
                }
            }
        }
    }
}
