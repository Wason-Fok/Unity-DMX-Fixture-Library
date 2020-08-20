using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

public class DmxSendGUI : MonoBehaviour
{
    /// <summary>
    /// 通道 Slider 组件
    /// </summary>
    public Slider channelSliderOrigin;

    /// <summary>
    /// SubNet
    /// </summary>
    public byte subNet;
    /// <summary>
    /// Universe
    /// </summary>
    public byte universe;
    public int fps;


    [SerializeField] private DmxController controller = null;
    [SerializeField] Slider[] channelSliders = new Slider[512];
    [SerializeField] byte[] dmxData;
    Thread dmxSender;

    [ContextMenu("build GUI")]
    void BuildGUI()
    {
        for (var i = 0; i < 512; i++)
        {
            var slider = channelSliders[i] = Instantiate(channelSliderOrigin, channelSliderOrigin.transform.parent);
            slider.name = string.Format("Slider{0:d3}", i);
            var rTrs = slider.GetComponent<RectTransform>();
            rTrs.localPosition = new Vector3(rTrs.rect.width * (i % 128 - 64), (rTrs.rect.height + 32) * (i / 128 - 2), 0);
        }
    }

    /// <summary>
    /// 是否开启 DMX 发送线程
    /// </summary>
    /// <param name="b"></param>
    public void SetSendingDMX(bool b)
    {
        if (dmxSender != null)
            dmxSender.Abort();
        if (b)
        {
            dmxSender = new Thread(SendDmx);
            dmxSender.Start();
        }
        else
            dmxSender = null;
    }

    /// <summary>
    /// 设置 SubNet
    /// </summary>
    /// <param name="str">SubNet</param>
    public void SetSubNet(string str)
    {
        subNet = (byte)Mathf.Clamp(int.Parse(str), 0, 15);
    }

    /// <summary>
    /// 设置 Universe
    /// </summary>
    /// <param name="str">Universe</param>
    public void SetUniverse(string str)
    {
        universe = (byte)Mathf.Clamp(int.Parse(str), 0, 15);
    }

    /// <summary>
    /// 设置发送 FPS
    /// </summary>
    /// <param name="str"></param>
    public void SetFps(string str)
    {
        var fps = int.Parse(str);
        this.fps = Mathf.Max(1, fps);
    }

    /// <summary>
    /// 设置 DMX 值
    /// </summary>
    /// <param name="channel">通道</param>
    /// <param name="val">值</param>
    void SetDmxValue(int channel, float val)
    {
        dmxData[channel] = (byte)Mathf.FloorToInt(Mathf.Lerp(0, 255, val));
    }

    // Use this for initialization
    void Start()
    {
        dmxData = new byte[512];
        subNet = 0;
        universe = 0;
        fps = 30;
        for (var i = 0; i < channelSliders.Length; i++)
        {
            var channel = i;
            channelSliders[channel].onValueChanged.AddListener((f) => SetDmxValue(channel, f));
        }
    }

    private void OnDestroy()
    {
        if (dmxSender != null)
            dmxSender.Abort();
    }

    void SendDmx()
    {
        while (true)
        {
            controller.Send(subNet, universe, dmxData);
            Thread.Sleep(System.Math.Max(1, 1000 / fps));
        }
    }
}
