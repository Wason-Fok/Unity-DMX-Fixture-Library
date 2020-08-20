using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GDTF 数据信息
/// </summary>
public class GDTF_Data
{
    public GDTF_FixtureTypeData fixtureType = new GDTF_FixtureTypeData();

    public GDTF_AttributeDefinitionsData attributeDefinitions = new GDTF_AttributeDefinitionsData();

    public List<GDTF_WheelsData> wheels = new List<GDTF_WheelsData>();

    public List<GDTF_DmxModesData> dmxModes = new List<GDTF_DmxModesData>();
}
