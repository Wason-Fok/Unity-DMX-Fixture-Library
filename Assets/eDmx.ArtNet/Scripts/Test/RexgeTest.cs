using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

[ExecuteInEditMode]
public class RexgeTest : MonoBehaviour
{
    public string text = "Color1";
    public string regexString = @"Color^[0-9]*$";

    public bool result = false;

    void Update()
    {
        result = Regex.IsMatch(text, regexString);
    }
}
