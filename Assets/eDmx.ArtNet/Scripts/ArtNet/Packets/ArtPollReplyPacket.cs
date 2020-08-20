using ArtNet.Enums;
using ArtNet.IO;
using ArtNet.Sockets;
using System;

namespace ArtNet.Packets
{
    /// <summary>
    /// OpPollReplay 响应状态标志枚举
    /// </summary>
    [Flags]
    public enum PollReplyStatus
    {
        None = 0,
        UBEA = 1,
        RdmCapable = 2,
        ROMBoot = 4
    }

    /// <summary>
    /// ArtNet OpPollReplay 数据包
    /// </summary>
    public class ArtPollReplyPacket : ArtNetPacket
    {
        public ArtPollReplyPacket()
            : base(ArtNetOpCodes.PollReply)
        {
        }

        public ArtPollReplyPacket(ArtNetRecieveData data)
            : base(data)
        {

        }

        #region 数据包属性

        private byte[] ipAddress = new byte[4];

        /// <summary>
        /// IP 地址字节数组
        /// </summary>
        public byte[] IpAddress
        {
            get { return ipAddress; }
            set
            {
                if (value.Length != 4)
                    throw new ArgumentException("The IP address must be an array of 4 bytes.");

                ipAddress = value;
            }
        }

        private short port = ArtNetSocket.Port;

        /// <summary>
        /// 端口
        /// </summary>
        public short Port
        {
            get { return port; }
            set { port = value; }
        }

        private short firmwareVersion = 0;

        /// <summary>
        /// 固件版本
        /// </summary>
        public short FirmwareVersion
        {
            get { return firmwareVersion; }
            set { firmwareVersion = value; }
        }


        private short subSwitch = 0;

        /// <summary>
        /// 此节点子网所有输入输出端口（NetSwitch and SubSwitch）
        /// </summary>
        public short SubSwitch
        {
            get { return subSwitch; }
            set { subSwitch = value; }
        }

        private short oem = 0xff;

        /// <summary>
        /// OEM 代码
        /// </summary>
        public short Oem
        {
            get { return oem; }
            set { oem = value; }
        }

        private byte ubeaVersion = 0;

        /// <summary>
        /// UBEA 版本号
        /// </summary>
        public byte UbeaVersion
        {
            get { return ubeaVersion; }
            set { ubeaVersion = value; }
        }

        private PollReplyStatus status = 0;

        /// <summary>
        /// 回复状态
        /// </summary>
        public PollReplyStatus Status
        {
            get { return status; }
            set { status = value; }
        }

        private short estaCode = 28794; // 707A

        /// <summary>
        /// ESTA 制造商代码
        /// </summary>
        public short EstaCode
        {
            get { return estaCode; }
            set { estaCode = value; }
        }

        private string shortName = string.Empty;

        /// <summary>
        /// 设备简称
        /// </summary>
        public string ShortName
        {
            get { return shortName; }
            set { shortName = value; }
        }

        private string longName = string.Empty;

        /// <summary>
        /// 完整名称
        /// </summary>
        public string LongName
        {
            get { return longName; }
            set { longName = value; }
        }

        private string nodeReport = string.Empty;

        /// <summary>
        /// 节点报告
        /// </summary>
        public string NodeReport
        {
            // #0001 [0000] ACME Art-Net Product. Good Boot.
            get { return nodeReport; }
            set { nodeReport = value; }
        }

        private short portCount = 0;

        /// <summary>
        /// 端口数量（定义设备最大输入或输出端口）
        /// </summary>
        public short PortCount
        {
            get { return portCount; }
            set { portCount = value; }
        }

        private byte[] portTypes = new byte[4];

        /// <summary>
        /// 端口类型
        /// </summary>
        public byte[] PortTypes
        {
            get { return portTypes; }
            set
            {
                if (value.Length != 4)
                    throw new ArgumentException("The port types must be an array of 4 bytes.");

                portTypes = value;
            }
        }

        private byte[] goodInput = new byte[4];

        /// <summary>
        /// 输入端口接收状态
        /// </summary>
        public byte[] GoodInput
        {
            get { return goodInput; }
            set
            {
                if (value.Length != 4)
                    throw new ArgumentException("The good input must be an array of 4 bytes.");

                goodInput = value;
            }
        }

        private byte[] goodOutput = new byte[4];

        /// <summary>
        /// 输出端口状态
        /// </summary>
        public byte[] GoodOutput
        {
            get { return goodOutput; }
            set
            {
                if (value.Length != 4)
                    throw new ArgumentException("The good output must be an array of 4 bytes.");

                goodOutput = value;
            }
        }

        private byte[] swIn = new byte[4];

        /// <summary>
        /// Switch 输入端口
        /// </summary>
        public byte[] SwIn
        {
            get { return swIn; }
            set { swIn = value; }
        }

        private byte[] swOut = new byte[4];

        /// <summary>
        /// Switch 输出端口
        /// </summary>
        public byte[] SwOut
        {
            get { return swOut; }
            set { swOut = value; }
        }

        private byte swVideo = 0;

        public byte SwVideo
        {
            get { return swVideo; }
            set { swVideo = value; }
        }

        private byte swMacro = 0;

        public byte SwMacro
        {
            get { return swMacro; }
            set { swMacro = value; }
        }

        private byte swRemote = 0;

        public byte SwRemote
        {
            get { return swRemote; }
            set { swRemote = value; }
        }

        private byte style = 0;

        /// <summary>
        /// 设备类别
        /// </summary>
        public byte Style
        {
            get { return style; }
            set { style = value; }
        }

        private byte[] macAddress = new byte[6];

        /// <summary>
        /// 发送此数据包的 MAC 地址
        /// </summary>
        public byte[] MacAddress
        {
            get { return macAddress; }
            set
            {
                if (value.Length != 6)
                    throw new ArgumentException("The mac address must be an array of 6 bytes.");

                macAddress = value;
            }
        }

        private byte[] bindIpAddress = new byte[4];

        /// <summary>
        /// 发送此数据包的 IP 地址
        /// </summary>
        public byte[] BindIpAddress
        {
            get { return bindIpAddress; }
            set
            {
                if (value.Length != 4)
                    throw new ArgumentException("The bind IP address must be an array of 4 bytes.");

                bindIpAddress = value;
            }
        }

        private byte bindIndex = 0;

        /// <summary>
        /// 绑定设备的顺序
        /// </summary>
        public byte BindIndex
        {
            get { return bindIndex; }
            set { bindIndex = value; }
        }

        private byte status2 = 0;

        /// <summary>
        /// 状态信息扩展
        /// </summary>
        public byte Status2
        {
            get { return status2; }
            set { status2 = value; }
        }


        #endregion

        #region Packet Helpers

        /// <summary>
        /// 解析 Universe 地址，确保与 ArtNet I II III 设备兼容
        /// </summary>
        /// <param name="outPorts">端口</param>
        /// <param name="portIndex">用于获取全局信息的端口索引</param>
        /// <returns>15bit Universe 地址</returns>
        public int UniverseAddress(bool outPorts, int portIndex)
        {
            int universe;

            if (SubSwitch > 0)
            {
                universe = (SubSwitch & 0x7F00);
                universe += (SubSwitch & 0x0F) << 4;
                universe += (outPorts ? SwOut[portIndex] : SwIn[portIndex]) & 0xF;
            }
            else
            {
                universe = (outPorts ? SwOut[portIndex] : SwIn[portIndex]);
            }

            return universe;
        }

        #endregion

        /// <summary>
        /// 读取数据
        /// </summary>
        /// <param name="data">ArtNet 二进制读取器</param>
        public override void ReadData(ArtNetBinaryReader data)
        {
            base.ReadData(data);

            IpAddress = data.ReadBytes(4);              // IpAddress
            Port = data.ReadInt16();                    // PortNumberLo PortNumberHi
            FirmwareVersion = data.ReadNetwork16();     // VersInfoHi VersInfoLo
            SubSwitch = data.ReadNetwork16();           // NetSwitch SubSwitch
            Oem = data.ReadNetwork16();                 // OemHi OemLo
            UbeaVersion = data.ReadByte();              // UbeaVersion
            Status = (PollReplyStatus)data.ReadByte();  // Status1
            EstaCode = data.ReadNetwork16();            // EstaManLo EstaManHi
            ShortName = data.ReadNetworkString(18);     // ShortName
            LongName = data.ReadNetworkString(64);      // LongName
            NodeReport = data.ReadNetworkString(64);    // NodeReport
            PortCount = data.ReadNetwork16();           // NumPortsHi NumPortsLo
            PortTypes = data.ReadBytes(4);              // PortTypes
            GoodInput = data.ReadBytes(4);              // GoodInput
            GoodOutput = data.ReadBytes(4);             // GoodOutput
            SwIn = data.ReadBytes(4);                   // SwIn
            SwOut = data.ReadBytes(4);                  // SwOut
            SwVideo = data.ReadByte();                  // SwVideo
            SwMacro = data.ReadByte();                  // SwMacro
            SwRemote = data.ReadByte();                 // SwRemote
            data.ReadBytes(3);                          // Spare1 Spare2 Spare3 (备用字段)
            Style = data.ReadByte();                    // Style
            MacAddress = data.ReadBytes(6);             // Mac
            BindIpAddress = data.ReadBytes(4);          // BindIp
            BindIndex = data.ReadByte();                // BindIndex
            Status2 = data.ReadByte();                  // Status2
        }

        public override void WriteData(ArtNetBinaryWriter data)
        {
            base.WriteData(data);

            data.Write(IpAddress);
            data.Write(Port);
            data.WriteNetwork(FirmwareVersion);
            data.WriteNetwork(SubSwitch);
            data.WriteNetwork(Oem);
            data.Write(UbeaVersion);
            data.Write((byte)Status);
            data.Write(EstaCode);
            data.WriteNetwork(ShortName, 18);
            data.WriteNetwork(LongName, 64);
            data.WriteNetwork(NodeReport, 64);
            data.WriteNetwork(PortCount);
            data.Write(PortTypes);
            data.Write(GoodInput);
            data.Write(GoodOutput);
            data.Write(SwIn);
            data.Write(SwOut);
            data.Write(SwVideo);
            data.Write(SwMacro);
            data.Write(SwRemote);
            data.Write(new byte[3]);
            data.Write(Style);
            data.Write(MacAddress);
            data.Write(BindIpAddress);
            data.Write(BindIndex);
            data.Write(Status2);
            data.Write(new byte[208]);
        }
    }
}
