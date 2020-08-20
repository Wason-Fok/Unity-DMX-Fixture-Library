using ArtNet.Rdm;
using System.IO;
using System.Net;
using System.Text;

namespace ArtNet.IO
{
    /// <summary>
    /// ArtNet 二进制写入器
    /// </summary>
    public class ArtNetBinaryWriter : BinaryWriter
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="output">数据流</param>
        public ArtNetBinaryWriter(Stream output)
            : base(output)
        {
        }

        /// <summary>
        /// 写入 byte
        /// </summary>
        /// <param name="value">byte</param>
        public void WriteNetwork(byte value)
        {
            base.Write(IPAddress.HostToNetworkOrder(value));
        }

        /// <summary>
        /// 写入 short
        /// </summary>
        /// <param name="value">short</param>
        public void WriteNetwork(short value)
        {
            base.Write(IPAddress.HostToNetworkOrder(value));
        }

        /// <summary>
        /// 写入 int
        /// </summary>
        /// <param name="value">int</param>
        public void WriteNetwork(int value)
        {
            base.Write(IPAddress.HostToNetworkOrder(value));
        }

        /// <summary>
        /// 写入 string
        /// </summary>
        /// <param name="value">string</param>
        /// <param name="length">长度</param>
        public void WriteNetwork(string value, int length)
        {
            Write(Encoding.UTF8.GetBytes(value.PadRight(length, (char)0x0)));
        }

        /// <summary>
        /// 写入 UID
        /// </summary>
        /// <param name="value">UID</param>
        public void Write(UId value)
        {
            WriteNetwork((short)value.ManufacturerId);
            WriteNetwork((int)value.DeviceId);
        }
    }
}
