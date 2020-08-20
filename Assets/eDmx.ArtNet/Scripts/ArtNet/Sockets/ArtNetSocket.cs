using ArtNet.IO;
using ArtNet.Packets;
using System;
using System.Net;
using System.Net.Sockets;

namespace ArtNet.Sockets
{
    public class ArtNetSocket : Socket
    {
        /// <summary>
        /// ArtNet 通讯端口
        /// </summary>
        public const int Port = 6454;

        public event UnhandledExceptionEventHandler UnhandledException;
        public event EventHandler<NewPacketEventArgs<ArtNetPacket>> NewPacket;

        #region 构造函数
        /// <summary>
        /// 构造函数
        /// </summary>
        public ArtNetSocket()
            : base(System.Net.Sockets.AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Dgram, System.Net.Sockets.ProtocolType.Udp)
        {            
        }
        #endregion

        #region 信息属性

        private bool portOpen = false;
        /// <summary>
        /// 端口开启状态（用于控制是否接收）
        /// </summary>
        public bool PortOpen
        {
            get { return portOpen; }
            set { portOpen = value; }
        }

        /// <summary>
        /// 本地 IP 地址
        /// </summary>
        public IPAddress LocalIP { get; protected set; }

        /// <summary>
        /// 本地子网掩码
        /// </summary>
        public IPAddress LocalSubnetMask { get; protected set; }

        /// <summary>
        /// 获取广播地址
        /// </summary>
        /// <param name="address">IP 地址</param>
        /// <param name="subnetMask">子网掩码</param>
        /// <returns>广播地址</returns>
        private static IPAddress GetBroadcastAddress(IPAddress address, IPAddress subnetMask)
        {
            byte[] ipAdressBytes = address.GetAddressBytes();
            byte[] subnetMaskBytes = subnetMask.GetAddressBytes();

            if (ipAdressBytes.Length != subnetMaskBytes.Length)
                throw new ArgumentException("Lengths of IP address and subnet mask do not match.");

            byte[] broadcastAddress = new byte[ipAdressBytes.Length];
            for (int i = 0; i < broadcastAddress.Length; i++)
            {
                broadcastAddress[i] = (byte)(ipAdressBytes[i] | (subnetMaskBytes[i] ^ 255));
            }
            return new IPAddress(broadcastAddress);
        }

        /// <summary>
        /// 广播地址
        /// </summary>
        public IPAddress BroadcastAddress
        {
            get
            {
                if (LocalSubnetMask == null || LocalIP == null)
                    return IPAddress.Broadcast;
                return GetBroadcastAddress(LocalIP, LocalSubnetMask);
            }
        }

        private DateTime? lastPacket = null;

        /// <summary>
        /// 最后接收数据时间
        /// </summary>
        public DateTime? LastPacket
        {
            get { return lastPacket; }
            protected set { lastPacket = value; }
        }

        private ArtPollPacket pollPacket;

        #endregion

        #region ArtNetSocket 接收部分
        /// <summary>
        /// 开启 Socket 并开始接收
        /// </summary>
        /// <param name="localIp">本地 IP</param>
        /// <param name="localSubnetMask">本地子网掩码</param>
        public void Open(IPAddress localIp, IPAddress localSubnetMask)
        {
            pollPacket = new ArtPollPacket();

            LocalIP = localIp;
            LocalSubnetMask = localSubnetMask;

            // 设置 socket 类型 第一个参数为适用于所有套接字 第二个参数为允许 Socket 绑定到已在使用中的地址 实现端口复用（一个地址绑定多个 Socket）
            SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            // 绑定 IP 和端口
            Bind(new IPEndPoint(LocalIP, Port));
            // 第二个参数为允许在套接字上发送广播消息
            SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);

            // 打开端口
            PortOpen = true;

            // 广播 ArtPoll 数据包
            Send(pollPacket);

            StartRecieve();
        }

        /// <summary>
        /// 开始接收
        /// </summary>
        public void StartRecieve()
        {
            try
            {
                EndPoint localPort = new IPEndPoint(IPAddress.Any, Port);
                ArtNetRecieveData recieveState = new ArtNetRecieveData();
                // 开始从指定 IP 异步接收数据
                // 第一个参数：存储接收数据字节数组，第二个参数为存储位置偏移量，第三个参数为接受字节数
                // 第五个参数为：发送端的 IP 地址
                // 第六个参数为：当接受到数据是的回调函数
                BeginReceiveFrom(recieveState.buffer, 0, recieveState.bufferSize, SocketFlags.None, ref localPort, new AsyncCallback(OnRecieve), recieveState);
            }
            catch (Exception ex)
            {
                OnUnhandledException(new ApplicationException("An error ocurred while trying to start recieving ArtNet.", ex));
            }
        }

        /// <summary>
        /// 接收数据回调函数
        /// </summary>
        /// <param name="state">状态信息以及自定义数据</param>
        private void OnRecieve(IAsyncResult state)
        {
            EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

            if (PortOpen)
            {
                try
                {
                    // 获取自定义数据
                    ArtNetRecieveData recieveState = (ArtNetRecieveData)(state.AsyncState);

                    if (recieveState != null)
                    {
                        // 结束挂起的、从指定 IP 地址 进行异步读取
                        recieveState.DataLength = EndReceiveFrom(state, ref remoteEndPoint);

                        // 防止UDP环回，不接收自己发出的数据包.
                        if (remoteEndPoint != LocalEndPoint && recieveState.Valid)
                        {
                            LastPacket = DateTime.Now;

                            // 根据接收数据的 OpCode 来创建对应的 ArtNet 数据包对象并开始解析
                            ProcessPacket((IPEndPoint)remoteEndPoint, ArtNetPacket.Create(recieveState));
                        }
                    }
                }
                catch (Exception ex)
                {
                    OnUnhandledException(ex);
                }
                finally
                {
                    // 继续接收下一个数据包
                    StartRecieve();
                }
            }
        }

        /// <summary>
        /// 解析 ArtNet 数据包
        /// </summary>
        /// <param name="source">源地址</param>
        /// <param name="packet">ArtNet 数据包</param>
        private void ProcessPacket(IPEndPoint source, ArtNetPacket packet)
        {
            if (packet != null)
            {
                if (NewPacket != null)
                    // 触发事件
                    NewPacket(this, new NewPacketEventArgs<ArtNetPacket>(source, packet));
            }
        }

        /// <summary>
        /// 异常抛出
        /// </summary>
        /// <param name="ex">异常</param>
        protected void OnUnhandledException(Exception ex)
        {
            if (UnhandledException != null) UnhandledException(this, new UnhandledExceptionEventArgs((object)ex, false));
        }
        #endregion

        #region ArtNet 发送部分

        /// <summary>
        /// 广播方式发送 ArtNet 数据包
        /// </summary>
        /// <param name="packet">ArtNet 数据包</param>
        public void Send(ArtNetPacket packet)
        {
            Send(packet, new IPEndPoint(BroadcastAddress, Port));
        }

        /// <summary>
        /// 点播方式发送 ArtNet 数据包
        /// </summary>
        /// <param name="packet">ArtNet 数据包</param>
        /// <param name="remote">目标 终结点</param>
        public void Send(ArtNetPacket packet, IPEndPoint remote)
        {
            SendTo(packet.ToArray(), remote);
        }
      
        #endregion

        /// <summary>
        /// 销毁 Socket 套接字
        /// </summary>
        /// <param name="disposing"></param>
        protected override void Dispose(bool disposing)
        {
            PortOpen = false;

            base.Dispose(disposing);
        }
    }
}
