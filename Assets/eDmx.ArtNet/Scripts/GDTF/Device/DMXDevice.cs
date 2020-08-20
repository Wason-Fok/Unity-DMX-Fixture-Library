using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// DMX 设备抽象类
/// </summary>
public abstract class DMXDevice : MonoBehaviour
{
    /// <summary>
    /// DMX 数据
    /// </summary>
    public byte[] dmxData;
    /// <summary>
    /// 起始通道
    /// </summary>
    public int startChannel;
    /// <summary>
    /// 通道数量
    /// </summary>
    public abstract int NumChannels { get; protected set; }

    /// <summary>
    /// 设置 DMX 数据
    /// </summary>
    /// <param name="dmxData"></param>
    public virtual void SetData(byte[] dmxData)
    {
        this.dmxData = dmxData;
    }
}
