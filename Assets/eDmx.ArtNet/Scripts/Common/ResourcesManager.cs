using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ResourcesManager
{
    // Key：资源文件名，Value：资源文件路径
    private static Dictionary<string, string> configMapDIC;
    // 文件读取出的字符串
    private static string fileContent;

    // 作用：初始化类的静态数据成员
    // 时机：类被加载时执行一次
    static ResourcesManager()
    {
        Init();
    }

    private static void Init()
    {
        // 初始化 Dictionary
        configMapDIC = new Dictionary<string, string>();
        // 加载文件
        fileContent = ConfigurationReader.GetConfigFile("");
        // 解析文件（string --> Dictionary<string, string>)
        ConfigurationReader.Reader(fileContent, BuildMap);
    }

    /// <summary>
    /// 解析字符串并将解析后的数据保存再字典中
    /// </summary>
    /// <param name="line">读取到的行数据</param>
    private static void BuildMap(string line)
    {
        // 解析行数据
        string[] keyValue = line.Split('=');
        configMapDIC.Add(keyValue[0], keyValue[1]);
    }

    /// <summary>
    /// 通过资源文件名找到资源文件
    /// </summary>
    /// <typeparam name="T">返回的对象类型</typeparam>
    /// <param name="perfabName">资源文件名</param>
    /// <returns></returns>
    public static T Load<T>(string prefabName) where T : UnityEngine.Object
    {
        string prefabPath = configMapDIC[prefabName];
        return Resources.Load<T>(prefabPath);
    }
}
