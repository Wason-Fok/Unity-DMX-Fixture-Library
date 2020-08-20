namespace ArtNet.IO
{
    /// <summary>
    /// ArtNet 数据接收类
    /// </summary>
    public class ArtNetRecieveData
    {
        /// <summary>
        /// 接收到的数据
        /// </summary>
        public byte[] buffer = new byte[1500];
        /// <summary>
        /// 数据字节大小
        /// </summary>
        public int bufferSize = 1500;
        /// <summary>
        /// 数据长度
        /// </summary>
        public int DataLength = 0;

        /// <summary>
        /// 接收到的数据是否有效
        /// </summary>
        public bool Valid
        {
            get { return DataLength > 12; }
        }

        /// <summary>
        /// ArtNet 操作码
        /// </summary>
        public int OpCode
        {
            get
            {
                // ArtNet 数据包的第 9 个字节为操作码高位（第 8 个字节为操作码低位）
                return buffer[9] + (buffer[8] << 8);
            }
        }
    }
}
