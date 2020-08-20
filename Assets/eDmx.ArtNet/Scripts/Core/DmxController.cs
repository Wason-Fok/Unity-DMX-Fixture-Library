using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using UnityEngine;
using ArtNet.Sockets;
using ArtNet.Packets;
using System.Net.NetworkInformation;
using System.Runtime.Remoting.Messaging;

/// <summary>
/// DMX 控制器类
/// </summary>
public class DmxController : MonoBehaviour
{
    [Header("Send DMX")]

    /// <summary>
    /// 发送模式是否为广播模式
    /// </summary>
    public bool useBroadcast;
    /// <summary>
    /// 目的 IP
    /// </summary>
    public string remoteIP = "localhost";
    /// <summary>
    /// 终结点
    /// </summary>
    IPEndPoint remote;
    /// <summary>
    /// 是否为服务端（是否接收数据）
    /// </summary>
    public bool isServer;
    /// <summary>
    /// 是否接受到新的数据包
    /// </summary>
    public bool newPacket;

    [Header("DMX Devices")]
    public UniverseDevices[] universes;
    
    [Header("Send/Recieved DMX data for debug")]
    [SerializeField] ArtNetDmxPacket latestReceivedDMX;                         // 最后收到的数据包
    [SerializeField] ArtNetDmxPacket dmxToSend = new ArtNetDmxPacket();         // 发送的数据包
    byte[] _dmxData;
    private ArtPollReplyPacket pollReplayPacket = new ArtPollReplyPacket()
    {
        IpAddress = GetLocalIP().GetAddressBytes(),
        ShortName = "UnityArtNet",
        LongName = "UnityArtNet-WASON",
        NodeReport = "#0000 [0000] UnityArtNet Art-Net Product. Good Boot.",
        MacAddress = GetLocalMAC()
    };

    /// <summary>
    /// ArtNet Socket 对象
    /// </summary>
    ArtNetSocket artnet;
    /// <summary>
    /// DMX 数据映射 SubNet -> Universe -> Data[]
    /// </summary>
    Dictionary<int, Dictionary<int, byte[]>> dmxDataMap;

    /// <summary>
    /// 当面板值被修改时初始化 DMX 设备
    /// </summary>
    private void OnValidate()
    {
        foreach (var u in universes)
        {
            u.Initialize();
        }
            
    }

    #region Unity 生命周期
    void Start()
    {
        // 初始化 ArtNet socket
        artnet = new ArtNetSocket();
        dmxDataMap = new Dictionary<int, Dictionary<int, byte[]>>();
        dmxToSend.DmxData = new byte[512];

        // 为事件绑定函数
        artnet.NewPacket += OnReceiveDmxPacket;
        artnet.NewPacket += OnReceivePollPacket;

        // 如果该 Controller 为 Server 端，那么看是监听接受数据
        if (isServer)
        {
            // 如果设定了子网掩码，就可以指定给哪个网段发送数据
            //artnet.Open(GetLocalIP(), new IPAddress(new byte[] { 255, 0, 0, 0 }));
            artnet.Open(GetLocalIP(), null);
        }

        // 如果不使用广播模式发送 或者 该 Controller 不接受数据
        if (!useBroadcast || !isServer)
        {
            remote = new IPEndPoint(FindFromHostName(remoteIP), ArtNetSocket.Port);
        }

        artnet.Send(pollReplayPacket);
    }

    private void Update()
    {
        lock (dmxDataMap)
        {
            // 获取 dmxDataMap 中的 SubNet 数组
            int[] subNetKeys = dmxDataMap.Keys.ToArray();

            for (var i = 0; i < subNetKeys.Length; i++)
            {
                int subNet = subNetKeys[i];
                // 获取该 SubNet 中的 Universe 数组
                int[] universeKeys = dmxDataMap[subNet].Keys.ToArray();

                for (int j = 0; j < universeKeys.Length; j++)
                {
                    // 获取当前 Universe 下的 DmxData
                    int universe = universeKeys[j];
                    byte[] dmxData = dmxDataMap[subNet][universe];
                    if (dmxData == null)
                    {
                        continue;
                    }

                    // 返回所有查找到的 UniverDevice，如果没有找到则返回 null
                    var universeDevices = universes.Where(u => (u.subNet == subNet && u.universe == universe));

                    // 如果找到了满足条件的 UniverseDevices
                    if (universeDevices != null)
                    {
                        foreach (var item in universeDevices)
                        {
                            foreach (var d in item.devices)
                            {
                                // 设置该设备的 DmxData
                                d.SetData(dmxData.Skip(d.startChannel).Take(d.NumChannels).ToArray());
                            }
                        }
                    }
                }
            }
        }
    }

    private void OnDestroy()
    {
        artnet.Close();
    }
    #endregion

    #region 事件函数
    /// <summary>
    /// ArtNet Socket 接收事件绑定函数
    /// </summary>
    /// <param name="sender">Socket 对象</param>
    /// <param name="e">ArtNet 事件参数对象</param>
    private void OnReceiveDmxPacket(object sender, NewPacketEventArgs<ArtNetPacket> e)
    {
        // 标记接收到了新的数据包
        newPacket = true;

        if (e.Packet.OpCode == ArtNet.Enums.ArtNetOpCodes.Dmx)
        {
            var packet = latestReceivedDMX = e.Packet as ArtNetDmxPacket;

            // 如果新数据包不等于现有的，那么再更新
            //if (packet.DmxData != _dmxData)
            //{
            //    _dmxData = packet.DmxData;
            //}

            var subNet = packet.Universe / 16;
            var universe = packet.Universe % 16;
            lock (dmxDataMap)
            {
                // 如果数据包中包含 该 SubNet 和 Universe 直接复制，没有则创建新的键值对
                if (dmxDataMap.ContainsKey(subNet) && dmxDataMap[subNet].ContainsKey(universe))
                {
                    dmxDataMap[subNet][universe] = packet.DmxData;
                }
                else if (dmxDataMap.ContainsKey(subNet))
                {
                    dmxDataMap[subNet] = new Dictionary<int, byte[]>() { { universe, packet.DmxData } };
                }
                else
                {
                    dmxDataMap.Add(subNet, new Dictionary<int, byte[]>() { { universe, packet.DmxData } });
                }

                Debug.Log("Received --- DMX Command: " + e.Source.ToString() + $" [SubNet: {subNet}, Universe: {universe}]");
            }
        }
    }

    private void OnReceivePollPacket(object sender, NewPacketEventArgs<ArtNetPacket> e)
    {
        newPacket = true;

        if (e.Packet.OpCode == ArtNet.Enums.ArtNetOpCodes.Poll)
        {
            artnet.Send(pollReplayPacket);
            Debug.Log("Received --- Poll Command: " + e.Source.ToString());
        }
    }
    #endregion

    #region Controller 发送部分
    [ContextMenu("Send DMX")]
    public void Send()
    {
        if (useBroadcast && isServer)
            artnet.Send(dmxToSend);
        else
            artnet.Send(dmxToSend, remote);
    }

    /// <summary>
    /// 发送数据
    /// </summary>
    /// <param name="net">SubNet</param>
    /// <param name="universe">Universe</param>
    /// <param name="dmxData">DMX 数据</param>
    public void Send(byte net, byte universe, byte[] dmxData)
    {
        dmxToSend.Universe = (byte)((net << 4) | universe);
        // 将传入的 DMX 数据拷贝到 ArtNet 数据包中
        System.Buffer.BlockCopy(dmxData, 0, dmxToSend.DmxData, 0, dmxData.Length);

        if (useBroadcast && isServer)
            artnet.Send(dmxToSend);
        else
            artnet.Send(dmxToSend, remote);
    }
    #endregion

    #region 功能函数
    /// <summary>
    /// 获取本地 MAC 地址
    /// </summary>
    /// <returns>MAC 地址字节数组</returns>
    static byte[] GetLocalMAC()
    {
        var interfaces = NetworkInterface.GetAllNetworkInterfaces();
        NetworkInterface net = interfaces.Where(i => i.OperationalStatus == OperationalStatus.Up).FirstOrDefault();

        if (net == null)
        {
            Debug.Log("Can not get MAC address.");
            return null;
        }

        return net.GetPhysicalAddress().GetAddressBytes();
    }

    /// <summary>
    /// 获取本地 IP 地址
    /// </summary>
    /// <returns>IP Address</returns>
    static IPAddress GetLocalIP()
    {
        var address = IPAddress.None;

        // 获取本地主机名
        string hostName = Dns.GetHostName();

        try
        {
            // 将主机名解析为 IPHostEntry 实例
            IPHostEntry localHost = Dns.GetHostEntry(hostName);
            foreach (var item in localHost.AddressList)
            {
                if (item.AddressFamily == AddressFamily.InterNetwork)
                {
                    address = item;
                    break;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogErrorFormat("Failed to find IP for :\n host name = {0}\n exception={1}", hostName, e);
        }

        return address;

    }

    /// <summary>
    /// 获取指定主机名中的 IP 地址
    /// </summary>
    /// <param name="hostname">HostName</param>
    /// <returns>IP 地址</returns>
    static IPAddress FindFromHostName(string hostname)
    {
        var address = IPAddress.None;
        try
        {
            // 如果该字符串为 IP 地址则返回
            if (IPAddress.TryParse(hostname, out address))
                return address;

            // 解析主机名或者 IP 地址
            var addresses = Dns.GetHostAddresses(hostname);
            for (var i = 0; i < addresses.Length; i++)
            {
                // 如果 IP 地址为 IPv4 则返回
                if (addresses[i].AddressFamily == AddressFamily.InterNetwork)
                {
                    address = addresses[i];
                    break;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogErrorFormat(
                "Failed to find IP for :\n host name = {0}\n exception={1}",
                hostname, e);
        }
        return address;
    }
    #endregion
}
