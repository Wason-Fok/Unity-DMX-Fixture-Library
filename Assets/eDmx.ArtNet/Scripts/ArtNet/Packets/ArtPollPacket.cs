using ArtNet.Enums;
using ArtNet.IO;

namespace ArtNet.Packets
{
    /// <summary>
    /// ArtNet OpPoll 数据包
    /// </summary>
    public class ArtPollPacket : ArtNetPacket
    {
        public ArtPollPacket()
            : base(ArtNetOpCodes.Poll)
        {
        }

        public ArtPollPacket(ArtNetRecieveData data)
            : base(data)
        {

        }

        #region 数据包属性

        private byte talkToMe = 0;

        /// <summary>
        /// 接收到的字节
        /// </summary>
        public byte TalkToMe
        {
            get { return talkToMe; }
            set { talkToMe = value; }
        }

        #endregion

        /// <summary>
        /// 读取 OpPoll 数据
        /// </summary>
        /// <param name="data">ArtNet 二进制读取器</param>
        public override void ReadData(ArtNetBinaryReader data)
        {
            base.ReadData(data);

            TalkToMe = data.ReadByte();
        }

        /// <summary>
        /// 写入 OpPoll 数据
        /// </summary>
        /// <param name="data">ArtNet 二进制写入器</param>
        public override void WriteData(ArtNetBinaryWriter data)
        {
            base.WriteData(data);

            data.Write(TalkToMe);
            data.Write((byte)0);
        }

    }
}
