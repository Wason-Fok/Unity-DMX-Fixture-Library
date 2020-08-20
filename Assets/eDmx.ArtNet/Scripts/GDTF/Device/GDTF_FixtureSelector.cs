using System.Collections.Generic;
using UnityEngine;

public class GDTF_FixtureSelector : MonoBehaviour
{
    /// <summary>
    /// 灯具缩略图
    /// </summary>
    public Texture2D fixtureThumbanil = null;

    #region 灯具基本信息
    public string gdtfDataVer = null;
    public string manufacturer = null;
    public string model = null;
    public string fixtureType = null;
    public string fixtureTypeID = null;
    #endregion

    /// <summary>
    /// GDTF 文件名
    /// </summary>
    public string gdtfFileName = null;

    /// <summary>
    /// 灯具 Wheels Texture 纹理
    /// </summary>
    public Dictionary<string, Texture2D> goboTextures = new Dictionary<string, Texture2D>();

    /// <summary>
    /// 灯具 DMX 模式名称
    /// </summary>
    public string dmxModeName = null;

    /// <summary>
    /// 当前灯具选择的 DmxMode
    /// </summary>
    public GDTF_DmxModesData dmxMode;

    /// <summary>
    /// 灯库信息
    /// </summary>
    public GDTF_Data descriptionData = null;

    /// <summary>
    /// 对应灯具名称的所有资源文件名称及路径
    /// </summary>
    private GDTF_FileInfo fileInfo;

    private void Awake()
    {
        LoadConfig();
    }

    /// <summary>
    /// 根据灯具名称开始加载配置信息
    /// </summary>
    public void LoadConfig()
    {
        // 如果 fixtureName 为空 或者 Dictionary 中没有该灯具信息 则跳出函数
        if(gdtfFileName == null || gdtfFileName == string.Empty)
        {
            return;
        }
        if (!GDTF_ResourcesLoader.GetFixtures().ContainsKey(gdtfFileName))
        {
            return;
        }

        // 获取该灯具的所有资源文件信息
        fileInfo = GDTF_ResourcesLoader.GetFileInfo(gdtfFileName);

        // 添加灯具缩略图
        fixtureThumbanil = Resources.Load<Texture2D>(fileInfo.thumbnail.filePath);
        // 添加灯具灯库信息
        descriptionData = GDTF_DescriptionReader.GetGdtfData(fileInfo.description.filePath);

        // 添加灯具基本信息
        model = descriptionData.fixtureType.ShortName;
        gdtfDataVer = descriptionData.fixtureType.GDTFDataVersion;
        fixtureType = descriptionData.fixtureType.FixtureType;
        fixtureTypeID = descriptionData.fixtureType.FixtureTypeID;
        manufacturer = descriptionData.fixtureType.Manufacturer;

        descriptionData.dmxModes.ForEach(mode => { if (mode.dmxModeName == dmxModeName) dmxMode = mode; });

        if(goboTextures.Count > 0)
        {
            goboTextures.Clear();
        }

        for(int i = 0; i < fileInfo.wheels.Length; i++)
        {
            goboTextures.Add(fileInfo.wheels[i].fileName, Resources.Load<Texture2D>(fileInfo.wheels[i].filePath));
        }
    }

    [ContextMenu("Reload Resources")]
    public void ReloadResources()
    {
        GDTF_ResourcesLoader.ReloadGdtfResourcesFiles();
    }
}
