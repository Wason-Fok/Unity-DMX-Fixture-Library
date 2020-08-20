using UnityEngine;

[System.Serializable]
public class UniverseDevices
{
    /// <summary>
    /// Universe 名称
    /// </summary>
    public string universeName;

    /// <summary>
    /// 子网
    /// </summary>
    [Range(0, 15)]
    public int subNet;

    /// <summary>
    /// Universe 编号
    /// </summary>
    [Range(0, 15)]
    public int universe;

    /// <summary>
    /// DMX 设备
    /// </summary>
    public DMXDevice[] devices;

    /// <summary>
    /// 初始化
    /// </summary>
    public void Initialize()
    {
        var startChannel = 0;
        foreach (var d in devices)
            if (d != null)
            {
                d.startChannel = startChannel;
                startChannel += d.NumChannels;
                d.name = string.Format("{0}[(S:{1}-U:{2})({3:d3}-{4:d3})]", d.GetType().ToString(), subNet, universe, d.startChannel, startChannel - 1);
            }
        if (startChannel > 512)
            Debug.LogErrorFormat("The number({0}) of channels of the universe {1} exceeds the upper limit(512 channels)!", startChannel, universe);
    }
}
