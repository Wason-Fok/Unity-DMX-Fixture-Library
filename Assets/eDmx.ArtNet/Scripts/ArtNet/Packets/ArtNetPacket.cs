using ArtNet.Enums;
using System.IO;
using ArtNet.IO;

namespace ArtNet.Packets
{
    /// <summary>
    /// ArtNet 数据包类基类
    /// </summary>
    public class ArtNetPacket
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="opCode">ArtNet 操作码</param>
        public ArtNetPacket(ArtNetOpCodes opCode)
        {
            OpCode = opCode;
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="data">ArtNet 接收到的数据</param>
        public ArtNetPacket(ArtNetRecieveData data)
        {
            ArtNetBinaryReader packetReader = new ArtNetBinaryReader(new MemoryStream(data.buffer));
            ReadData(packetReader);
        }

        public byte[] ToArray()
        {
            MemoryStream stream = new MemoryStream();
            WriteData(new ArtNetBinaryWriter(stream));
            return stream.ToArray();
        }

        #region ArtNet 数据包属性

        private string protocol = "Art-Net";

        /// <summary>
        /// 协议名称
        /// </summary>
        public string Protocol
        {
            get { return protocol; }
            protected set
            {
                if (value.Length > 8)
                    protocol = value.Substring(0, 8);
                else
                    protocol = value;
            }
        }


        private short version = 14;

        /// <summary>
        /// 版本
        /// </summary>
        public short Version
        {
            get { return version; }
            protected set { version = value; }
        }

        private ArtNetOpCodes opCode = ArtNetOpCodes.None;

        /// <summary>
        /// 操作码
        /// </summary>
        public virtual ArtNetOpCodes OpCode
        {
            get { return opCode; }
            protected set { opCode = value; }
        }

        #endregion

        /// <summary>
        /// 解析获取到的数据流
        /// </summary>
        /// <param name="data">ArtNet 二进制读取器</param>
        public virtual void ReadData(ArtNetBinaryReader data)
        {
            // 前 8 位为 ArtNet ID
            Protocol = data.ReadNetworkString(8);
            // 16 为 ArtNet OpCode 操作码
            OpCode = (ArtNetOpCodes)data.ReadNetwork16();

            // 由于某些原因，轮询包头不包括版本
            if (OpCode != ArtNetOpCodes.PollReply)
                // 16位 ProtVer 协议版本（>= 14）
                Version = data.ReadNetwork16();

        }

        /// <summary>
        /// 向数据流写入数据
        /// </summary>
        /// <param name="data">ArtNet 二进制写入器</param>
        public virtual void WriteData(ArtNetBinaryWriter data)
        {
            data.WriteNetwork(Protocol, 8);
            data.WriteNetwork((short)OpCode);

            // 由于某些原因，轮询包头不包括版本
            if (OpCode != ArtNetOpCodes.PollReply)
                data.WriteNetwork(Version);
        }

        /// <summary>
        /// 根据 OpCode 创建 ArtNet 数据包
        /// </summary>
        /// <param name="data">ArtNet 接收的数据</param>
        /// <returns></returns>
        public static ArtNetPacket Create(ArtNetRecieveData data)
        {
            switch ((ArtNetOpCodes)data.OpCode)
            {
                case ArtNetOpCodes.Poll:
                    return new ArtPollPacket(data);
                case ArtNetOpCodes.PollReply:
                    return new ArtPollReplyPacket(data);
                case ArtNetOpCodes.Dmx:
                    return new ArtNetDmxPacket(data);
                case ArtNetOpCodes.TodRequest:
                    return new ArtTodRequestPacket(data);
                case ArtNetOpCodes.TodData:
                    return new ArtTodDataPacket(data);
                case ArtNetOpCodes.TodControl:
                    return new ArtTodControlPacket(data);
            }

            return null;

        }
    }
}
