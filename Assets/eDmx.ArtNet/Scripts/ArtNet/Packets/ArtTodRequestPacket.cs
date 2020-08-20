using ArtNet.Enums;
using ArtNet.IO;
using System.Collections.Generic;

namespace ArtNet.Packets
{
    /// <summary>
    /// ArtNet OpTodRequest 数据包
    /// </summary>
    public class ArtTodRequestPacket : ArtNetPacket
    {
        public ArtTodRequestPacket()
            : base(ArtNetOpCodes.TodRequest)
        {
            RequestedUniverses = new List<byte>();
        }

        public ArtTodRequestPacket(ArtNetRecieveData data)
            : base(data)
        {

        }

        #region 数据包属性

        public byte Net { get; set; }

        /// <summary>
        /// 命令
        /// 定义如何处理 RDM 数据
        /// </summary>
        public byte Command { get; set; }

        public List<byte> RequestedUniverses { get; set; }


        #endregion

        /// <summary>
        /// 读取 RDM 数据
        /// </summary>
        /// <param name="data">ArtNet 二进制读取器</param>
        public override void ReadData(ArtNetBinaryReader data)
        {
            base.ReadData(data);

            data.BaseStream.Seek(9, System.IO.SeekOrigin.Current);      // 忽略 Filler1 Filler2 Spare1 - 7
            Net = data.ReadByte();                                      // Net
            Command = data.ReadByte();                                  // Command
            int count = data.ReadByte();                                // AddCount
            RequestedUniverses = new List<byte>(data.ReadBytes(count)); // Address[32]
        }

        /// <summary>
        /// 写入 RDM 数据
        /// </summary>
        /// <param name="data">ArtNet 二进制写入器</param>
        public override void WriteData(ArtNetBinaryWriter data)
        {
            base.WriteData(data);
            data.Write(new byte[9] { 0,0,0,0,0,0,0,0,0 });
            data.Write(Net);
            data.Write(Command);
            data.Write((byte)RequestedUniverses.Count);
            data.Write(RequestedUniverses.ToArray());
        }


    }
}
