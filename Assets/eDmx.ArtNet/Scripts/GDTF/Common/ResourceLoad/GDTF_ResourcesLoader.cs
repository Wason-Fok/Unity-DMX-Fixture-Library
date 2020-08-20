using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class GDTF_ResourcesLoader : MonoBehaviour
{
    private static string configFileName = "GDTF_ResourcesFileInfo.json";
    private static string configFilePath = Application.dataPath + "/eDmx.ArtNet/Resources/GDTF_Configs/";
    private static GDTF_ResourcesFiles resourcesFiles;

    private static Dictionary<string, GDTF_FileInfo> fixtures;

    /// <summary>
    /// 获取所有灯具相关资源信息
    /// </summary>
    /// <returns></returns>
    public static Dictionary<string, GDTF_FileInfo> GetFixtures()
    {
        if (fixtures == null)
        {
            fixtures = new Dictionary<string, GDTF_FileInfo>();
        }

        if (fixtures.Count <= 0)
        {
            SetFixtures();
            return fixtures;
        }
        else
        {
            return fixtures;
        }
    }

    /// <summary>
    /// 根据灯具名称返回对应资源文件信息
    /// </summary>
    /// <returns></returns>
    public static GDTF_FileInfo GetFileInfo(string fixtureName)
    {
        if(fixtureName == null || fixtureName == string.Empty)
        {
            Debug.LogError("Fixture name Wrong!");
            return null;
        }

        return GetFixtures()[fixtureName];
    }

    /// <summary>
    /// 遍历从 Json 转换的对象 将所有灯具对应的资源信息保存在 Dictionary 中
    /// </summary>
    private static void SetFixtures()
    {
        if(fixtures == null)
        {
            fixtures = new Dictionary<string, GDTF_FileInfo>();
        }

        if(resourcesFiles == null)
        {
            LoadGdtfResourcesFiles();
        }

        foreach (var item in resourcesFiles.Fixtures)
        {
            fixtures.Add(item.name, item);
        }
    }

    /// <summary>
    /// 重新读取 GDTF JSON
    /// </summary>
    public static void ReloadGdtfResourcesFiles()
    {
        if(fixtures == null)
        {
            fixtures = new Dictionary<string, GDTF_FileInfo>();
        }

        fixtures.Clear();
        LoadGdtfResourcesFiles();
        SetFixtures();
    }

    /// <summary>
    /// 读取 GDTF JSON 并生成对象
    /// </summary>
    public static void LoadGdtfResourcesFiles()
    {
        DirectoryInfo dirction = new DirectoryInfo(configFilePath);
        FileInfo[] configFiles = dirction.GetFiles(configFileName);

        string json = ConfigurationReader.GetConfigFile(configFileName, configFiles[0].Directory.ToString());

        if (json != null)
        {
            resourcesFiles = JsonUtility.FromJson<GDTF_ResourcesFiles>(json);
        }
    }
}
