using ArtNet.Rdm;
using System.IO;
using System.Net;
using System.Text;

namespace ArtNet.IO
{
    /// <summary>
    /// ArtNet 二进制数据读取器
    /// </summary>
    public class ArtNetBinaryReader : BinaryReader
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="input">数据流</param>
        public ArtNetBinaryReader(Stream input)
            : base(input)
        {
        }

        /// <summary>
        /// 网络字节转为本地（读取16字节）
        /// </summary>
        /// <returns></returns>
        public short ReadNetwork16()
        {
            return (short)IPAddress.NetworkToHostOrder(ReadInt16());
        }

        /// <summary>
        /// 网络字节转为本地（读取32字节）
        /// </summary>
        /// <returns></returns>
        public int ReadNetwork32()
        {
            return (int)IPAddress.NetworkToHostOrder(ReadInt32());
        }

        /// <summary>
        /// 网络字符串转为本地
        /// </summary>
        /// <param name="length">字符串长度</param>
        /// <returns></returns>
        public string ReadNetworkString(int length)
        {
            return Encoding.UTF8.GetString(ReadBytes(length));
        }

        /// <summary>
        /// 获取 UID
        /// </summary>
        /// <returns>UID</returns>
        public UId ReadUId()
        {
            return new UId((ushort)(int)ReadNetwork16(), (uint)ReadNetwork32());
        }
    }
}
