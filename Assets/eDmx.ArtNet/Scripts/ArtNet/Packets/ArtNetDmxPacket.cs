
using ArtNet.Enums;
using ArtNet.IO;

namespace ArtNet.Packets
{
    /// <summary>
    /// ArtNet DMX 数据包
    /// </summary>
    [System.Serializable]
    public class ArtNetDmxPacket : ArtNetPacket
    {
        public ArtNetDmxPacket()
            : base(ArtNetOpCodes.Dmx)
        {
        }

        public ArtNetDmxPacket(ArtNetRecieveData data)
            : base(data)
        {

        }
         
        #region 数据包属性
        
        private byte sequence = 0;

        /// <summary>
        /// 序列
        /// 显示数据包生成顺序
        /// </summary>
        public byte Sequence
        {
            get { return sequence; }
            set { sequence = value; }
        }
        
        private byte physical = 0;

        /// <summary>
        /// 物理
        /// 定义生成数据包的物理端口
        /// </summary>
        public byte Physical
        {
            get { return physical; }
            set { physical = value; }
        }

        [UnityEngine.SerializeField]
        private byte universe = 0;

        /// <summary>
        /// SubNet & Universe
        /// </summary>
        public byte Universe
        {
            get { return universe; }
            set { universe = value; }
        }

        [UnityEngine.SerializeField]
        private byte net = 0;

        public byte Net
        {
            get { return net; }
            set { net = value; }
        }

        /// <summary>
        /// 数据长度
        /// </summary>
        public short Length
        {
            get
            {
                if (dmxData == null)
                    return 0;
                return (short)dmxData.Length;
            }
        }

        [UnityEngine.SerializeField]
        private byte[] dmxData = null;

        /// <summary>
        /// DMX 数据
        /// </summary>
        public byte[] DmxData
        {
            get { return dmxData; }
            set { dmxData = value; }
        }

        #endregion

        /// <summary>
        /// 读取数据
        /// </summary>
        /// <param name="data">ArtNet 二进制读取器</param>
        public override void ReadData(ArtNetBinaryReader data)
        {
            base.ReadData(data);

            Sequence = data.ReadByte();         // Sequence
            Physical = data.ReadByte();         // Physical
            Universe = data.ReadByte();         // SubUni
            Net = data.ReadByte();              // Net
            int length = data.ReadNetwork16();  // LengthHi LengthLo
            DmxData = data.ReadBytes(length);   // Data[]
        }

        public override void WriteData(ArtNetBinaryWriter data)
        {
            base.WriteData(data);

            data.Write(Sequence);
            data.Write(Physical);
            data.Write(Universe);
            data.Write(Net);
            data.WriteNetwork(Length);
            data.Write(DmxData);
        }

    }
}
