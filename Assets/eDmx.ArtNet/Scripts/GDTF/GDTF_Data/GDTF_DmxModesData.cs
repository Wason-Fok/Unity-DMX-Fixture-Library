using System.Collections.Generic;
using UnityEngine.Networking.NetworkSystem;

/// <summary>
/// DMX 数值分辨率
/// </summary>
public enum DmxBit
{
    /// <summary>
    /// 0 ~ 255
    /// </summary>
    Bit8 = 1,
    /// <summary>
    /// 0 ~ 65535
    /// </summary>
    Bit16 = 2,
    /// <summary>
    /// 0 ~ 16777215
    /// </summary>
    Bit24 = 3,
    /// <summary>
    /// 0 ~ 4294967295
    /// </summary>
    Bit32 = 4
};

/// <summary>
/// GDTF DMX 模式相关信息类
/// </summary>
public class GDTF_DmxModesData
{
    /// <summary>
    /// DMX 模式名称
    /// </summary>
    public string dmxModeName = string.Empty;
    /// <summary>
    /// DMX 模式几何名称
    /// </summary>
    public string dmxModeGeometry = string.Empty;

    /// <summary>
    /// 该模式下所有通道定义信息
    /// </summary>
    public List<GDTF_DmxChannel> channelsData = new List<GDTF_DmxChannel>();
}

/// <summary>
/// GDTF DMX 通道信息
/// </summary>
public class GDTF_DmxChannel
{
    /// <summary>
    /// 通道 DMX 数值分辨率
    /// </summary>
    public DmxBit dmxBit = DmxBit.Bit8;
    /// <summary>
    /// DMX 地址号
    /// </summary>
    public int dmxBreak = 1;
    /// <summary>
    /// 该通道所占用实际通道的编号
    /// </summary>
    public int[] offset;
    /// <summary>
    /// 该通道信息的默认 DMX 值
    /// </summary>
    public int deafault = 0;
    /// <summary>
    /// 该通道信息名称
    /// </summary>
    public string geometry = string.Empty;
    /// <summary>
    /// 该通道的相关的基础信息
    /// </summary>
    public GDTF_DmxLogicalChannel logicalChannel;
}

/// <summary>
/// GDTF DMXLogicalChannel 信息
/// </summary>
public class GDTF_DmxLogicalChannel
{
    /// <summary>
    /// 该通道所定义的名称
    /// </summary>
    public GDTF_AttributeDefinitionsData_Attribute attribute = null;
    /// <summary>
    /// 逻辑通道绑定的所有通道函数
    /// </summary>
    public List<GDTF_DmxChannelFunction> channelFunctions = new List<GDTF_DmxChannelFunction>();
}

/// <summary>
/// GDTF DMX 通道函数
/// </summary>
public class GDTF_DmxChannelFunction
{
    /// <summary>
    /// 函数名称
    /// </summary>
    public string functionName = string.Empty;
    /// <summary>
    /// 通道函数所绑定的属性
    /// </summary>
    public GDTF_AttributeDefinitionsData_Attribute attribute = null;
    /// <summary>
    /// 制造商原始函数名 默认空
    /// </summary>
    public string originalAttribute = string.Empty;
    /// <summary>
    /// 函数 DMX 起始值
    /// </summary>
    public int functionDmxFrom = 0;
    /// <summary>
    /// 函数 DMX 结束值
    /// </summary>
    public int functionDmxTo = 0;
    /// <summary>
    /// 物理起始值
    /// </summary>
    public float functionPhysicalFrom = 0;
    /// <summary>
    /// 物理结束值
    /// </summary>
    public float functionPhysicalTo = 0;
    /// <summary>
    /// 该函数所关联的 Wheel 组名 默认为 null
    /// </summary>
    public string wheelName = string.Empty;

    /// <summary>
    /// 该函数下所有预设值
    /// </summary>
    public List<GDTF_DmxChannelSet> channelSets = new List<GDTF_DmxChannelSet>();
}

/// <summary>
/// GDTF DMX 函数预设值
/// </summary>
public class GDTF_DmxChannelSet
{
    /// <summary>
    /// 预设值名称
    /// </summary>
    public string setName = string.Empty;
    /// <summary>
    /// 预设值 DMX 起始值
    /// </summary>
    public int setDmxForm = 0;
    /// <summary>
    /// 预设值 DMX 结束值
    /// </summary>
    public int setDmxTo = 0;
    /// <summary>
    /// 预设值物理起始值
    /// </summary>
    public int setPhysicalFrom = 0;
    /// <summary>
    /// 预设物理结束值
    /// </summary>
    public int setPhysicalTo = 0;
    /// <summary>
    /// Wheel Slot 索引
    /// </summary>
    public int wheelSlotIndex = 0;
}
