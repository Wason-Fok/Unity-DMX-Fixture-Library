using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using UnityEngine;

public class GDTF_DescriptionReader
{
    public static GDTF_Data GetGdtfData(string xmlPath)
    {
        GDTF_Data data = new GDTF_Data();

        if(xmlPath == null || xmlPath == string.Empty)
        {
            Debug.LogError("Fixture description.xml not found!");
            return null;
        }

        LoadXmlFile(xmlPath, data);

        return data;
    }

    private static void LoadXmlFile(string filePath, GDTF_Data data)
    {
        if (File.Exists(filePath))
        {
            // 打开 XML 文件并设置根节点
            xmlDoc.Load(filePath);
            rootNode = xmlDoc.SelectSingleNode("GDTF");

            // 获取 GDTF 版本信息
            data.fixtureType.GDTFDataVersion = GetNodeAttribute(rootNode, "DataVersion");

            // 获取 FixtureType 节点
            fixtureTypeNode = rootNode.FirstChild;
            
            // 获取 FixtureType 节点属性
            data.fixtureType.FixtureType = GetNodeAttribute(fixtureTypeNode, "Description");
            data.fixtureType.FixtureTypeID = GetNodeAttribute(fixtureTypeNode, "FixtureTypeID");
            data.fixtureType.LongName = GetNodeAttribute(fixtureTypeNode, "LongName");
            data.fixtureType.Manufacturer = GetNodeAttribute(fixtureTypeNode, "Manufacturer");
            data.fixtureType.Name = GetNodeAttribute(fixtureTypeNode, "Name");
            data.fixtureType.RefFT = GetNodeAttribute(fixtureTypeNode, "RefFT");
            data.fixtureType.ShortName = GetNodeAttribute(fixtureTypeNode, "ShortName");
            data.fixtureType.Thumbnail = GetNodeAttribute(fixtureTypeNode, "Thumbnail");

            foreach (XmlNode item in fixtureTypeNode.ChildNodes)
            {
                switch (item.Name)
                {
                    case "AttributeDefinitions":
                        attributeDefinitionsNode = item;
                        GetAttributeDefinitions(data);
                        break;
                    case "Wheels":
                        wheelsNode = item;
                        GetWheelsData(data);
                        break;
                    case "PhysicalDescriptions":
                        physicalDescriptionsNode = item;
                        break;
                    case "Models":
                        modelsNode = item;
                        break;
                    case "Geometries":
                        geometriesNode = item;
                        break;
                    case "DMXModes":
                        dmxModes = item;
                        GetDmxModes(data);
                        break;
                    case "Revisions":
                        revisionsNode = item;
                        break;
                    case "FTPresets":
                        ftPresetsNode = item;
                        break;
                    case "FTMacros":
                        ftMacrosNode = item;
                        break;
                    case "Protocols":
                        protocolsNode = item;
                        break;
                    default:
                        break;
                }
            }
        }
        else
        {
            Debug.LogError("Xml Unfined!");
        }
    }

    #region XML 节点

    private static XmlDocument xmlDoc = new XmlDocument();
    private static XmlNode rootNode;
    private static XmlNode fixtureTypeNode;
    private static XmlNode attributeDefinitionsNode;
    private static XmlNode wheelsNode;
    private static XmlNode physicalDescriptionsNode;
    private static XmlNode modelsNode;
    private static XmlNode geometriesNode;
    private static XmlNode dmxModes;
    private static XmlNode revisionsNode;
    private static XmlNode ftPresetsNode;
    private static XmlNode ftMacrosNode;
    private static XmlNode protocolsNode;

    #endregion

    #region 获取 GDTF 属性信息
    private static void GetAttributeDefinitions(GDTF_Data data)
    {
        // 如果 AttributeDefinitions 节点为空则推出
        if(attributeDefinitionsNode == null)
        {
            Debug.Log("Not Found AttributeDefinitions Node!");
            return;
        }

        // 如果 AttributeDefinitions 节点存在子节点
        if (attributeDefinitionsNode.HasChildNodes)
        {
            // GDTF_Data 对象中保存的信息
            Dictionary<string, GDTF_AttributeDefinitionsData_Attribute> attributes = new Dictionary<string, GDTF_AttributeDefinitionsData_Attribute>();
            Dictionary<string, GDTF_AttributeDefinitionsData_ActivationGroup> activationGroups = new Dictionary<string, GDTF_AttributeDefinitionsData_ActivationGroup>();
            Dictionary<string, GDTF_AttributeDefinitionsData_FeatureGroup> featureGroups = new Dictionary<string, GDTF_AttributeDefinitionsData_FeatureGroup>();

            // 查找 Group 节点
            foreach (XmlNode groupsNode in attributeDefinitionsNode)
            {
                // 添加激活组相关信息
                if(groupsNode.Name == "ActivationGroups" && groupsNode.HasChildNodes)
                {
                    GetActivationGroups(groupsNode, activationGroups);
                }

                // 添加功能组相关信息
                if (groupsNode.Name == "FeatureGroups" && groupsNode.HasChildNodes)
                {
                    GetFeatureGroups(groupsNode, featureGroups);
                }
            }

            // 查找 Attributes 节点
            foreach(XmlNode attributesNode in attributeDefinitionsNode)
            {
                // 如果存在 Attributes 节点，并且该节点存在子节点
                if (attributesNode.Name == "Attributes" && attributesNode.HasChildNodes)
                {
                    // 遍历 Attributes 节点下的 Attribute 节点
                    foreach (XmlNode attributeNode in attributesNode)
                    {
                        // Attribute 中所绑定的 ActivationGroup 名称、Feature 名称、Color 名称
                        string activationGroupName = GetNodeAttribute(attributeNode, "ActivationGroup");
                        string featureName = GetNodeAttribute(attributeNode, "Feature");
                        string colorName = GetNodeAttribute(attributeNode, "Color");

                        // 如果存在 ActivationGroup
                        if (activationGroupName != null)
                        {
                            // 生成 Attribute 对象并绑定到 ActivationGroup 中
                            GDTF_AttributeDefinitionsData_Attribute attribute = GenerateAttribute(attributes, attributeNode);
                            if(attribute != null)
                            {
                                activationGroups[activationGroupName].attributes.Add(attribute);
                            }
                        }

                        // 如果存在 Feature
                        if(featureName != null)
                        {
                            // 生成 Attribute 对象并绑定到对应的 Feature 中
                            GDTF_AttributeDefinitionsData_Attribute attribute = GenerateAttribute(attributes, attributeNode);
                            if (attribute != null)
                            {
                                string[] featurePath = featureName.Split('.');

                                featureGroups[featurePath[0]].features[featurePath[1]].attributes.Add(attribute);
                            }
                        }
                    }
                }
            }

            data.attributeDefinitions.activationGroups = activationGroups;
            data.attributeDefinitions.featureGroups = featureGroups;
            data.attributeDefinitions.attributes = attributes;
        }
    }

    /// <summary>
    /// 根据 Attribute 节点，将 MainAttribute 进行绑定
    /// </summary>
    /// <param name="attributes">保存所有 Attribute 的字典</param>
    /// <param name="attributeNode">当前节点</param>
    private static GDTF_AttributeDefinitionsData_Attribute GenerateAttribute(Dictionary<string, GDTF_AttributeDefinitionsData_Attribute> attributes, XmlNode attributeNode)
    {
        // 如果字典中存在 Attribute 属性，因为 Feature 和 ActivationGroup 可能会绑定同一个 Attribute
        string attributeName = GetNodeAttribute(attributeNode, "Name");
        if(attributeName != null && attributes.ContainsKey(attributeName))
        {
            return attributes[attributeName];
        }

        // 获取 Attributes 节点
        XmlNode attributesNode = attributeNode.ParentNode;

        // 父 Attribute 名称
        string mainAttributeName = GetNodeAttribute(attributeNode, "MainAttribute");

        // 如果当前 Attribute 存在 父Attribute
        if (mainAttributeName != null)
        {
            GDTF_AttributeDefinitionsData_Attribute mainAttribute = null;

            // 在当前保存 Attribute 的字典中查找指定名称的 Attribute 对象
            if (attributes.ContainsKey(mainAttributeName))
            {
                mainAttribute = attributes[mainAttributeName];
            }
            

            // 如果找到了并为其绑定
            if (mainAttribute != null)
            {
                GDTF_AttributeDefinitionsData_Attribute attribute = CreateAttribute(attributeNode);
                attribute.mainAttribute = mainAttribute;
                mainAttribute.childAttribute.Add(attribute);

                attributes[attribute.attributeName] = attribute;
                return attribute;
            }
            // 如果没找到先创建 父Attribute 再绑定
            else
            {
                GDTF_AttributeDefinitionsData_Attribute attribute = null;

                foreach (XmlNode mainAttributeNode in attributesNode)
                {
                    string name = GetNodeAttribute(mainAttributeNode, "Name");
                    if (name != null && name == mainAttributeName)
                    {
                        mainAttribute = CreateAttribute(mainAttributeNode);
                        attribute = CreateAttribute(attributeNode);
                        mainAttribute.childAttribute.Add(attribute);

                        attributes[mainAttribute.attributeName] = mainAttribute;
                        attributes[attribute.attributeName] = attribute;
                        break;
                    }
                }

                return attribute;
            }
        }
        else
        {
            GDTF_AttributeDefinitionsData_Attribute attribute = CreateAttribute(attributeNode);
            attributes[attribute.attributeName] = attribute;

            return attribute;
        }
    }

    /// <summary>
    /// 根据 Attribute 节点创建 Attribute 对象，并添加基础信息
    /// </summary>
    /// <param name="attributeNode">Attribute 节点</param>
    /// <returns>生成的 Attribute 对象</returns>
    private static GDTF_AttributeDefinitionsData_Attribute CreateAttribute(XmlNode attributeNode)
    {
        GDTF_AttributeDefinitionsData_Attribute attribute = new GDTF_AttributeDefinitionsData_Attribute();

        attribute.attributeName = GetNodeAttribute(attributeNode, "Name");
        attribute.physicalUnit = (PhysicalUnit)Enum.Parse(typeof(PhysicalUnit), GetNodeAttribute(attributeNode, "PhysicalUnit"));
        string color = GetNodeAttribute(attributeNode, "Color");
        if (color != null)
        {
            attribute.color = XmlColorToColor(GetNodeAttribute(attributeNode, "Color"));
        }

        return attribute;
    }

    /// <summary>
    /// 查找当前节点下所有 FeatureGroup 并添加到列表中
    /// </summary>
    /// <param name="featureGroupsNode">FeatureGroups 节点</param>
    /// <param name="featureGroups">保存 FeatureGroup 的字典</param>
    private static void GetFeatureGroups(XmlNode featureGroupsNode, Dictionary<string, GDTF_AttributeDefinitionsData_FeatureGroup> featureGroups)
    {
        // 查找 FeatureGroup 节点
        foreach (XmlNode featureGroupNode in featureGroupsNode)
        {
            // 如果存在子节点
            if (featureGroupNode.HasChildNodes)
            {
                GDTF_AttributeDefinitionsData_FeatureGroup featureGroup = new GDTF_AttributeDefinitionsData_FeatureGroup();

                string featureGroupName = GetNodeAttribute(featureGroupNode, "Name");
                if (featureGroupName != null)
                {
                    featureGroup.featureGroupName = featureGroupName;
                    if (featureGroupNode.HasChildNodes)
                    {
                        // 在当前 FeatureGroup 下添加 Feature
                        GetFeatures(featureGroupNode, featureGroup);
                    }

                    featureGroups[featureGroupName] = featureGroup;
                }
            }
        }
    }

    /// <summary>
    /// 查找当前节点下所有 Feature 并添加到 FeatureGroup 对象中
    /// </summary>
    /// <param name="featureGroupNode">FeatureGroup 节点</param>
    /// <param name="featureGroup">FeatureGroup 对象</param>
    private static void GetFeatures(XmlNode featureGroupNode, GDTF_AttributeDefinitionsData_FeatureGroup featureGroup)
    {
        Dictionary<string, GDTF_AttributeDefinitionsData_Feature> features = new Dictionary<string, GDTF_AttributeDefinitionsData_Feature>();

        foreach (XmlNode featureNode in featureGroupNode)
        {
            string featureName = GetNodeAttribute(featureNode, "Name");
            if (featureName != null)
            {
                GDTF_AttributeDefinitionsData_Feature feature = new GDTF_AttributeDefinitionsData_Feature()
                {
                    fatureName = featureName
                };
                features[featureName] = feature;
            }
        }

        featureGroup.features = features;
    }

    /// <summary>
    /// 查找当前节点下的所有 ActivationGrop 并添加到列表中
    /// </summary>
    /// <param name="activationGroupsNode">ActivationGroups 节点</param>
    /// <param name="activationGroups">保存 ActivationGroup 的字典</param>
    private static void GetActivationGroups(XmlNode activationGroupsNode, Dictionary<string, GDTF_AttributeDefinitionsData_ActivationGroup> activationGroups)
    {
        // 遍历 ActivationGroups 节点下的子节点
        foreach (XmlNode activationGroupNode in activationGroupsNode)
        {
            string activationGroupName = GetNodeAttribute(activationGroupNode, "Name");
            if (activationGroupName != null)
            {
                GDTF_AttributeDefinitionsData_ActivationGroup activationGroup = new GDTF_AttributeDefinitionsData_ActivationGroup()
                {
                    activationGroupName = activationGroupName
                };
                activationGroups[activationGroupName] = activationGroup;
            }
        }
    }
    #endregion

    #region 获取 WheelsData 信息

    /// <summary>
    /// 获取灯具 Wheels 信息
    /// </summary>
    private static void GetWheelsData(GDTF_Data data)
    {
        if(wheelsNode == null)
        {
            Debug.Log("Not Found Wheels Node!");
            return;
        }

        // 如果节点下存在子节点
        if (wheelsNode.HasChildNodes)
        {
            foreach (XmlNode wheelNode in wheelsNode.ChildNodes)
            {

                if(wheelNode.Name != "Wheel")
                {
                    continue;
                }

                if (wheelNode.HasChildNodes)
                {
                    // 创建 GDTF_WheelsData 对象，并设置该组 Wheel 名称
                    GDTF_WheelsData wheelsData = new GDTF_WheelsData()
                    {
                        wheelName = GetNodeAttribute(wheelNode, "Name")
                    };

                    foreach (XmlNode slotNode in wheelNode.ChildNodes)
                    {
                        WheelSlot slotData = new WheelSlot();
                        slotData.name = GetNodeAttribute(slotNode, "Name");
                        slotData.color = XmlColorToColor(GetNodeAttribute(slotNode, "Color"));
                        slotData.mediaFileName = GetNodeAttribute(slotNode, "MediaFileName");

                        // 将该条 slot 信息添加
                        wheelsData.slots.Add(slotData);
                    }

                    data.wheels.Add(wheelsData);
                }
            }
        }
    }

    #endregion

    #region 获取 DMXModes 信息

    /// <summary>
    /// 获取灯具 DMX 模式
    /// </summary>
    private static void GetDmxModes(GDTF_Data data)
    {
        if (dmxModes == null)
        {
            Debug.Log("Not Found DmxModes Node!");
            return;
        }

        if (dmxModes.HasChildNodes)
        {
            // 获取所有 DMX 模式
            foreach(XmlNode dmxModeNode in dmxModes)
            {
                // 如果当前模式下包含子节点
                if (dmxModeNode.HasChildNodes)
                {

                    XmlNode dmxChannelsNode = FindChildNode(dmxModeNode, "DMXChannels");
                    // 查找 DMXChannels 节点
                    if (dmxChannelsNode != null)
                    {
                        // 创建 DMXModesData 对象
                        GDTF_DmxModesData modeData = new GDTF_DmxModesData()
                        {
                            dmxModeName = GetNodeAttribute(dmxModeNode, "Name"),
                            dmxModeGeometry = GetNodeAttribute(dmxModeNode, "Geometry")
                        };

                        // 为该模式添加通道信息
                        GetDmxModeChannels(dmxChannelsNode, modeData, data);
                        data.dmxModes.Add(modeData);
                    }
                }
            }
        }
    }

    /// <summary>
    ///  获取当前模式下 DMX 通道信息
    /// </summary>
    /// <param name="dmxChannelsNode">DMX Channels 节点</param>
    /// <param name="modeData">DMX Mode 对象</param>
    /// <param name="data">GDTF Data 对象</param>
    private static void GetDmxModeChannels(XmlNode dmxChannelsNode, GDTF_DmxModesData modeData, GDTF_Data data)
    {
        // 如果 DMXChannels 节点下存在 DMXChannel
        if (dmxChannelsNode.HasChildNodes)
        {
            foreach (XmlNode dmxChannel in dmxChannelsNode)
            {
                // 如果 DMXChannel 下存在子节点
                if (dmxChannel.HasChildNodes)
                {
                    // 如果当前 DMXChannel 下存在 LogicalChannel 节点
                    XmlNode logicalChannelNode = FindChildNode(dmxChannel, "LogicalChannel");
                    if(logicalChannelNode != null)
                    {
                        // 创建 DMXChannelData 对象
                        GDTF_DmxChannel channel = new GDTF_DmxChannel()
                        {
                            dmxBreak = Convert.ToInt32(GetNodeAttribute(dmxChannel, "DMXBreak")),
                            dmxBit = GetDmxValueResolution(GetNodeAttribute(dmxChannel, "Default")),
                            deafault = GetDmxValue(GetNodeAttribute(dmxChannel, "Default")),
                            geometry = GetNodeAttribute(dmxChannel, "Geometry"),
                            offset = XmlSplitToIntArray(GetNodeAttribute(dmxChannel, "Offset"))
                        };

                        // 为该 DMX Mode 添加 DMXChannel
                        modeData.channelsData.Add(channel);
                        GetDmxLogicalChannel(dmxChannel, channel, data);
                    }

                    
                }
            }
        }
    }

    /// <summary>
    /// 获取当前模式下 LogicalChannel 信息
    /// </summary>
    /// <param name="dmxChannelNode">DmxChannel 节点</param>
    /// <param name="channel">DMXChannel 对象</param>
    /// <param name="data">GDTF_Data 对象</param>
    private static void GetDmxLogicalChannel(XmlNode dmxChannelNode, GDTF_DmxChannel channel, GDTF_Data data)
    {
        // 如果 DMXChannel 节点下存在 LogicalChannel 节点
        if (dmxChannelNode.HasChildNodes)
        {
            XmlNode logicalChannelNode = dmxChannelNode.FirstChild;
            if(logicalChannelNode != null)
            {
                string attributeName = GetNodeAttribute(logicalChannelNode, "Attribute");

                GDTF_DmxLogicalChannel logicalChannel = new GDTF_DmxLogicalChannel
                {
                    attribute = data.attributeDefinitions.attributes[attributeName]
                };

                GetDmxChannelFuctions(logicalChannelNode, logicalChannel, data);
                channel.logicalChannel = logicalChannel;

            }

        }
    }

    /// <summary>
    /// 获取当前 DMX LogicalChannel 下的 ChannelFunction
    /// </summary>
    /// <param name="logicalChannelNode">LogicalChannel 节点</param>
    /// <param name="logicalChannel">DMXChannelData 对象</param>
    /// <param name="data">GDTF_Data 对象</param>
    private static void GetDmxChannelFuctions(XmlNode logicalChannelNode, GDTF_DmxLogicalChannel logicalChannel, GDTF_Data data)
    {
        // 如果 LogicalChannel 节点下存在子节点
        if (logicalChannelNode.HasChildNodes)
        {
            List<XmlNode> channelFunctionNodes = new List<XmlNode>();

            // 获取所有 ChannelFunction 节点
            foreach(XmlNode channelFunctionNode in logicalChannelNode)
            {
                // 只保存 ChannelFunction 节点
                if(channelFunctionNode.Name == "ChannelFunction")
                {
                    channelFunctionNodes.Add(channelFunctionNode);
                }
            }

            for(int i = 0; i < channelFunctionNodes.Count; i++)
            {
                GDTF_DmxChannelFunction channelFunction = new GDTF_DmxChannelFunction();

                int functionDmxFrom = GetDmxValue(GetNodeAttribute(channelFunctionNodes[i], "DMXFrom"));
                int functionDmxTo;

                if(i == channelFunctionNodes.Count - 1)
                {
                    functionDmxTo = (1 << ((int)GetDmxValueResolution(GetNodeAttribute(channelFunctionNodes[i], "DMXFrom")) * 8)) - 1;
                }
                else
                {
                    functionDmxTo = GetDmxValue(GetNodeAttribute(channelFunctionNodes[i + 1], "DMXFrom")) - 1;
                }

                channelFunction.functionName = GetNodeAttribute(channelFunctionNodes[i], "Name");
                string attributeName = GetNodeAttribute(channelFunctionNodes[i], "Attribute");
                if(attributeName != null)
                {
                    channelFunction.attribute = data.attributeDefinitions.attributes[attributeName];
                }
                channelFunction.functionDmxFrom = functionDmxFrom;
                channelFunction.functionDmxTo = functionDmxTo;
                channelFunction.functionPhysicalFrom = Convert.ToSingle(GetNodeAttribute(channelFunctionNodes[i], "PhysicalFrom"));
                channelFunction.functionPhysicalTo = Convert.ToSingle(GetNodeAttribute(channelFunctionNodes[i], "PhysicalTo"));
                channelFunction.wheelName = GetNodeAttribute(channelFunctionNodes[i], "Wheel");

                // 为该 ChannelFunction 添加 ChannelSet
                GetDmxChannelSets(channelFunctionNodes[i], channelFunction);

                // 为该 Channel 添加 ChannelFunction
                logicalChannel.channelFunctions.Add(channelFunction);
            }
        }
    }

    /// <summary>
    /// 获取当前 ChannelFunction 下的 ChannelSet
    /// </summary>
    /// <param name="channelFunctionNode">ChannelFunction 节点</param>
    /// <param name="channelFunction">GDTF_DmxChannelFunction 对象</param>
    private static void GetDmxChannelSets(XmlNode channelFunctionNode, GDTF_DmxChannelFunction channelFunction)
    {
        // 如果 ChannelFunction 节点下面存在子节点
        if (channelFunctionNode.HasChildNodes)
        {
            List<XmlNode> channelSetNodes = new List<XmlNode>();
            List<GDTF_DmxChannelSet> channelSets = new List<GDTF_DmxChannelSet>();

            foreach(XmlNode channelSetNode in channelFunctionNode)
            {
                // 只保存 ChannelSet 节点
                if(channelSetNode.Name == "ChannelSet")
                {
                    channelSetNodes.Add(channelSetNode);
                }
            }

            // 遍历整理后有效的 ChannelSet 节点
            for(int i = 0; i < channelSetNodes.Count; i++)
            {
                GDTF_DmxChannelSet channelSet = new GDTF_DmxChannelSet();
                
                // 获取预设的起始通道值
                int dmxFrom = GetDmxValue(GetNodeAttribute(channelSetNodes[i], "DMXFrom"));
                
                // 预设的结束通道值
                int dmxTo;

                // 如果但当前已经索引到最后一个 ChannelSet，那么 ChannelSet 的 dmxTo 为 Channel 的最大 DmxTo 值
                // 否则 ChannelSet 的 dmxTo 值为，下一个 ChannelSet 的 dmxFrom 值减一
                if (i ==  channelSetNodes.Count - 1)
                {
                    dmxTo = channelFunction.functionDmxTo;
                }
                else
                {
                    dmxTo = GetDmxValue(GetNodeAttribute(channelSetNodes[i + 1], "DMXFrom")) - 1;
                }

                // 如果当前节点的 Name 属性不为空就添加
                if(GetNodeAttribute(channelSetNodes[i], "Name") != string.Empty)
                {
                    channelSet.setName = GetNodeAttribute(channelSetNodes[i], "Name");
                    channelSet.setDmxForm = dmxFrom;
                    channelSet.setDmxTo = dmxTo;
                    channelSet.wheelSlotIndex = Convert.ToInt32(GetNodeAttribute(channelSetNodes[i], "WheelSlotIndex"));
                    channelSet.setName = GetNodeAttribute(channelSetNodes[i], "Name");

                    channelSets.Add(channelSet);
                }
            }

            channelFunction.channelSets = channelSets;
        }
    }

    #endregion

    #region 功能函数

    /// <summary>
    /// 将 XML 中的颜色信息转换成 string[]
    /// </summary>
    /// <param name="data">颜色信息</param>
    /// <param name="color">分割后的数组</param>
    private static void XmlColorToStringArray(string data, ref string[] color)
    {
        if(data == string.Empty)
        {
            color = null;
        }

        color = data.Split(',');
    }

    /// <summary>
    /// 将 XML 中的颜色信息转换成 Color 对象
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    private static Color XmlColorToColor(string data)
    {
        if(data == null)
        {
            return Color.black;
        }
        string[] stringArray = data.Split(',');

        return XYY2RGB(Convert.ToSingle(stringArray[0]), Convert.ToSingle(stringArray[1]), Convert.ToSingle(stringArray[2]));
    }

    /// <summary>
    /// 将以 逗号 分割的字符串解析为 int[]
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    private static int[] XmlSplitToIntArray(string data)
    {
        if(data == string.Empty)
        {
            return null;
        }

        List<int> value = new List<int>(5);
        
        foreach(var item in data.Split(','))
        {
            value.Add(int.Parse(item));
        }

        return value.ToArray();
    }

    /// <summary>
    /// 获取该节点的属性值
    /// </summary>
    /// <param name="node">节点</param>
    /// <param name="attributeName">属性名称</param>
    /// <returns>返回的值</returns>
    private static string GetNodeAttribute(XmlNode node, string attributeName)
    {
        if(node == null)
        {
            return null;
        }

        if(node.Attributes[attributeName] != null)
        {
            return node.Attributes[attributeName].Value;
        }
        else
        {
            return null;
        }
    }

    /// <summary>
    /// 获取 DMX 数值的分辨率
    /// </summary>
    /// <param name="data">字符串</param>
    /// <param name="bit">DMX 数值分辨率枚举</param>
    private static DmxBit GetDmxValueResolution(string data)
    {
        if(data == string.Empty)
        {
            return DmxBit.Bit8;
        }

        string[] bitValue = data.Split('/');
        int value = int.Parse(bitValue[1]);

        if(value > Enum.GetValues(typeof(DmxBit)).Length)
        {
            return DmxBit.Bit8;
        }

        return (DmxBit)value;
    }

    /// <summary>
    /// 获取 DMX 值
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    private static int GetDmxValue(string data)
    {
        if(data == string.Empty)
        {
            return 0;
        }

        string[] array = data.Split('/');

        int dmxValue = 0;
        if(int.TryParse(array[0], out dmxValue))
        {
            return dmxValue;
        }
        else
        {
            Debug.LogError("Can not Parse Value");
            return 0;
        }
    }

    /// <summary>
    /// 根据父节点查找第一个制定的子节点
    /// </summary>
    /// <param name="parent">父节点</param>
    /// <param name="childName">子节点名称</param>
    /// <returns>子节点</returns>
    private static XmlNode FindChildNode(XmlNode parent, string childName)
    {
        if(parent == null && childName == null)
        {
            return null;
        }

        foreach(XmlNode childNode in parent.ChildNodes)
        {
            if(childNode.Name == childName)
            {
                return childNode;
            }
        }

        return null;
    }

    /// <summary>
    /// 未知层级查找第一个指定名称的子节点
    /// </summary>
    /// <param name="parent">父节点</param>
    /// <param name="childName">子节点名称</param>
    /// <returns>子节点</returns>
    private static XmlNode FindeNode(XmlNode parent, string childName)
    {
        if (parent == null && childName == null)
        {
            return null;
        }

        // 先遍历一变子节点如果找到返回
        foreach(XmlNode childNode in parent.ChildNodes)
        {
            if(childNode.Name == childName)
            {
                return childNode;
            }
        }

        XmlNode node = null;

        foreach(XmlNode childNode in parent.ChildNodes)
        {
            node = FindeNode(childNode, childName);
            if(node != null)
            {
                return node;
            }
        }

        return null;
    }

    /// <summary>
    /// 将 CIE xyY 颜色转成 RGB
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="Y"></param>
    /// <returns></returns>
    private static Color XYY2RGB(float x, float y, float Y)
    {
        float dx = x * Y / y;
        float dy = Y;
        float dz = ((1 - x - y) * Y) / y;

        return XYZ2RGB(dx, dy, dz);
    }

    /// <summary>
    /// 将 CIE XYZ 颜色转成 RGB
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    /// <returns></returns>
    private static Color XYZ2RGB(float x, float y, float z)
    {
        // 将 CIE XYZ 转换成 D65 sRGB
        double dr, dg, db;
        dr = 3.2404542 * x - 1.5371385 * y - 0.4985314 * z;
        dg = -0.9692660 * x + 1.8760108 * y + 0.0415560 * z;
        db = 0.0556434 * x - 0.2040259 * y + 1.0572252 * z;

        // 找出 RGB 中最大值
        double max = 0;
        max = dr > dg ? dr : dg;
        max = max > db ? max : db;

        dr = dr / max;
        dg = dg / max;
        db = db / max;

        dr = dr > 0 ? dr : 0;
        dg = dg > 0 ? dg : 0;
        db = db > 0 ? db : 0;

        dr = dr > 1 ? 1 : dr;
        dg = dg > 1 ? 1 : dg;
        db = db > 1 ? 1 : db;

        return new Color((float)dr, (float)dg, (float)db);

    }

    #endregion
}
