using System;
using System.Collections.Generic;
using System.IO;

namespace Unity.QuickSearch.Providers
{
    public class FileSearchIndexer : SearchIndexer, IDisposable
    {
        private const int k_MinIndexCharVariation = 2;
        private const int k_MaxIndexCharVariation = 32;

        public string type { get; }

        public FileSearchIndexer(string type, IEnumerable<SearchIndexerRoot> roots)
            : base (roots)
        {
            this.type = type;
            minIndexCharVariation = k_MinIndexCharVariation;
            maxIndexCharVariation = k_MaxIndexCharVariation;
            skipEntryHandler = ShouldSkipEntry;
            getIndexFilePathHandler = GetIndexFilePath;
            getEntryComponentsHandler = (e, i) => SearchUtils.SplitFileEntryComponents(e, entrySeparators, k_MinIndexCharVariation, k_MaxIndexCharVariation);
            enumerateRootEntriesHandler = EnumerateAssetPaths;

            AssetPostprocessorIndexer.Enable();
            AssetPostprocessorIndexer.contentRefreshed += UpdateIndexWithNewContent;
        }

        private static bool ShouldSkipEntry(string entry)
        {
            return entry.Length == 0 || entry[0] == '.' || entry.EndsWith(".meta", System.StringComparison.OrdinalIgnoreCase);
        }

        private string GetIndexFilePath(string basePath)
        {
            string indexFileName = $"quicksearch.{type}.index";
            return Path.GetFullPath(Path.Combine(basePath, "..", "Library", indexFileName));
        }

        private static IEnumerable<string> EnumerateAssetPaths(SearchIndexerRoot root)
        {
            return Directory.EnumerateFiles(root.basePath, "*.*", SearchOption.AllDirectories);
        }

        public void Dispose()
        {
            AssetPostprocessorIndexer.contentRefreshed -= UpdateIndexWithNewContent;
        }
    }
}
