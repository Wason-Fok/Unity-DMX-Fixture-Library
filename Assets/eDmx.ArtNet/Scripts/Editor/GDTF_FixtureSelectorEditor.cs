using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CanEditMultipleObjects]
[CustomEditor(typeof(GDTF_FixtureSelector))]
public class GDTF_FixtureSelectorEditor : Editor
{
    List<string> fixtureNames = new List<string>();

    /// <summary>
    /// 灯具名称
    /// </summary>
    SerializedProperty fixtureName;
    /// <summary>
    /// 灯具 DMX 模式名称
    /// </summary>
    SerializedProperty dmxModeName;
    /// <summary>
    /// 灯具选择器对象
    /// </summary>
    GDTF_FixtureSelector targetObj;

    private void OnEnable()
    {
        fixtureName = serializedObject.FindProperty("fixtureName");
        dmxModeName = serializedObject.FindProperty("dmxModeName");
        targetObj = serializedObject.targetObject as GDTF_FixtureSelector;

        // 获取所有灯具名称并保存到列表中
        fixtureNames = GDTF_ResourcesLoader.GetFixtures().Keys.ToList();
    }

    /// <summary>
    /// 显示灯具选择下拉菜单
    /// </summary>
    /// <param name="rect"></param>
    void ShowFixturesDropDown(Rect rect)
    {
        var menu = new GenericMenu();

        if(fixtureNames.Count > 0)
        {
            foreach(var name in fixtureNames)
            {
                menu.AddItem(new GUIContent(name), false, OnSelectLibrary, name);
            }
        }
        else
        {
            menu.AddItem(new GUIContent("Not Found Libraries"), false, null);
        }

        menu.DropDown(rect);
    }

    /// <summary>
    /// 显示灯库选择下拉菜单
    /// </summary>
    /// <param name="rect"></param>
    void ShowDmxModeDropDown(Rect rect)
    {
        var menu = new GenericMenu();
        if(targetObj.descriptionData != null)
        {
            if (targetObj.descriptionData.dmxModes.Count > 0 && targetObj.descriptionData.dmxModes != null)
            {
                foreach (var dmxMode in targetObj.descriptionData.dmxModes)
                {
                    menu.AddItem(new GUIContent(dmxMode.dmxModeName), false, OnSelectDmxMode, dmxMode.dmxModeName);
                }
            }
        }
        else
        {
            menu.AddItem(new GUIContent("Not Found DmxModes"), false, null);
        }

        menu.DropDown(rect);
        
    }

    private void OnSelectLibrary(object name)
    {
        targetObj.gdtfFileName = (string)name;
        targetObj.dmxModeName = string.Empty;
        targetObj.LoadConfig();
    }

    private void OnSelectDmxMode(object name)
    {
        targetObj.dmxModeName = (string)name;
        targetObj.LoadConfig();
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        DropdownButtonText();

        var libraryDropdownRect = EditorGUILayout.GetControlRect(false, GUILayout.Width(200));
        if(EditorGUI.DropdownButton(libraryDropdownRect, libraryDropdownButtonContent, FocusType.Keyboard))
        {
            ShowFixturesDropDown(libraryDropdownRect);
        }

        var dmxModeDropdownRect = EditorGUILayout.GetControlRect(false, GUILayout.Width(200));
        if (EditorGUI.DropdownButton(dmxModeDropdownRect, dmxModeDropdownButtonContent, FocusType.Keyboard))
        {
            ShowDmxModeDropDown(dmxModeDropdownRect);
        }
    }

    private static GUIContent libraryDropdownButtonContent = new GUIContent();
    private static GUIContent dmxModeDropdownButtonContent = new GUIContent();
    private void DropdownButtonText()
    {
        if(targetObj.gdtfFileName == null || targetObj.gdtfFileName == string.Empty)
        {
            libraryDropdownButtonContent.text = "Select Library";
        }
        else
        {
            libraryDropdownButtonContent.text = targetObj.gdtfFileName;
        }

        if(targetObj.dmxModeName == null || targetObj.dmxModeName == string.Empty)
        {
            dmxModeDropdownButtonContent.text = "Select DmxMode";
        }
        else
        {
            dmxModeDropdownButtonContent.text = targetObj.dmxModeName;
        }
    }
}
