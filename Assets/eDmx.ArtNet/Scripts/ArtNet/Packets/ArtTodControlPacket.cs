using ArtNet.Enums;
using ArtNet.IO;

namespace ArtNet.Packets
{
    /// <summary>
    /// ArtNet OpTodControl 数据包
    /// </summary>
    public class ArtTodControlPacket : ArtNetPacket
    {
        public ArtTodControlPacket()
            : base(ArtNetOpCodes.TodControl)
        {
        }

        public ArtTodControlPacket(ArtNetRecieveData data)
            : base(data)
        {

        }

        #region 数据包属性

        /// <summary>
        /// Net
        /// </summary>
        public byte Net { get; set; }

        /// <summary>
        /// 命令
        /// </summary>
        public ArtTodControlCommand Command { get; set; }

        /// <summary>
        /// 地址
        /// </summary>
        public byte Address { get; set; }


        #endregion

        /// <summary>
        /// 读取数据
        /// </summary>
        /// <param name="data">ArtNet 二进制读取器</param>
        public override void ReadData(ArtNetBinaryReader data)
        {
            base.ReadData(data);

            data.BaseStream.Seek(9, System.IO.SeekOrigin.Current);  // Filler1 Filler2 Sqare1 - 7
            Net = data.ReadByte();                                  // Net
            Command = (ArtTodControlCommand)data.ReadByte();        // Command
            Address = data.ReadByte();                              // Address
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="data">ArtNet 二进制写入器</param>
        public override void WriteData(ArtNetBinaryWriter data)
        {
            base.WriteData(data);

            data.Write(new byte[9]);
            data.Write(Net);
            data.Write((byte)Command);
            data.Write(Address);
        }


    }
}
