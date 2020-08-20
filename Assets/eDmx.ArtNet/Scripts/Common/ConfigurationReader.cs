using System;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public class ConfigurationReader
{
    /// <summary>
    /// 读取配置文件
    /// </summary>
    /// <param name="fileName">配置文件名</param>
    /// <returns>读取后的数据</returns>
    public static string GetConfigFile(string fileName, string path = null)
    {
        Uri uri;

        if (path == null || path == string.Empty)
        {
            uri = new Uri(Application.streamingAssetsPath + "/" + fileName);
        }
        else
        {
            uri = new Uri(path + "/" + fileName);
        }
        
        if (!File.Exists(uri.LocalPath))
        {
            Debug.LogError("Can not found Config File!");
            return null;
        }

        using(UnityWebRequest unityWebRequest = UnityWebRequest.Get(uri))
        {
            unityWebRequest.SendWebRequest();
            while (true)
            {
                if (unityWebRequest.isDone)
                {
                    return unityWebRequest.downloadHandler.text;
                }
            }
        }
    }

    /// <summary>
    /// 解析数据
    /// </summary>
    /// <param name="fileContent">要解析的字符串</param>
    /// <param name="handler">自定义解析方法</param>
    public static void Reader(string fileContent, Action<string> handler)
    {
        string line;
        using(StringReader reader = new StringReader(fileContent))
        {
            while((line = reader.ReadLine()) != null)
            {
                handler(line);
            }
        }
    }
}
