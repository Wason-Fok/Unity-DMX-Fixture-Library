using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Unity.QuickSearch
{
    public static class SearchUtils
    {
        public static readonly char[] entrySeparators = { '/', ' ', '_', '-', '.' };

        public static string[] FindShiftLeftVariations(string word)
        {
            if (word.Length <= 1)
                return new string[0];

            var variations = new List<string>(word.Length) { word };
            for (int i = 1, end = word.Length - 1; i < end; ++i)
            {
                word = word.Substring(1);
                variations.Add(word);
            }

            return variations.ToArray();
        }
        /// <summary>
        /// Tokenize a string each Capital letter.
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static string[] SplitCamelCase(string source)
        {
            return Regex.Split(source, @"(?<!^)(?=[A-Z0-9])");
        }

        public static IEnumerable<string> SplitEntryComponents(string entry, char[] entrySeparators, int minIndexCharVariation, int maxIndexCharVariation)
        {
            var nameTokens = entry.Split(entrySeparators).Distinct();
            var scc = nameTokens.SelectMany(s => SplitCamelCase(s)).Where(s => s.Length > 0);
            var fcc = scc.Aggregate("", (current, s) => current + s[0]);
            return new []{ fcc, entry }.Concat(scc.Where(s => s.Length > 1))
                                .Where(s => s.Length > 0)
                                .Select(s => s.Substring(0, Math.Min(s.Length, maxIndexCharVariation)).ToLowerInvariant())
                                .Distinct();
        }

        public static IEnumerable<string> SplitFileEntryComponents(string path, char[] entrySeparators, int minIndexCharVariation, int maxIndexCharVariation)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            var nameTokens = name.Split(entrySeparators).Distinct().ToArray();
            var scc = nameTokens.SelectMany(s => SplitCamelCase(s)).Where(s => s.Length > 0).ToArray();
            var fcc = scc.Aggregate("", (current, s) => current + s[0]);
            return Enumerable.Empty<string>()
                             .Concat(scc.Where(s => s.Length > 1))
                             .Concat(new[] { Path.GetExtension(path).Replace(".", "") })
                             .Concat(FindShiftLeftVariations(fcc))
                             .Concat(nameTokens)
                             .Concat(path.Split(entrySeparators).Reverse())
                             .Where(s => s.Length > 0)
                             .Select(s => s.Substring(0, Math.Min(s.Length, maxIndexCharVariation)).ToLowerInvariant())
                             .Distinct();
        }
    }
}