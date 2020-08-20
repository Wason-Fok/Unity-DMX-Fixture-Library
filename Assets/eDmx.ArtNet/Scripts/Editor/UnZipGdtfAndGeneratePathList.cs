using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 解压 GDTF 文件并生成路径索引
/// </summary>
public class UnZipGdtfAndGeneratePathList : Editor
{
    #region 配置信息
    /// <summary>
    /// Json 配置文件名
    /// </summary>
    public static string configFileName = "GDTF_ResourcesFileInfo.json";
    /// <summary>
    /// Resources 文件夹路径
    /// </summary>
    private static readonly string resString = "\\Assets\\eDmx.ArtNet\\Resources\\";
    /// <summary>
    /// GDTF 文件存放根目录
    /// </summary>
    private static readonly string path = Application.dataPath + "/eDmx.ArtNet/Resources/";
    #endregion

    [MenuItem("GDTF/UnZip all GDTF and creat JSON")]
    public static void UnZipAllGdtf()
    {
        // 如果该路径存在
        if (Directory.Exists(path))
        {
            // 获取文件夹信息
            DirectoryInfo direction = new DirectoryInfo(path);
            // 获取该路径下所有后缀名为 .gdtf 文件
            FileInfo[] gdtfFiles = direction.GetFiles("*.gdtf", SearchOption.AllDirectories);

            Debug.LogWarning($"Find {gdtfFiles.Length} GDTF Files!");

            // 存放所有 GDTF 资源文件索引信息
            List<GDTF_FileInfo> fileInfos = new List<GDTF_FileInfo>();

            for (int i = 0; i < gdtfFiles.Length; i++)
            {
                GDTF_FileInfo resInfo = new GDTF_FileInfo();

                // 截取 GDTF 文件名（不包含扩展名）
                string gdtfFileName = Path.GetFileNameWithoutExtension(gdtfFiles[i].ToString());
                // GDTF 文件绝对路径
                string gdtfPath = gdtfFiles[i].ToString();
                // 目标解压缩路径
                string unZipPath = path + "GDTF_Unzip/" + gdtfFileName + "/";

                // 解压 GDTF 文件到指定目录
                if(!UnzipGDTF(gdtfPath, unZipPath))
                {
                    Debug.Log($"UnZip GDTF File :{gdtfFiles[i].Name} Error!!!");
                    continue;
                }

                resInfo.name = gdtfFileName;

                // 开始索引解压目录下的资源文件
                DirectoryInfo unZipPathInfo = new DirectoryInfo(unZipPath);
                if (unZipPathInfo.Exists)
                {
                    // 查找 xml 配置文件以及缩略图
                    foreach(var item in unZipPathInfo.GetFiles("*", SearchOption.TopDirectoryOnly))
                    {
                        if(item.Extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
                        {
                            resInfo.thumbnail = new FileNameAndPath(Path.GetFileNameWithoutExtension(item.Name), ResourcesApiPath(item));
                        }
                        
                        if(item.Name.Equals("description.xml", StringComparison.OrdinalIgnoreCase))
                        {
                            resInfo.description = new FileNameAndPath(Path.GetFileNameWithoutExtension(item.Name), item.ToString());
                        }
                    }

                    // 查找 Wheel 图片以及 Model 模型
                    foreach(var item in unZipPathInfo.GetDirectories())
                    {
                        if(item.Name.Equals("wheels", StringComparison.OrdinalIgnoreCase))
                        {
                            List<FileNameAndPath> wheelsNameAndPath = new List<FileNameAndPath>();

                            foreach(var wheel in item.GetFiles("*.png", SearchOption.AllDirectories))
                            {
                                string asstePath = AssetImpoterApiPath(wheel);
                                TextureImporter texture = AssetImporter.GetAtPath(asstePath) as TextureImporter;
                                if(texture != null)
                                {
                                    texture.textureType = TextureImporterType.Cookie;
                                    texture.alphaSource = TextureImporterAlphaSource.FromGrayScale;
                                    texture.wrapMode = TextureWrapMode.Clamp;
                                    AssetDatabase.ImportAsset(asstePath);
                                }
                                
                                wheelsNameAndPath.Add(new FileNameAndPath(Path.GetFileNameWithoutExtension(wheel.Name), ResourcesApiPath(wheel)));
                            }

                            resInfo.wheels = wheelsNameAndPath.ToArray();
                        }

                        if(item.Name.Equals("models", StringComparison.OrdinalIgnoreCase))
                        {
                            List<FileNameAndPath> modelsNameAndPath = new List<FileNameAndPath>();

                            foreach(var model in item.GetFiles("*.3ds", SearchOption.AllDirectories))
                            {
                                modelsNameAndPath.Add(new FileNameAndPath(Path.GetFileNameWithoutExtension(model.Name), ResourcesApiPath(model)));
                            }

                            resInfo.models = modelsNameAndPath.ToArray();
                        }
                    }
                }

                fileInfos.Add(resInfo);
            }

            // 将对象转换成 JSON 格式别生成文件
            GDTF_ResourcesFiles resourcesFiles = new GDTF_ResourcesFiles() { Fixtures = fileInfos.ToArray() };
            string json = JsonUtility.ToJson(resourcesFiles);
            File.WriteAllText("Assets/eDmx.ArtNet/Resources/GDTF_Configs/" + configFileName, json);
        }

        AssetDatabase.Refresh();
        GDTF_ResourcesLoader.LoadGdtfResourcesFiles();
    }

    #region 功能函数
    /// <summary>
    /// 将资源文件路径解析为 Resources API 支持的格式
    /// </summary>
    /// <param name="fileInfo"></param>
    /// <returns>路径</returns>
    private static string ResourcesApiPath(FileInfo fileInfo)
    {
        // 获取文件绝对路径
        string filePath = fileInfo.ToString();

        // 计算需要删除的部分
        int index = filePath.IndexOf(resString);
        index += resString.Length;

        string resPath = filePath.Remove(0, index).Replace(fileInfo.Extension, string.Empty).Replace("\\", "/");

        return resPath;
    }
    /// <summary>
    /// 将资源文件路径解析为 AssetImporter API 支持的格式
    /// </summary>
    /// <param name="fileInfo"></param>
    /// <returns></returns>
    private static string AssetImpoterApiPath(FileInfo fileInfo)
    {
        string filePath = fileInfo.ToString();

        int index = filePath.IndexOf("Assets");
        string resPath = filePath.Remove(0, index).Replace("\\", "/");

        return resPath;
    }
    /// <summary>
    /// 解压 GDTF 文件
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="targetPath">解压到</param>
    /// <returns>是否解压成功</returns>
    private static bool UnzipGDTF(string filePath, string targetPath)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError("Unzip GDTF file error! Not found GDTF file!");
            return false;
        }

        if (filePath != string.Empty && filePath != null)
        {
            ZipUtil.Unzip(filePath, targetPath);
            return true;
        }

        return false;
    }
    #endregion
}
