using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LightHead : MonoBehaviour
{
    /// <summary>
    /// 光束组件
    /// </summary>
    //public NoribenLightBeam beam;

    /// <summary>
    /// 灯光强度
    /// </summary>
    public float intensity = 1f;

    /// <summary>
    /// 灯光强度倍数
    /// </summary>
    public float headOnlyIntensityMultiplier = 1f;

    /// <summary>
    /// 灯光面板颜色
    /// </summary>
    public Color color = Color.red;

    /// <summary>
    /// 光束外径
    /// </summary>
    public float lightOutterAngle = 30;
    /// <summary>
    /// 光束内径
    /// </summary>
    public float lightInnerAngle = 1;

    public Texture2D lightMask = null;

    /// <summary>
    /// 灯光面板网格渲染器、材质
    /// </summary>
    protected MeshRenderer rendererCom;
    protected MaterialPropertyBlock matBlock;

    /// <summary>
    /// 灯光组件
    /// </summary>
    private Light lightCom;


    protected virtual void Start()
    {
        rendererCom = GetComponent<MeshRenderer>();
        lightCom = GetComponentInChildren<Light>();
        matBlock = new MaterialPropertyBlock();

        rendererCom.GetPropertyBlock(matBlock);
        matBlock.SetInt("_UseEmissiveIntensity", 1);
        matBlock.SetColor("_EmissiveColor", Color.white);
        matBlock.SetFloat("_EmissiveIntensity", 1.0f);

        rendererCom.SetPropertyBlock(matBlock);

    }

    protected virtual void Update()
    {

        //beam.color = color;
        //beam.intensity = intensity;

        lightCom.color = color;
        lightCom.intensity =  1.0f * intensity;

        lightCom.spotAngle = lightOutterAngle;
        lightCom.innerSpotAngle = lightInnerAngle;

        lightCom.cookie = lightMask;

        rendererCom.GetPropertyBlock(matBlock);
        matBlock.SetColor("_EmissiveColor", color);
        matBlock.SetFloat("_EmissiveIntensity", intensity * headOnlyIntensityMultiplier);
        rendererCom.SetPropertyBlock(matBlock);
    }
}
