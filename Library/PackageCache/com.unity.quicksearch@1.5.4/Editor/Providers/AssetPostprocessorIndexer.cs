using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;

namespace Unity.QuickSearch.Providers
{
    class AssetPostprocessorIndexer : AssetPostprocessor
    {
        private static bool s_Enabled;

        public static event Action<string[], string[], string[]> contentRefreshed;

        static AssetPostprocessorIndexer()
        {
            EditorApplication.quitting += OnQuitting;
        }

        public static void Enable()
        {
            s_Enabled = true;
        }

        public static void Disable()
        {
            s_Enabled = false;
        }

        private static void OnQuitting()
        {
            s_Enabled = false;
        }

        [UsedImplicitly]
        internal static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] movedTo, string[] movedFrom)
        {
            if (!s_Enabled || contentRefreshed == null || contentRefreshed.GetInvocationList().Length == 0)
                return;

            RaiseContentRefreshed(imported, deleted.Concat(movedFrom).Distinct().ToArray(), movedTo);
        }

        #region Refresh search content event

        private static double s_BatchStartTime;
        private static string[] s_UpdatedItems = new string[0];
        private static string[] s_RemovedItems = new string[0];
        private static string[] s_MovedItems = new string[0];
        internal static void RaiseContentRefreshed(IEnumerable<string> updated, IEnumerable<string> removed, IEnumerable<string> moved)
        {
            s_UpdatedItems = s_UpdatedItems.Concat(updated).Distinct().ToArray();
            s_RemovedItems = s_RemovedItems.Concat(removed).Distinct().ToArray();
            s_MovedItems = s_MovedItems.Concat(moved).Distinct().ToArray();

            if (s_UpdatedItems.Length > 0 || s_RemovedItems.Length > 0 || s_MovedItems.Length > 0)
                RaiseContentRefreshed();
        }

        private static void RaiseContentRefreshed()
        {
            EditorApplication.delayCall -= RaiseContentRefreshed;

            var currentTime = EditorApplication.timeSinceStartup;
            if (s_BatchStartTime != 0 && currentTime - s_BatchStartTime > 0.5)
            {
                if (s_UpdatedItems.Length != 0 || s_RemovedItems.Length != 0 || s_MovedItems.Length != 0)
                    contentRefreshed?.Invoke(s_UpdatedItems, s_RemovedItems, s_MovedItems);
                s_UpdatedItems = new string[0];
                s_RemovedItems = new string[0];
                s_MovedItems = new string[0];
                s_BatchStartTime = 0;
            }
            else
            {
                if (s_BatchStartTime == 0)
                    s_BatchStartTime = currentTime;
                EditorApplication.delayCall += RaiseContentRefreshed;
            }
        }
        #endregion
    }
}
