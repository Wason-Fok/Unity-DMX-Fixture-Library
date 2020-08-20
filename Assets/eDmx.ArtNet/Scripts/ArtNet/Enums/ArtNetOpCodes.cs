namespace ArtNet.Enums
{
    /// <summary>
    /// ArtNet 操作码
    /// 用于定义 ArtNet 数据包类型
    /// </summary>
    public enum ArtNetOpCodes
    {
        None = 0,
        /// <summary>
        /// 由控制器传输，以便发现链接到网络的所有 ArtNet 设备
        /// 用于发现 ArtNet 节点的存在
        /// </summary>
        Poll = 0x20,
        /// <summary>
        /// 对 Poll 的响应，既确认发现过程，也包含关键状态信息
        /// 节点响应于收到 OpPoll 而发送的 ArtNet 数据包
        /// </summary>
        PollReply = 0x21,
        /// <summary>
        /// 定义 ArtDmx 数据包
        /// </summary>
        Dmx = 0x50,
        /// <summary>
        /// 指示所有 ArtNet 设备向网络传输它们的TOD（设备表）
        /// </summary>
        TodRequest = 0x80,
        /// <summary>
        /// 向网络传输 TOD（设备表）数据包
        /// </summary>
        TodData = 0x81,
        /// <summary>
        /// 向网络发送 RDM 发现控制信息
        /// </summary>
        TodControl = 0x82,
        /// <summary>
        /// 向网络发送所有非发现 RDM 包
        /// </summary>
        Rdm = 0x83,
        /// <summary>
        /// 向网络发送压缩的 RDM 子设备数据
        /// </summary>
        RdmSub = 0x84,
    }

    /// <summary>
    /// ArtNet 格式
    /// </summary>
    public enum ArtNetStyles
    {
        StNode = 0x00,
        StServer = 0x01,
        StMedia = 0x02,
        StRoute = 0x03,
        StBackup = 0x04,
        StConfig = 0x05
    }

}
