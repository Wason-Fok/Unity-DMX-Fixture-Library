using ArtNet.Enums;
using ArtNet.IO;
using ArtNet.Rdm;
using System.Collections.Generic;

namespace ArtNet.Packets
{
    /// <summary>
    /// ArtNet OpTodData 数据包
    /// </summary>
    public class ArtTodDataPacket : ArtNetPacket
    {
        public ArtTodDataPacket()
            : base(ArtNetOpCodes.TodData)
        {
            RdmVersion = 1;
            Devices = new List<UId>();
        }

        public ArtTodDataPacket(ArtNetRecieveData data)
            : base(data)
        {

        }

        #region 数据包属性

        /// <summary>
        /// RDM 版本
        /// </summary>
        public byte RdmVersion { get; set; }

        /// <summary>
        /// 物理端口 Range 1 - 4
        /// </summary>
        public byte Port { get; set; }

        /// <summary>
        /// Net
        /// </summary>
        public byte Net { get; set; }

        /// <summary>
        /// 命令
        /// </summary>
        public byte Command { get; set; }

        public byte Universe { get; set; }

        /// <summary>
        /// UID 数量
        /// </summary>
        public short UIdTotal { get; set; }

        /// <summary>
        /// 数据包块
        /// 当 UID Total 超过 200 时，使用多个 ArtTodData 数据包
        /// 第一个数据包的块计数设置 0，依次递增
        /// </summary>
        public byte BlockCount { get; set; }

        /// <summary>
        /// UID 设备
        /// </summary>
        public List<UId> Devices { get; set; }


        #endregion
        
        /// <summary>
        /// 读取 RDM 数据
        /// </summary>
        /// <param name="data">ArtNet 二进制读取器</param>
        public override void ReadData(ArtNetBinaryReader data)
        {
            var reader = new ArtNetBinaryReader(data.BaseStream);

            base.ReadData(data);

            RdmVersion = data.ReadByte();                           // RdmVer
            Port = data.ReadByte();                                 // Port
            data.BaseStream.Seek(7, System.IO.SeekOrigin.Current);  // BindIndex 未实现
            Net = data.ReadByte();                                  // Net
            Command = data.ReadByte();                              // Command
            Universe = data.ReadByte();                             // 高 8 位 Sub-Net & 低 8 位 Universe
            UIdTotal = reader.ReadNetwork16();                      // UidTotalHi UidTotalLo
            BlockCount = data.ReadByte();                           // BlockCount

            Devices = new List<UId>();
            int count = data.ReadByte();                            // UidCount
            for (int n = 0; n < count; n++)
                Devices.Add(reader.ReadUId());                      // ToD[]
        }

        /// <summary>
        /// 写入 RDM 数据
        /// </summary>
        /// <param name="data">ArtNet 二进制写入器</param>
        public override void WriteData(ArtNetBinaryWriter data)
        {
            base.WriteData(data);

            var writer = new ArtNetBinaryWriter(data.BaseStream);

            data.Write(RdmVersion);
            data.Write(Port);
            data.Write(new byte[7]);
            data.Write(Net);
            data.Write(Command);
            data.Write(Universe);
            writer.WriteNetwork(UIdTotal);
            data.Write(BlockCount);
            data.Write((byte)Devices.Count);

            foreach (UId id in Devices)
                writer.Write(id);
        }


    }
}
