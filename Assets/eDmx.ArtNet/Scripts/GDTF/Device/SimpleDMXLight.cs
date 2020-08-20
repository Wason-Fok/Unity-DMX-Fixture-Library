using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Light))]
public class SimpleDMXLight : DMXDevice
{
    public new Light light;

    /// <summary>
    /// 通道数量
    /// </summary>
    public override int NumChannels { get; protected set; }

    /// <summary>
    /// 设置 DMX 数据
    /// </summary>
    /// <param name="dmxData">DMX 数据</param>
    public override void SetData(byte[] dmxData)
    {
        base.SetData(dmxData);

        var color = light.color;

        color.r = dmxData[0] / 256f;
        color.g = dmxData[1] / 256f;
        color.b = dmxData[2] / 256f;
        color += Color.white * 0.5f * dmxData[3] / 256f;

        light.color = color;
    }

    private void Start()
    {
        light = GetComponent<Light>();
    }
}
