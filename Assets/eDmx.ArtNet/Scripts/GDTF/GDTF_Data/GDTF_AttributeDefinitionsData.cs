using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GDTF 物理单位
/// </summary>
public enum PhysicalUnit
{
    None = 0,
    /// <summary>
    /// 百分比
    /// </summary>
    Percent,
    /// <summary>
    /// 长度 m
    /// </summary>
    Length,
    /// <summary>
    /// 重量 kg
    /// </summary>
    Mass,
    /// <summary>
    /// 时间 s
    /// </summary>
    Time,
    /// <summary>
    /// 温度 K
    /// </summary>
    Temperature,
    /// <summary>
    /// 亮度 cd
    /// </summary>
    LuminousIntensity,
    /// <summary>
    /// 角度
    /// </summary>
    Angle,
    /// <summary>
    /// 力量 N
    /// </summary>
    Force,
    /// <summary>
    /// 频率 Hz
    /// </summary>
    Frequency,
    /// <summary>
    /// 电流 A
    /// </summary>
    Current,
    /// <summary>
    /// 电压 V
    /// </summary>
    Voltage,
    /// <summary>
    /// 功率 W
    /// </summary>
    Power,
    /// <summary>
    /// 电量 J
    /// </summary>
    Energy,
    /// <summary>
    /// 面积 ㎡
    /// </summary>
    Area,
    /// <summary>
    /// 体积 m³
    /// </summary>
    Volume,
    /// <summary>
    /// 速度 m/s2
    /// </summary>
    Speed,
    /// <summary>
    /// 加速度 m/s
    /// </summary>
    Acceleration,
    /// <summary>
    /// 角速度 度/s
    /// </summary>
    AngularSpeed,
    /// <summary>
    /// 角质量 度/s
    /// </summary>
    AngularAccc,
    /// <summary>
    /// 波长 nm
    /// </summary>
    WaveLength,
    /// <summary>
    /// 颜色组件
    /// </summary>
    ColorComponent
}

/// <summary>
/// GDTF AttributeDefinitions 信息
/// </summary>
public class GDTF_AttributeDefinitionsData
{
    /// <summary>
    /// 所有 Attribute 信息 
    /// </summary>
    public Dictionary<string, GDTF_AttributeDefinitionsData_Attribute> attributes;
    /// <summary>
    /// 所有 激活组 信息
    /// </summary>
    public Dictionary<string, GDTF_AttributeDefinitionsData_ActivationGroup> activationGroups;
    /// <summary>
    /// 所有 功能组 信息
    /// </summary>
    public Dictionary<string, GDTF_AttributeDefinitionsData_FeatureGroup> featureGroups;
}

/// <summary>
/// GDTF 激活组
/// </summary>
public class GDTF_AttributeDefinitionsData_ActivationGroup
{
    /// <summary>
    /// 组名称
    /// </summary>
    public string activationGroupName = string.Empty;
    /// <summary>
    /// 该组中绑定的属性
    /// </summary>
    public List<GDTF_AttributeDefinitionsData_Attribute> attributes = new List<GDTF_AttributeDefinitionsData_Attribute>();
}

/// <summary>
/// GDTF 功能组
/// </summary>
public class GDTF_AttributeDefinitionsData_FeatureGroup
{
    /// <summary>
    /// 该功能组名称
    /// </summary>
    public string featureGroupName = string.Empty;
    /// <summary>
    /// 功能组所有功能
    /// </summary>
    public Dictionary<string, GDTF_AttributeDefinitionsData_Feature> features = new Dictionary<string, GDTF_AttributeDefinitionsData_Feature>();
}

/// <summary>
/// GDTF 功能
/// </summary>
public class GDTF_AttributeDefinitionsData_Feature 
{
    /// <summary>
    /// 功能名称
    /// </summary>
    public string fatureName = string.Empty;
    /// <summary>
    /// 功能所绑定的所有属性
    /// </summary>
    public List<GDTF_AttributeDefinitionsData_Attribute> attributes = new List<GDTF_AttributeDefinitionsData_Attribute>();
}

/// <summary>
/// GDTF 属性
/// </summary>
public class GDTF_AttributeDefinitionsData_Attribute
{
    /// <summary>
    /// 属性名称
    /// </summary>
    public string attributeName = string.Empty;
    /// <summary>
    /// 父属性
    /// </summary>
    public GDTF_AttributeDefinitionsData_Attribute mainAttribute = null;
    /// <summary>
    /// 子属性列表
    /// </summary>
    public List<GDTF_AttributeDefinitionsData_Attribute> childAttribute = new List<GDTF_AttributeDefinitionsData_Attribute>();
    /// <summary>
    /// 属性物理单位
    /// </summary>
    public PhysicalUnit physicalUnit = PhysicalUnit.None;
    /// <summary>
    /// 颜色信息
    /// </summary>
    public Color color = Color.black;
}