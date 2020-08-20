using System.Collections.Generic;
using UnityEngine;
using System.Text.RegularExpressions;

[RequireComponent(typeof(GDTF_FixtureSelector))]
public class MovingLight : DMXDevice
{
    public GDTF_FixtureSelector fixtureSelector;

    /// <summary>
    /// 定义该灯具所需通道数量
    /// </summary>
    public override int NumChannels { get { return GetFixtureModeChannelCount(); } protected set { } }

    public Transform panRotater;
    public Transform tiltRotater;
    public LightHead lightHead;

    [Header("PanTilt")]
    public int[] panAddr;
    /// <summary>
    /// Pan 最小角度
    /// <summary>
    public float panFrom = 270.0f;
    /// <summary>
    /// Pan 最大角度
    /// </summary>
    public float panTo = -270.0f;
    public int[] tiltAddr;
    /// <summary>
    /// Tilt 最小角度
    /// </summary>
    public float tiltFrom = 125.0f;
    /// <summary>
    /// Tilt 最大角度
    /// </summary>
    public float tiltTo = -125.0f;

    [Header("Dimmer")]
    public int[] dimmerAddr;
    
    [Header("Color")]
    public int[] colorAddr;
    public GDTF_DmxChannelFunction colornFunction;
    public List<WheelSlot> colorSlots = null;

    [Header("Gobo")]
    public int[] goboAddr;
    
    public GDTF_DmxChannelFunction gobonFunction;
    public List<WheelSlot> goboSlots = null;

    [Header("TargetState")]
    /// <summary>
    /// Pan 目标角度
    /// </summary>
    public float panTarget = 0;
    /// <summary>
    /// Tilt 目标角度
    /// </summary>
    public float tiltTarget = 0;
    /// <summary>
    /// 灯具 Dimmer 目标强度
    /// </summary>
    public float dimmerTarget = 0.0f;
    /// <summary>
    /// 灯具目标颜色
    /// </summary>
    public Color colorTarget = Color.black;
    /// <summary>
    /// 灯具目标 Gobo
    /// </summary>
    public Texture2D goboTarget = null;

    [Header("CurrentInfo")]
    /// <summary>
    /// 当前 Pan 轴角度
    /// </summary>
    public float pan = 0;
    /// <summary>
    /// 当前 Tilt 轴角度
    /// </summary>
    public float tilt = 0;
    /// <summary>
    /// 旋转速度
    /// </summary>
    public float rotSpeed = 400;

    private void Start()
    {
        fixtureSelector = GetComponent<GDTF_FixtureSelector>();
        ConfigChannelData();
    }

    private void ConfigChannelData()
    {
        
        NumChannels = GetFixtureModeChannelCount();
        
        for(int i = 0; i < fixtureSelector.dmxMode.channelsData.Count; i++)
        {
            // 配置 Pan 轴信息
            if(fixtureSelector.dmxMode.channelsData[i].logicalChannel.attribute.attributeName == "Pan")
            {
                panAddr = fixtureSelector.dmxMode.channelsData[i].offset;
                panFrom = fixtureSelector.dmxMode.channelsData[i].logicalChannel.channelFunctions[0].functionPhysicalFrom;
                panTo = fixtureSelector.dmxMode.channelsData[i].logicalChannel.channelFunctions[0].functionPhysicalTo;
            }

            // 配置 Tilt 轴信息
            if(fixtureSelector.dmxMode.channelsData[i].logicalChannel.attribute.attributeName == "Tilt")
            {
                tiltAddr = fixtureSelector.dmxMode.channelsData[i].offset;
                tiltFrom = fixtureSelector.dmxMode.channelsData[i].logicalChannel.channelFunctions[0].functionPhysicalFrom;
                tiltTo = fixtureSelector.dmxMode.channelsData[i].logicalChannel.channelFunctions[0].functionPhysicalTo;
            }

            // 配置 Dimmer 信息
            if(fixtureSelector.dmxMode.channelsData[i].logicalChannel.attribute.attributeName == "Dimmer")
            {
                dimmerAddr = fixtureSelector.dmxMode.channelsData[i].offset;
            }

            foreach(var item in fixtureSelector.dmxMode.channelsData[i].logicalChannel.channelFunctions)
            {
                if (Regex.IsMatch(item.attribute.attributeName, @"Color[0-9]+$"))
                {
                    colornFunction = item;
                    colorAddr = fixtureSelector.dmxMode.channelsData[i].offset;
                    colorSlots = fixtureSelector.descriptionData.wheels.Find(w => w.wheelName == item.wheelName).slots;
                    break;
                }
            }

            foreach(var item in fixtureSelector.dmxMode.channelsData[i].logicalChannel.channelFunctions)
            {
                if(Regex.IsMatch(item.attribute.attributeName, @"Gobo[0-9]+$"))
                {
                    gobonFunction = item;
                    goboAddr = fixtureSelector.dmxMode.channelsData[i].offset;
                    goboSlots = fixtureSelector.descriptionData.wheels.Find(w => w.wheelName == item.wheelName).slots;
                    break;
                }
            }
        }


    }

    public override void SetData(byte[] dmxData)
    {
        base.SetData(dmxData);

        if (dmxData == null)
        {
            return;
        }

        SetPan(dmxData);
        SetTilt(dmxData);
        SetDimmer(dmxData);
        SetColorN(dmxData);
        SetGoboN(dmxData);
    }

    private void Update()
    {
        UpdateRotation();
        UpdataDimmer();
        UpdataColor();
        UpdateGobo();
    }

    /// <summary>
    /// 更新灯具 Pan/Tilt
    /// </summary>
    void UpdateRotation()
    {
        // 计算 Pan 需要旋转的角度
        var dpan = panTarget - pan;
        // 防止当实际需要的角度 < Time.deltaTime * rotSpeed 时，灯光产生抖动
        // Mathf.Sign() 根据参数的正负 来返回 1 或 -1
        dpan = Mathf.Min(Mathf.Abs(dpan), Time.deltaTime * rotSpeed) * Mathf.Sign(dpan);
        pan += dpan;
        panRotater.Rotate(0, dpan, 0, Space.Self);


        var dtilt = tiltTarget - tilt;
        dtilt = Mathf.Min(Mathf.Abs(dtilt), Time.deltaTime * rotSpeed) * Mathf.Sign(dtilt);
        tilt += dtilt;
        tiltRotater.Rotate(0, 0, dtilt, Space.Self);

    }

    /// <summary>
    /// 更新灯具 Dimmer
    /// </summary>
    void UpdataDimmer()
    {
        lightHead.intensity = dimmerTarget;
    }

    void UpdataColor()
    {
        lightHead.color = colorTarget;
    }

    void UpdateGobo()
    {
        lightHead.lightMask = goboTarget;
    }

    #region 设置灯具参数
    /// <summary>
    /// 设置 Pan 轴
    /// </summary>
    /// <param name="dmxData">Dmx 数据</param>
    void SetPan(byte[] dmxData)
    {
        int value = DmxBit2Value(panAddr, dmxData);
        panTarget = MapRangeClamp(value, 0, Mathf.Pow(2, panAddr.Length * 8) - 1, panFrom, panTo);
    }

    /// <summary>
    /// 设置 Tilt 轴
    /// </summary>
    /// <param name="dmxData">dmx 数据</param>
    void SetTilt(byte[] dmxData)
    {
        int value = DmxBit2Value(tiltAddr, dmxData);
        tiltTarget = MapRangeClamp(value, 0, Mathf.Pow(2, tiltAddr.Length * 8) - 1, tiltFrom, tiltTo);
    }

    /// <summary>
    /// 设置 Dimmer 遮光器
    /// </summary>
    /// <param name="dmxData">dmx 数据</param>
    void SetDimmer(byte[] dmxData)
    {
        int value = DmxBit2Value(dimmerAddr, dmxData);
        dimmerTarget = MapRangeClamp(value, 0, Mathf.Pow(2, dimmerAddr.Length * 8) - 1, 0, 1);
    }

    void SetColorN(byte[] dmxData)
    {
        int value = DmxBit2Value(colorAddr, dmxData);
        if (value < colornFunction.functionDmxFrom || value > colornFunction.functionDmxTo)
        {
            return;
        }

        foreach (var item in colornFunction.channelSets)
        {
            if (value >= item.setDmxForm && value <= item.setDmxTo)
            {
                colorTarget = colorSlots[item.wheelSlotIndex - 1].color;
            }
        }
    }

    void SetGoboN(byte[] dmxData)
    {
        int value = DmxBit2Value(goboAddr, dmxData);

        if (value < gobonFunction.functionDmxFrom || value > colornFunction.functionDmxTo)
        {
            return;
        }

        foreach (var item in gobonFunction.channelSets)
        {
            if (value >= item.setDmxForm && value <= item.setDmxTo)
            {
                string goboFileName = goboSlots[item.wheelSlotIndex - 1].mediaFileName;
                if (goboFileName != null && goboFileName != string.Empty)
                {
                    goboTarget = fixtureSelector.goboTextures[goboSlots[item.wheelSlotIndex - 1].mediaFileName];
                }
                else
                {
                    goboTarget = null;
                }
            }
        }
    }
    #endregion

    #region 功能函数

    private int GetFixtureModeChannelCount()
    {
        int count = 0;
        if(fixtureSelector.dmxMode == null)
        {
            fixtureSelector.LoadConfig();
        }
        if(fixtureSelector.dmxMode != null)
        {
            foreach (var item in fixtureSelector.dmxMode.channelsData)
            {
                count += item.offset.Length;
            }
            return count;
        }
        else
        {
            Debug.LogError("Fixture Channel Counnt is null");
            return 0;
        }
    }

    int DmxBit2Value(int[] dmxAddr, byte[] dmxData)
    {
        if(dmxAddr.Length <= 0)
        {
            Debug.LogError("DMX Index Wrong!!!");
            return -1;
        }

        if(dmxAddr.Length == 1)
        {
            return dmxData[dmxAddr[0] - 1];
        }

        int value = 0;
        byte[] dataArray = new byte[dmxAddr.Length];

        for (int i = 0; i < dmxAddr.Length; i++)
        {
            dataArray[i] = dmxData[dmxAddr[i] - 1];
        }

        switch (dataArray.Length)
        {
            case 1:
                value = (int)dataArray[0];
                break;
            case 2:
                value = (int)((dataArray[0] << 8) | (dataArray[1] & 0x00FF));
                break;
            case 3:
                value = (int)((dataArray[0] << 16) | (dataArray[1] << 8) | (dataArray[2]) & 0x0000FF);
                break;
            case 4:
                value = (int)((dataArray[0] << 24) | (dataArray[1] << 16) | (dataArray[2] << 8) | (dataArray[3] & 0x000000FF));
                break;
            default:
                value = -1;
                break;
        }

        return value;
    }

    public float MapRangeClamp(float value, float inFrom, float inEnd, float outFrom, float outEnd)
    {
        value = Mathf.Clamp(value, inFrom, inEnd);

        float inLength = Mathf.Abs(inEnd - inFrom);
        float lengthValueToFrom = Mathf.Abs(value - inFrom);
        float curPercent = lengthValueToFrom / inLength;

        return (outFrom + (outEnd - outFrom) * curPercent);

    }
    #endregion
}
