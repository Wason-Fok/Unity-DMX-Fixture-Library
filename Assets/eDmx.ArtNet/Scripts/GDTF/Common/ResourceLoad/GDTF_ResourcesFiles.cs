using System;

[Serializable]
public class GDTF_ResourcesFiles
{
    public GDTF_FileInfo[] Fixtures;
}

[Serializable]
public class GDTF_FileInfo
{
    public string name;
    public FileNameAndPath thumbnail;
    public FileNameAndPath description;
    public FileNameAndPath[] wheels;
    public FileNameAndPath[] models;
}

[Serializable]
public class FileNameAndPath
{
    public string fileName;
    public string filePath;
    public FileNameAndPath(string fileName, string filePath)
    {
        this.fileName = fileName;
        this.filePath = filePath;
    }
}