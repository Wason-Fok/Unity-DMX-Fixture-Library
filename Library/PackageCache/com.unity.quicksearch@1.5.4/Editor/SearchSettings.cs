using System.Linq;
using JetBrains.Annotations;
using Unity.QuickSearch.Providers;
using UnityEditor;
using UnityEngine;

namespace Unity.QuickSearch
{
    internal static class SearchSettings
    {
        private const string k_KeyPrefix = "quicksearch";

        public const string settingsPreferencesKey = "Preferences/Quick Search";
        public static bool trackSelection { get; private set; }
        public static bool fetchPreview { get; private set; }

        static SearchSettings()
        {
            trackSelection = EditorPrefs.GetBool($"{k_KeyPrefix}.{nameof(trackSelection)}", true);
            fetchPreview = EditorPrefs.GetBool($"{k_KeyPrefix}.{nameof(fetchPreview)}", true);
        }

        private static void Save()
        {
            EditorPrefs.SetBool($"{k_KeyPrefix}.{nameof(trackSelection)}", trackSelection);
            EditorPrefs.SetBool($"{k_KeyPrefix}.{nameof(fetchPreview)}", fetchPreview);
        }

        [UsedImplicitly, SettingsProvider]
        private static SettingsProvider CreateSearchSettings()
        {
            var settings = new SettingsProvider(settingsPreferencesKey, SettingsScope.User)
            {
                keywords = new[] { "quick", "omni", "search" },
                guiHandler = searchContext =>
                {
                    EditorGUIUtility.labelWidth = 500;
                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Space(10);
                        GUILayout.BeginVertical();
                        {
                            GUILayout.Space(10);
                            EditorGUI.BeginChangeCheck();
                            {
                                trackSelection = EditorGUILayout.Toggle(Styles.trackSelectionContent, trackSelection);
                                fetchPreview = EditorGUILayout.Toggle(Styles.fetchPreviewContent, fetchPreview);
                                GUILayout.Space(10);
                                DrawProviderSettings();
                            }
                            if (EditorGUI.EndChangeCheck())
                            {
                                Save();
                                SearchService.Refresh();
                            }
                        }
                        GUILayout.EndVertical();
                    }
                    GUILayout.EndHorizontal();
                }
            };
            return settings;
        }

        private static void DrawProviderSettings()
        {
            EditorGUILayout.LabelField("Provider Settings", EditorStyles.largeLabel);
            foreach (var p in SearchService.OrderedProviders)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(20);

                var wasActive = p.active;
                p.active = GUILayout.Toggle(wasActive, Styles.toggleActiveContent);
                if (p.active != wasActive)
                    EditorPrefs.SetBool($"{k_KeyPrefix}.{p.name.id}.active", p.active);

                using (new EditorGUI.DisabledGroupScope(!p.active))
                {
                    GUILayout.Label(new GUIContent(p.name.displayName, $"{p.name.id} ({p.priority})"), GUILayout.Width(175));
                }

                if (!p.isExplicitProvider)
                {
                    if (GUILayout.Button(Styles.increasePriorityContent, Styles.priorityButton))
                        LowerProviderPriority(p);
                    if (GUILayout.Button(Styles.decreasePriorityContent, Styles.priorityButton))
                        UpperProviderPriority(p);
                }
                else
                {
                    GUILayoutUtility.GetRect(Styles.increasePriorityContent, Styles.priorityButton);
                    GUILayoutUtility.GetRect(Styles.increasePriorityContent, Styles.priorityButton);
                }

                GUILayout.Space(20);

                using (new EditorGUI.DisabledScope(p.actions.Count < 2))
                {
                    EditorGUI.BeginChangeCheck();
                    var items = p.actions.Select(a => new GUIContent(a.DisplayName, a.content.image,
                        p.actions.Count == 1 ?
                        $"Default action for {p.name.displayName} (Enter)" :
                        $"Set default action for {p.name.displayName} (Enter)")).ToArray();
                    var newDefaultAction = EditorGUILayout.Popup(0, items, GUILayout.ExpandWidth(true));
                    if (EditorGUI.EndChangeCheck())
                    {
                        SearchService.SetDefaultAction(p.name.id, p.actions[newDefaultAction].Id);
                        GUI.changed = true;
                    }
                    GUILayout.Space(10);
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();
            GUILayout.Space(20);
            if (GUILayout.Button(Styles.resetPrioritiesContent, GUILayout.MaxWidth(100)))
                ResetProviderPriorities();
            GUILayout.EndHorizontal();
        }

        private static void ResetProviderPriorities()
        {
            foreach (var p in SearchService.Providers)
            {
                EditorPrefs.DeleteKey($"{k_KeyPrefix}.{p.name.id}.active");
                EditorPrefs.DeleteKey($"{k_KeyPrefix}.{p.name.id}.priority");
            }
        }

        private static void LowerProviderPriority(SearchProvider provider)
        {
            var sortedProviderList = SearchService.Providers.Where(p => !p.isExplicitProvider).OrderBy(p => p.priority).ToList();
            for (int i = 1, end = sortedProviderList.Count; i < end; ++i)
            {
                var cp = sortedProviderList[i];
                if (cp != provider)
                    continue;

                var adj = sortedProviderList[i-1];
                var temp = provider.priority;
                if (cp.priority == adj.priority)
                    temp++;

                provider.priority = adj.priority;
                adj.priority = temp;

                EditorPrefs.SetInt($"{k_KeyPrefix}.{adj.name.id}.priority", adj.priority);
                EditorPrefs.SetInt($"{k_KeyPrefix}.{provider.name.id}.priority", provider.priority);
                break;
            }
        }

        private static void UpperProviderPriority(SearchProvider provider)
        {
            var sortedProviderList = SearchService.Providers.Where(p => !p.isExplicitProvider).OrderBy(p => p.priority).ToList();
            for (int i = 0, end = sortedProviderList.Count-1; i < end; ++i)
            {
                var cp = sortedProviderList[i];
                if (cp != provider)
                    continue;

                var adj = sortedProviderList[i+1];
                var temp = provider.priority;
                if (cp.priority == adj.priority)
                    temp--;

                provider.priority = adj.priority;
                adj.priority = temp;

                EditorPrefs.SetInt($"{k_KeyPrefix}.{adj.name.id}.priority", adj.priority);
                EditorPrefs.SetInt($"{k_KeyPrefix}.{provider.name.id}.priority", provider.priority);
                break;
            }
        }

        static class Styles
        {
            public static GUIStyle priorityButton = new GUIStyle("Button")
            {
                fixedHeight = 20,
                fixedWidth = 20,
                #if UNITY_2019_3_OR_NEWER
                fontSize = 14,
                padding = new RectOffset(0, 0, 0, 4),
                #else
                fontSize = 16,
                padding = new RectOffset(0, 0, 0, 2),
                #endif
                margin = new RectOffset(1, 1, 1, 1),
                alignment = TextAnchor.MiddleCenter,
                richText = true
            };

            public static GUIContent toggleActiveContent = new GUIContent("", "Enable or disable this provider. Disabled search provider will be completely ignored by the search service.");
            public static GUIContent resetPrioritiesContent = new GUIContent("Reset Priorities", "All search providers will restore their initial priority");
            public static GUIContent increasePriorityContent = new GUIContent("\u2191", "Increase the provider's priority");
            public static GUIContent decreasePriorityContent = new GUIContent("\u2193", "Decrease the provider's priority");

            public static GUIContent useDockableWindowContent = new GUIContent("Use a dockable window (instead of a modal popup window, not recommended)");
            public static GUIContent closeWindowByDefaultContent = new GUIContent("Automatically close the window when an action is executed");
            public static GUIContent useFilePathIndexerContent = new GUIContent(
                "Enable fast indexing of file system entries under your project (experimental)",
                "This indexing system takes around 1 and 10 seconds to build the first time you launch the quick search window. " +
                "It can take up to 30-40 mb of memory, but it provides very fast search for large projects. " +
                "Note that if you want to use standard asset database filtering, you will need to rely on `t:`, `a:`, etc.");
            public static GUIContent trackSelectionContent = new GUIContent(
                "Track the current selection in the quick search",
                "Tracking the current selection can alter other window state, such as pinging the project browser or the scene hierarchy window.");
            public static GUIContent fetchPreviewContent = new GUIContent(
                "Generate an asset preview thumbnail for found items",
                "Fetching the preview of the items can consume more memory and make searches within very large project slower.");
            public static GUIContent useUberIndexingContent = new GUIContent(
                "Index all asset properties (Experimental and consume more resources)",
                "This new indexer, indexes all asset properties, such as dependencies (i.e. dep:door), components (i.e. has:audio), size (i.e. size>=1000), etc.");
            public static GUIContent rebuildIndexButtonContent = new GUIContent("Rebuild index");
        }
    }
}