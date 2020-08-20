//#define DEBUG_UBER_INDEXING

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.QuickSearch.Providers
{
    class AssetIndexer : SearchIndexer
    {
        internal const string k_IndexFilePath = "Library/quicksearch.uber.index";

        public bool useFinishThread { get; set; } = true;

        #if DEBUG_UBER_INDEXING
        private readonly Dictionary<string, HashSet<string>> m_DebugStringTable = new Dictionary<string, HashSet<string>>();
        #endif

        private readonly QueryEngine<SearchEntryResult> m_QueryEngine = new QueryEngine<SearchEntryResult>(validateFilters: false);

        public AssetIndexer() : base("assets")
        {
            minIndexCharVariation = 2;
            maxIndexCharVariation = 12;
            getIndexFilePathHandler = basePath => k_IndexFilePath;
            getEntryComponentsHandler = GetEntryComponents;
        }

        public IEnumerable<string> GetEntryComponents(string path, int index)
        {
            return SearchUtils.SplitFileEntryComponents(path, entrySeparators, minIndexCharVariation, maxIndexCharVariation);
        }

        public event Action<int, string, float, bool> reportProgress;

        public IEnumerable<SearchEntryResult> Search(string searchQuery)
        {
            var query = m_QueryEngine.Parse<SearchIndexerQuery, SearchIndexerQuery.EvalHandler, object>(searchQuery);
            if (!query.valid)
                return new SearchEntryResult[0];
            return query.Eval(args =>
            {
                if (args.op == SearchIndexOperator.None)
                    return SearchIndexerQuery.EvalResult.None;

                #if DEBUG_UBER_INDEXING
                using(var t = new DebugTimer(null))
                #endif
                {
                    HashSet<int> subset = null;
                    if (args.andSet != null)
                        subset = new HashSet<int>(args.andSet.Select(e => e.index));

                    var results = SearchTerm(args.name, args.value, args.op, args.exclude, int.MaxValue, subset);
                    if (args.orSet != null)
                        results = results.Concat(args.orSet).OrderBy(e => e.score).Distinct();

                    #if DEBUG_UBER_INDEXING
                    SearchIndexerQuery.EvalResult.Print(args, results, subset, t.timeMs);
                    #endif
                    return SearchIndexerQuery.EvalResult.Combined(results);
                }
            }, null);
        }

        public override void Build()
        {
            if (LoadIndexFromDisk(null, true))
                return;

            var it = BuildAsync(-1, null);
            while (it.MoveNext())
                ;
        }

        internal System.Collections.IEnumerator BuildAsync(int progressId, object userData = null)
        {
            var paths = AssetDatabase.GetAllAssetPaths();
            var pathIndex = 0;
            var pathCount = (float)paths.Length;

            Start(clear: true);

            EditorApplication.LockReloadAssemblies();
            foreach (var path in paths)
            {
                var progressReport = pathIndex++ / pathCount;
                reportProgress?.Invoke(progressId, path, progressReport, false);
                IndexAsset(path, false);
                yield return null;
            }
            EditorApplication.UnlockReloadAssemblies();

            Finish(useFinishThread);

            while (!IsReady())
                yield return null;

            reportProgress?.Invoke(progressId, $"Indexing Completed (Documents: {documentCount}, Indexes: {indexCount:n0})", 1f, true);
            yield return null;
        }

        private string[] GetComponents(string value, int documentIndex)
        {
            return getEntryComponentsHandler(value, documentIndex).Where(c => c.Length > 0).ToArray();
        }

        private void AddWordComponents(string path, int documentIndex, string word)
        {
            foreach (var c in GetComponents(word, documentIndex))
                AddWord(path, c, documentIndex);
        }

        private void AddPropertyComponents(string path, int documentIndex, string name, string value)
        {
            foreach (var c in GetComponents(value, documentIndex))
                AddProperty(path, name, c, documentIndex);
        }

        internal void IndexAsset(string path, bool checkIfDocumentExists)
        {
            var documentIndex = AddDocument(path, checkIfDocumentExists);
            if (documentIndex < 0)
                return;

            AddWordComponents(path, documentIndex, path);

            var fi = new FileInfo(path);
            if (fi.Exists)
                AddProperty(path, "size", (double)fi.Length, documentIndex);

            try
            {
                var fileName = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                AddWord(path, fileName, documentIndex, Math.Min(16, fileName.Length), true);

                var mainAsset = AssetDatabase.LoadAssetAtPath<Object>(path);
                if (!mainAsset)
                    return;

                if (!String.IsNullOrEmpty(mainAsset.name))
                    AddWord(path, mainAsset.name, documentIndex, true);

                Type at = mainAsset.GetType();
                while (at != null && at != typeof(Object))
                { 
                    AddProperty(path, "t", at.Name, documentIndex);
                    at = at.BaseType;
                }

                if (PrefabUtility.GetPrefabAssetType(mainAsset) != PrefabAssetType.NotAPrefab)
                    AddProperty(path, "t", "prefab", documentIndex);

                IndexObject(path, mainAsset, documentIndex);

                if (mainAsset is GameObject go)
                {
                    foreach (var v in go.GetComponents(typeof(Component)))
                    {
                        if (!v)
                            continue;
                        AddPropertyComponents(path, documentIndex, "has", v.GetType().Name);
                        IndexObject(path, v, documentIndex);
                    }
                }

                foreach (var depPath in AssetDatabase.GetDependencies(path, true))
                {
                    if (path == depPath)
                        continue;
                    var depName = Path.GetFileNameWithoutExtension(depPath);
                    AddProperty(path, "dep", depName, documentIndex);
                }

                if (path.StartsWith("Packages/", StringComparison.Ordinal))
                    AddProperty(path, "a", "packages", documentIndex);
                else
                    AddProperty(path, "a", "assets", documentIndex);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        [System.Diagnostics.Conditional("DEBUG_UBER_INDEXING")]
        private void AddDebugMatch(string path, string name, string value)
        {
            AddDebugMatch(path, $"{name}:{value}");
        }

        [System.Diagnostics.Conditional("DEBUG_UBER_INDEXING")]
        private void AddDebugMatch(string path, string word)
        {
            #if DEBUG_UBER_INDEXING
            HashSet<string> words;
            if (m_DebugStringTable.TryGetValue(path, out words))
            {
                words.Add(word);
            }
            else
            {
                m_DebugStringTable[path] = new HashSet<string> { word };
            }
            #endif
        }

        private void AddWord(string path, string word, int documentIndex, int maxVariations, bool exact)
        {
            AddDebugMatch(path, word);
            AddWord(word.ToLowerInvariant(), minIndexCharVariation, maxVariations, 0, documentIndex);
            if (exact)
                AddExactWord(word.ToLowerInvariant(), 0, documentIndex);
        }

        private void AddWord(string path, string word, int documentIndex, bool exact = false)
        {
            AddWord(path, word, documentIndex, maxIndexCharVariation, exact);
        }

        private void AddProperty(string path, string name, string value, int documentIndex)
        {
            if (String.IsNullOrEmpty(value))
                return;
            AddDebugMatch(path, name, value);
            AddProperty(name, value.ToLowerInvariant(), documentIndex);
        }

        private void AddProperty(string path, string name, double number, int documentIndex)
        {
            AddDebugMatch(path, name, number.ToString());
            AddProperty(name, number, documentIndex);
        }

        internal string GetDebugIndexStrings(string path)
        {
            #if DEBUG_UBER_INDEXING
            if (!m_DebugStringTable.ContainsKey(path))
                return null;
                
            return String.Join(" ", m_DebugStringTable[path].ToArray());
            #else
            return null;
            #endif
        }

        private object GetPropertyValue(SerializedProperty p)
        {
            object fieldValue = null;
            switch (p.propertyType)
            {
                case SerializedPropertyType.Integer:
                    fieldValue = (double)p.intValue;
                    break;
                case SerializedPropertyType.Boolean:
                    fieldValue = p.boolValue.ToString();
                    break;
                case SerializedPropertyType.Float:
                    fieldValue = (double)p.floatValue;
                    break;
                case SerializedPropertyType.String:
                    if (p.stringValue != null && p.stringValue.Length < 10)
                        fieldValue = p.stringValue.Replace(" ", "").ToString();
                    break;
                case SerializedPropertyType.Enum:
                    if (p.enumValueIndex >= 0 && p.type == "Enum")
                        fieldValue = p.enumNames[p.enumValueIndex].ToString();
                    break;
                case SerializedPropertyType.ObjectReference:
                    if (p.objectReferenceValue)
                        fieldValue = p.objectReferenceValue.name.Replace(" ", "");
                    break;
                case SerializedPropertyType.Color:
                case SerializedPropertyType.Vector2:
                case SerializedPropertyType.Vector3:
                case SerializedPropertyType.Vector4:
                case SerializedPropertyType.Rect:
                case SerializedPropertyType.ArraySize:
                case SerializedPropertyType.Character:
                case SerializedPropertyType.AnimationCurve:
                case SerializedPropertyType.Bounds:
                case SerializedPropertyType.Gradient:
                case SerializedPropertyType.Quaternion:
                case SerializedPropertyType.ExposedReference:
                case SerializedPropertyType.FixedBufferSize:
                case SerializedPropertyType.Vector2Int:
                case SerializedPropertyType.Vector3Int:
                case SerializedPropertyType.RectInt:
                case SerializedPropertyType.BoundsInt:
                //case SerializedPropertyType.ManagedReference:
                case SerializedPropertyType.Generic:
                case SerializedPropertyType.LayerMask:
                default:
                    break;
            }

            return fieldValue;
        }

        private void IndexObject(string path, Object obj, int documentIndex)
        {
            using (var so = new SerializedObject(obj))
            {
                var p = so.GetIterator();
                var next = p.Next(true);
                while (next)
                {
                    var fieldName = p.displayName.Replace("m_", "").Replace(" ", "");
                    var scc = SearchUtils.SplitCamelCase(fieldName);
                    var fcc = scc.Length > 1 && fieldName.Length > 10 ? scc.Aggregate("", (current, s) => current + s[0]) : fieldName;
                    object fieldValue = GetPropertyValue(p);

                    if (fieldValue != null)
                    {
                        var sfv = fieldValue as string;
                        if (sfv != null)
                        {
                            if (sfv != "")
                                AddProperty(fcc.ToLowerInvariant(), sfv.Replace(" ", "").ToLowerInvariant(), documentIndex);
                            else
                                AddWord($"@{fcc}".ToLowerInvariant(), 0, documentIndex);
                        }
                        else if (fieldValue is double)
                        {
                            var nfv = (double)fieldValue;
                            AddProperty(fcc.ToLowerInvariant(), nfv, documentIndex);
                        }

                        AddDebugMatch(path, fcc, fieldValue.ToString());
                    }
                    next = p.Next(false);
                }
            }
        }
    }
}
