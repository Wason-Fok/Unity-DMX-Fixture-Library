using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;

namespace Unity.QuickSearch
{
    internal class FilterWindow : EditorWindow
    {
        private static class Styles
        {
            public static Vector2 windowSize = new Vector2(270, 250);
            public static readonly GUIStyle filterHeader = new GUIStyle(EditorStyles.boldLabel)
            {
                name = "quick-search-filter-header",
                margin = new RectOffset(4, 4, 3, 2)
            };

            public static readonly GUIContent prefButtonContent = new GUIContent(Icons.settings, "Open quick search preferences...");
            public static readonly GUIStyle prefButton = new GUIStyle("IconButton")
            {
                #if UNITY_2019_3_OR_NEWER
                fixedWidth = 16, fixedHeight = 16, 
                margin = new RectOffset(2, 2, 2, 2)
                #else
                fixedWidth = 20, fixedHeight = 20, 
                margin = new RectOffset(2, 2, 2, 0)
                #endif
            };

            public static readonly GUIStyle filterTimeLabel = new GUIStyle(EditorStyles.miniLabel)
            {
                name = "quick-search-filter-time-label",
                fixedWidth = 50,
                alignment = TextAnchor.MiddleRight,
                #if UNITY_2019_1_OR_NEWER
                margin = new RectOffset(0, 0, 1, 1),
                #else
                margin = new RectOffset(0, 0, 2, 1),
                #endif
                fontSize = Math.Max(filterHeader.fontSize - 2, 9),
                fontStyle = FontStyle.Italic,
                normal = new GUIStyleState { textColor = EditorStyles.helpBox.normal.textColor }
            };

            public static readonly GUIStyle filterTimeLongLabel = new GUIStyle(filterTimeLabel)
            {
                name = "quick-search-filter-time-long-label",
                normal = new GUIStyleState() { textColor = Color.red }
            };

            public static readonly GUIStyle filterToggle = new GUIStyle("Toggle") { margin = new RectOffset(4, 4, 2, 1) };
            public static readonly GUIStyle headerFilterToggle = new GUIStyle(filterToggle) { margin = new RectOffset(4, 4, 3, 1) };

            public static readonly GUIStyle filterEntry = new GUIStyle(EditorStyles.label) { name = "quick-search-filter-entry" };
            public static readonly GUIStyle panelBorder = new GUIStyle("grey_border") { name = "quick-search-filter-panel-border" };
            public static readonly GUIStyle filterExpanded = new GUIStyle("IN Foldout")
            {
                margin = new RectOffset(2, 1, 2, 0)
            };
            public static readonly GUIStyle separator = new GUIStyle("sv_iconselector_sep")
            {
                #if UNITY_2019_3_OR_NEWER
                margin = new RectOffset(1, 1, 4, 0)
                #else
                margin = new RectOffset(1, 1, 0, 0)
                #endif
            };

            public static float foldoutIndent = filterExpanded.fixedWidth + 6;
        }

        public ISearchView searchView;

        private Vector2 m_ScrollPos;
        private List<SearchFilter.ProviderDesc> initialProviders;
        private int m_ToggleFilterFocusIndex = 1;
        private int m_ToggleFilterNextIndex = 0;
        private int m_ToggleFilterCount = 0;
        private int m_ExpandToggleIndex = -1;

        internal static double s_CloseTime;
        internal static bool canShow
        {
            get
            {
                if (EditorApplication.timeSinceStartup - s_CloseTime < 0.250)
                    return false;
                return true;
            }
        }

        public static bool ShowAtPosition(ISearchView quickSearchTool, Rect rect)
        {
            var screenPos = GUIUtility.GUIToScreenPoint(new Vector2(rect.x, rect.y));
            var screenRect = new Rect(screenPos, rect.size);
            var filterWindow = ScriptableObject.CreateInstance<FilterWindow>();
            filterWindow.searchView = quickSearchTool;
            filterWindow.ShowAsDropDown(screenRect, Styles.windowSize);
            return true;
        }

        [UsedImplicitly]
        internal void OnEnable()
        {
            if (SearchService.Filter.allActive)
                initialProviders = SearchService.Filter.providerFilters.ToList();
            else
                initialProviders = SearchService.Filter.providerFilters.Where(p => p.name.isEnabled).ToList();
        }

        [UsedImplicitly]
        internal void OnDestroy()
        {
            s_CloseTime = EditorApplication.timeSinceStartup;
            if (SearchService.Filter.providerFilters.All(desc => !desc.name.isEnabled))
            {
                Debug.LogWarning("All filters are disabled. Loading last used filters.");
                SearchService.LoadGlobalSettings();
            }
        }

        [UsedImplicitly]
        internal void OnGUI()
        {
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                Close();
                if (searchView != null)
                    searchView.Focus();
                return;
            }

            HandleKeyboardNavigation();

            m_ToggleFilterNextIndex = 0;

            GUI.Box(new Rect(0, 0, position.width, position.height), GUIContent.none, Styles.panelBorder);
            DrawHeader();
            GUILayout.Label(GUIContent.none, Styles.separator);

            m_ScrollPos = GUILayout.BeginScrollView(m_ScrollPos);
             
            foreach (var providerDesc in initialProviders.OrderBy(f => f.priority))
            {
                DrawSectionHeader(providerDesc);
                if (providerDesc.isExpanded)
                    DrawSubCategories(providerDesc);
            }

            m_ToggleFilterCount = m_ToggleFilterNextIndex;

            GUILayout.Space(10);
            DrawExplicitProviders();

            GUILayout.EndScrollView();
            GUILayout.Space(1);
        }

        private void HandleKeyboardNavigation()
        {
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.UpArrow)
            {

                m_ToggleFilterFocusIndex = Math.Max(0, m_ToggleFilterFocusIndex-1);
                Event.current.Use();
            }
            else if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.DownArrow)
            {
                m_ToggleFilterFocusIndex = Math.Min(m_ToggleFilterFocusIndex+1, m_ToggleFilterCount-1);
                Event.current.Use();
            }
            else if (Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.LeftArrow || Event.current.keyCode == KeyCode.RightArrow))
            {
                m_ExpandToggleIndex = m_ToggleFilterFocusIndex;
                Event.current.Use();
            }

            GUI.FocusControl($"Box_{m_ToggleFilterFocusIndex}");
        }

        private static void DrawExplicitProviders()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent("Special Search Providers", null, "Providers only available if specified explicitly"), Styles.filterHeader);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Label(GUIContent.none, Styles.separator);

            foreach (var provider in SearchService.Providers.Where(p => p.active && p.isExplicitProvider).OrderBy(p => p.priority))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(Styles.foldoutIndent);
                GUILayout.Label(GetProviderLabelContent(provider), Styles.filterHeader);
                GUILayout.EndHorizontal();
            }
        }

        private void DrawHeader()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Regular Search Providers", Styles.filterHeader);
            if (GUILayout.Button(Styles.prefButtonContent, Styles.prefButton))
                SettingsService.OpenUserPreferences(SearchSettings.settingsPreferencesKey);
            GUILayout.FlexibleSpace();
            EditorGUI.BeginChangeCheck();
            GUI.SetNextControlName($"Box_{m_ToggleFilterNextIndex++}");
            bool isEnabled = GUILayout.Toggle(SearchService.Filter.providerFilters.All(p => p.name.isEnabled), "", Styles.headerFilterToggle, GUILayout.ExpandWidth(false));
            if (EditorGUI.EndChangeCheck())
            {
                foreach (var provider in SearchService.Filter.providerFilters)
                {
                    SearchService.Filter.SetFilter(isEnabled, provider.name.id);
                }
                searchView.Refresh();
            }

            GUILayout.EndHorizontal();
        }

        private void DrawSectionHeader(SearchFilter.ProviderDesc desc)
        {
            GUILayout.BeginHorizontal();

            if (desc.categories.Count > 0)
            {
                EditorGUI.BeginChangeCheck();
                if (m_ExpandToggleIndex == m_ToggleFilterNextIndex && 
                    (Event.current.type == EventType.Repaint || Event.current.type == EventType.Layout))
                {
                    desc.isExpanded = !desc.isExpanded;
                    GUI.changed = true;
                    m_ExpandToggleIndex = -1;
                }
                bool isExpanded = GUILayout.Toggle(desc.isExpanded, "", Styles.filterExpanded);
                if (EditorGUI.EndChangeCheck())
                {
                    SearchService.Filter.SetExpanded(isExpanded, desc.name.id);
                }
            }
            else
            {
                GUILayout.Space(Styles.foldoutIndent);
            }

            GUILayout.Label(GetProviderLabelContent(desc.provider, desc.name.displayName), Styles.filterHeader);
            GUILayout.FlexibleSpace();
            if (desc.provider != null)
            {
                var avgTime = desc.provider.avgTime;
                var loadTime = desc.provider.loadTime;
                var enableTime = desc.provider.enableTime;
                if (avgTime > 0.99 || loadTime > 9.99 || enableTime > 9.99)
                {
                    GUIContent content = new GUIContent(avgTime.ToString("0.#") + " ms", 
                                                        $"Initialization took {loadTime.ToString("0.#")} ms\r\n" +
                                                        $"Activation took {enableTime.ToString("0.#")} ms");
                    GUILayout.Label(content, avgTime < 25.0 ? Styles.filterTimeLabel : Styles.filterTimeLongLabel);
                }
            }

            EditorGUI.BeginChangeCheck();
            GUI.SetNextControlName($"Box_{m_ToggleFilterNextIndex++}");
            bool isEnabled = GUILayout.Toggle(desc.name.isEnabled, "", Styles.filterToggle, GUILayout.ExpandWidth(false));
            if (EditorGUI.EndChangeCheck())
            {
                SearchService.Filter.SetFilter(isEnabled, desc.name.id);
                searchView.Refresh();
            }

            GUILayout.EndHorizontal();
        }

        private static GUIContent GetProviderLabelContent(SearchProvider provider, string displayName = null)
        {
            if (displayName == null)
                displayName = SearchFilter.GetProviderNameWithFilter(provider);

            string tooltip = null;
            if (provider.filterId != null)
            {
                tooltip = $"Type \"{provider.filterId}\" to search ONLY for {provider.name.displayName}";
            }
            return new GUIContent(displayName, null, tooltip);
        }

        private void DrawSubCategories(SearchFilter.ProviderDesc desc)
        {
            foreach (var cat in desc.categories)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(Styles.foldoutIndent + 5);
                GUILayout.Label(cat.displayName, Styles.filterEntry);
                GUILayout.FlexibleSpace();

                EditorGUI.BeginChangeCheck();
                GUI.SetNextControlName($"Box_{m_ToggleFilterNextIndex++}");
                bool isEnabled = GUILayout.Toggle(cat.isEnabled, "", Styles.filterToggle);
                if (EditorGUI.EndChangeCheck())
                {
                    SearchService.Filter.SetFilter(isEnabled, desc.name.id, cat.id);
                    searchView.Refresh();
                }

                GUILayout.EndHorizontal();
            }
        }
    }
}