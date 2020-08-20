using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 色轮 slot 相关属性
/// </summary>
public struct WheelSlot
{
    /// <summary>
    /// Slot 名称
    /// </summary>
    public string name;
    /// <summary>
    /// 颜色
    /// </summary>
    public Color color;
    /// <summary>
    /// 投射图案文件名
    /// </summary>
    public string mediaFileName;
}

/// <summary>
/// GDTF 色轮数据类
/// </summary>
public class GDTF_WheelsData
{
    /// <summary>
    /// 所有 Wheel 信息
    /// </summary>
    public string wheelName = string.Empty;

    public List<WheelSlot> slots = new List<WheelSlot>();

    /// <summary>
    /// 添加 Slot
    /// </summary>
    /// <param name="slot">WheelSlot 结构体</param>
    public void AddSlot(WheelSlot slot)
    {
        if (slots == null)
        {
            slots = new List<WheelSlot>();
        }

        slots.Add(slot);
    }
}
