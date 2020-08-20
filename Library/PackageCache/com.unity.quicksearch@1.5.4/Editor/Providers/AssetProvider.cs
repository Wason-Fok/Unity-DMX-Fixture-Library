using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.QuickSearch.Providers
{
    /* Filters:
        t:<type>
        l:<label>
        ref[:id]:path
        v:<versionState>
        s:<softLockState>
        a:<area> [assets, packages]
     */
    [UsedImplicitly]
    static class AssetProvider
    {
        private const string type = "asset";
        private const string displayName = "Asset";

        private static FileSearchIndexer fileIndexer;
        private static readonly char[] k_InvalidIndexedChars = { '*', ':' };

        private static readonly string[] baseTypeFilters = new[]
        {
            "Folder", "DefaultAsset", "AnimationClip", "AudioClip", "AudioMixer", "ComputeShader", "Font", "GUISKin", "Material", "Mesh", 
            "Model", "PhysicMaterial", "Prefab", "Scene", "Script", "ScriptableObject", "Shader", "Sprite", "StyleSheet", "Texture", "VideoClip"
        };

        #if UNITY_2019_2_OR_NEWER
        private static readonly string[] typeFilter = baseTypeFilters.Concat(TypeCache.GetTypesDerivedFrom<ScriptableObject>()
                                                                                      .Where(t => !t.IsSubclassOf(typeof(Editor)))
                                                                                      .Select(t => t.Name)
                                                                                      .Distinct()
                                                                                      .OrderBy(n => n)).ToArray();
        #else
        private static readonly string[] typeFilter = baseTypeFilters;
        #endif

        private static readonly char[] k_InvalidSearchFileChars = Path.GetInvalidFileNameChars().Where(c => c != '*').ToArray();

        [UsedImplicitly, SearchItemProvider]
        internal static SearchProvider CreateProvider()
        {
            return new SearchProvider(type, displayName)
            {
                priority = 25,
                filterId = "p:",
                showDetails = SearchSettings.fetchPreview,
                
                subCategories = new List<NameEntry>()
                {
                    new NameEntry("guid", "guid"),
                    new NameEntry("packages", "packages")
                },

                onEnable = () =>
                {
                    if (fileIndexer == null)
                    {
                        var packageRoots = Utils.GetPackagesPaths().Select(p => new SearchIndexerRoot(Path.GetFullPath(p).Replace('\\', '/'), p));
                        var roots = new[] { new SearchIndexerRoot(Application.dataPath, "Assets") }.Concat(packageRoots);
                        fileIndexer = new FileSearchIndexer(type, roots);
                        fileIndexer.Build();
                    }
                },

                isEnabledForContextualSearch = () => QuickSearch.IsFocusedWindowTypeName("ProjectBrowser"),

                fetchItems = (context, items, _provider) => SearchAssets(context, items, _provider),

                fetchKeywords = (context, lastToken, items) =>
                {
                    if (!lastToken.StartsWith("t:"))
                        return;
                    items.AddRange(typeFilter.Select(t => "t:" + t));
                },

                fetchDescription = (item, context) => (item.description = GetAssetDescription(item.id)),
                fetchThumbnail = (item, context) => Utils.GetAssetThumbnailFromPath(item.id),
                fetchPreview = (item, context, size, options) => Utils.GetAssetPreviewFromPath(item.id, size, options),

                startDrag = (item, context) =>
                {
                    var obj = AssetDatabase.LoadAssetAtPath<Object>(item.id);
                    if (obj)
                        Utils.StartDrag(obj, item.label);
                },
                trackSelection = (item, context) => Utils.PingAsset(item.id)
            };
        }

        private static IEnumerator SearchAssets(SearchContext context, List<SearchItem> items, SearchProvider provider)
        {
            var searchQuery = context.searchQuery;
            var searchGuids = context.categories.Any(c => c.id == "guid" && c.isEnabled);
            var searchPackages = context.categories.Any(c => c.id == "packages" && c.isEnabled);

            // Search by GUIDs
            if (searchGuids)
            {
                var gui2Path = AssetDatabase.GUIDToAssetPath(searchQuery);
                if (!String.IsNullOrEmpty(gui2Path))
                    yield return provider.CreateItem(gui2Path, -1, $"{Path.GetFileName(gui2Path)} ({searchQuery})", null, null, null);
            }

            var fileIndexerReady = fileIndexer.IsReady();
            if (fileIndexerReady)
            {
                if (searchQuery.IndexOfAny(k_InvalidIndexedChars) == -1)
                {
                    foreach (var item in SearchFileIndex(searchQuery, searchPackages, provider))
                        yield return item;
                    if (!context.wantsMore)
                        yield break;
                }
            }

            if (!searchPackages)
            {
                if (!searchQuery.Contains("a:assets"))
                    searchQuery = "a:assets " + searchQuery;
            }

            foreach (var assetEntry in AssetDatabase.FindAssets(searchQuery).Select(AssetDatabase.GUIDToAssetPath).Select(path => provider.CreateItem(path, Path.GetFileName(path))))
                yield return assetEntry;

            if (!fileIndexerReady)
            {
                // Indicate to the user that we are still building the index.
                while (!fileIndexer.IsReady())
                    yield return null;

                foreach (var item in SearchFileIndex(searchQuery, searchPackages, provider))
                    yield return item;
            }

            // Search file system wildcards
            if (context.searchQuery.Contains('*'))
            {
                var safeFilter = string.Join("_", context.searchQuery.Split(k_InvalidSearchFileChars));
                var projectFiles = System.IO.Directory.EnumerateFiles(Application.dataPath, safeFilter, System.IO.SearchOption.AllDirectories);
                projectFiles = projectFiles.Select(path => path.Replace(Application.dataPath, "Assets").Replace("\\", "/"));
                foreach (var fileEntry in projectFiles.Select(path => provider.CreateItem(path, Path.GetFileName(path))))
                    yield return fileEntry;
            }
        }

        private static IEnumerable<SearchItem> SearchFileIndex(string filter, bool searchPackages, SearchProvider provider)
        {
            UnityEngine.Assertions.Assert.IsNotNull(fileIndexer);

            return fileIndexer.Search(filter, searchPackages ? int.MaxValue : 100).Select(e =>
            {
                var filename = Path.GetFileName(e.path);
                var filenameNoExt = Path.GetFileNameWithoutExtension(e.path);
                var itemScore = e.score;
                if (filenameNoExt.Equals(filter, StringComparison.OrdinalIgnoreCase))
                    itemScore = SearchProvider.k_RecentUserScore + 1;
                return provider.CreateItem(e.path, itemScore, filename, null, null, null);
            });
        }

        internal static string GetAssetDescription(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
                return assetPath;
            var fi = new FileInfo(assetPath);
            if (!fi.Exists)
                return "File does not exist anymore.";
            var fileSize = new FileInfo(assetPath).Length;
            return $"{assetPath} ({EditorUtility.FormatBytes(fileSize)})";
        }

        [UsedImplicitly, SearchActionsProvider]
        internal static IEnumerable<SearchAction> CreateActionHandlers()
        {
            #if UNITY_EDITOR_OSX
            const string k_RevealActionLabel = "Reveal in Finder...";
            #else
            const string k_RevealActionLabel = "Show in Explorer...";
            #endif

            return new[]
            {
                new SearchAction(type, "select", null, "Select asset...")
                {
                    handler = (item, context) => Utils.FrameAssetFromPath(item.id)
                },
                new SearchAction(type, "open", null, "Open asset...")
                {
                    handler = (item, context) =>
                    {
                        var asset = AssetDatabase.LoadAssetAtPath<Object>(item.id);
                        if (asset != null) AssetDatabase.OpenAsset(asset);
                    }
                },
                new SearchAction(type, "reveal", null, k_RevealActionLabel)
                {
                    handler = (item, context) => EditorUtility.RevealInFinder(item.id)
                },
                new SearchAction(type, "context", null, "Context")
                {
                    handler = (item, context) =>
                    {
                        var asset = AssetDatabase.LoadAssetAtPath<Object>(item.id);
                        if (asset != null)
                        {
                            var old = Selection.activeObject;
                            Selection.activeObject = asset;
                            EditorUtility.DisplayPopupMenu(QuickSearch.ContextualActionPosition, "Assets/", null);
                            EditorApplication.delayCall += () => Selection.activeObject = old;
                        }
                    }
                }
            };
        }

        #if UNITY_2019_1_OR_NEWER
        [UsedImplicitly, Shortcut("Help/Quick Search/Assets")]
        internal static void PopQuickSearch()
        {
            QuickSearch.OpenWithContextualProvider(type);
        }
        #endif
    }
}
