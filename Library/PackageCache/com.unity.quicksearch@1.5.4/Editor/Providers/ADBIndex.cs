//#define DEBUG_UBER_INDEXING

using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Unity.QuickSearch.Providers
{
    static class ADBIndex
    {
        private static AssetIndexer s_GlobalIndexer;
        private static bool s_IndexInitialized = false;

        const string k_ProgressTitle = "Building search index...";

        static ADBIndex()
        {
            s_GlobalIndexer = new AssetIndexer();
            Debug.Assert(!s_GlobalIndexer.IsReady());
        }

        public static AssetIndexer Get()
        {
            return s_GlobalIndexer;
        }

        public static void Initialize()
        {
            if (s_IndexInitialized)
                return;

            if (s_GlobalIndexer.LoadIndexFromDisk(null, true))
            {
                s_IndexInitialized = true;
                AssetPostprocessorIndexer.Enable();
                AssetPostprocessorIndexer.contentRefreshed -= OnContentRefreshed;
                AssetPostprocessorIndexer.contentRefreshed += OnContentRefreshed;

                #if DEBUG_UBER_INDEXING
                Debug.Log("Search index loaded from disk");
                #endif
            }
            else
            {
                s_GlobalIndexer.reportProgress += ReportProgress;
                s_GlobalIndexer.Build();
            }
        }

        private static void OnContentRefreshed(string[] updated, string[] removed, string[] moved)
        {
            s_GlobalIndexer.Start();
            foreach (var path in updated.Concat(moved).Distinct())
                s_GlobalIndexer.IndexAsset(path, true);
            s_GlobalIndexer.Finish(true, removed);
        }

        private static void ReportProgress(int progressId, string description, float progress, bool finished)
        {
            EditorUtility.DisplayProgressBar(k_ProgressTitle, description, progress);
            if (finished)
            { 
                EditorUtility.ClearProgressBar();
                Debug.Log(description);
            }
        }

        #if DEBUG_UBER_INDEXING
        [MenuItem("Quick Search/Rebuild Über Index")]
        #endif
        internal static void RebuildIndex()
        {
            if (System.IO.File.Exists(AssetIndexer.k_IndexFilePath))
                System.IO.File.Delete(AssetIndexer.k_IndexFilePath);
            #if UNITY_2019_3_OR_NEWER
            EditorUtility.RequestScriptReload();
            #else
            UnityEditorInternal.InternalEditorUtility.RequestScriptReload();
            #endif
        }
    }
}
