using System;
using System.Net;

namespace ArtNet.Sockets
{
    /// <summary>
    /// ArtNet 数据包事件参数类
    /// </summary>
    /// <typeparam name="TPacketType">数据包类型</typeparam>
    public class NewPacketEventArgs<TPacketType> : EventArgs
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="source">IP 终结点</param>
        /// <param name="packet">ArtNet 数据包</param>
        public NewPacketEventArgs(IPEndPoint source, TPacketType packet)
        {
            Source = source;
            Packet = packet;
        }

        private IPEndPoint source;

        /// <summary>
        /// 数据源地址
        /// </summary>
        public IPEndPoint Source
        {
            get { return source; }
            protected set { source = value; }
        }

        private TPacketType packet;

        /// <summary>
        /// ArtNet 数据包
        /// </summary>
        public TPacketType Packet
        {
            get { return packet; }
            private set { packet = value; }
        }

    }
}
