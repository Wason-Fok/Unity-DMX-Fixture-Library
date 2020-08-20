using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class MapRangeClamp : MonoBehaviour
{
    public float value;
    public float inFrom;
    public float inEnd;
    public float outFrom;
    public float outEnd;

    public float current;
    
    // Update is called once per frame
    void Update()
    {
        current = MapRangeClamp1(value, inFrom, inEnd, outFrom, outEnd);
    }

    private float MapRangeClamp1(float value, float inFrom, float inEnd, float outFrom, float outEnd)
    {
        value = Mathf.Clamp(value, inFrom, inEnd);

        float inLength = Mathf.Abs(inEnd - inFrom);
        float lengthValueToFrom = Mathf.Abs(value - inFrom);
        float curPercent = lengthValueToFrom / inLength;

        return (outFrom + (outEnd - outFrom) * curPercent);
    }
}
