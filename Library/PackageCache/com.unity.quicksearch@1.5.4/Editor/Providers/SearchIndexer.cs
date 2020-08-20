using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEditor;
using UnityEngine.Assertions;

namespace Unity.QuickSearch
{
    internal enum SearchIndexOperator
    {
        Contains,
        Equal,
        NotEqual,
        Greater,
        GreaterOrEqual,
        Less,
        LessOrEqual,
        None
    }

    public enum Combine
    {
        None,
        Intersection,
        Union
    }

    enum SearchIndexEntryType : int
    {
        Undefined = 0,
        Word,
        Number,
        Property
    }

    [DebuggerDisplay("{type} - K:{key}|{number} - C:{crc} - I:{index}")]
    readonly struct SearchIndexEntry : IEquatable<SearchIndexEntry>
    {

        // 1- Initial format
        // 2- Added score to words
        // 3- Save base name in entry paths
        // 4- Added entry types
        internal const int Version = 0x4242E000 | 0x004;

        public readonly long key;   // value hash
        public readonly long crc;   // value correction code (can be length, property key hash, etc.)
        public readonly int _type;  // Type of the index entry
        public readonly int index;
        public readonly int score;
        public readonly double number;

        public SearchIndexEntryType type => (SearchIndexEntryType)_type;

        public SearchIndexEntry(long _key, long _crc, SearchIndexEntryType type, int _index = -1, int _score = int.MaxValue)
        {
            key = _key;
            crc = _crc;
            _type = (int)type;
            index = _index;
            score = _score;
            number = BitConverter.Int64BitsToDouble(key);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return key.GetHashCode() ^ crc.GetHashCode() ^ _type.GetHashCode() ^ index.GetHashCode();
            }
        }

        public override bool Equals(object other)
        {
            return other is SearchIndexEntry l && Equals(l);
        }

        public bool Equals(SearchIndexEntry other)
        {
            return key == other.key && crc == other.crc && _type == other._type && index == other.index;
        }
    }

    [DebuggerDisplay("{index} ({score})")]
    readonly struct SearchPatternMatch : IEquatable<SearchPatternMatch>
    {
        public SearchPatternMatch(int _i, int _s)
        {
            index = _i;
            score = _s;
        }

        public override int GetHashCode()
        {
            return index.GetHashCode();
        }

        public override bool Equals(object other)
        {
            return other is SearchPatternMatch l && Equals(l);
        }

        public bool Equals(SearchPatternMatch other)
        {
            return other.index == index;
        }

        public readonly int index;
        public readonly int score;
    }

    class SearchIndexComparer : IComparer<SearchIndexEntry>, IEqualityComparer<SearchIndexEntry>
    {
        const double EPSILON = 0.0000000000001;

        public SearchIndexOperator op { get; private set; }

        public SearchIndexComparer(SearchIndexOperator op = SearchIndexOperator.Contains)
        {
            this.op = op;
        }

        public int Compare(SearchIndexEntry item1, SearchIndexEntry item2)
        {
            var c = item1._type.CompareTo(item2._type);
            if (c != 0)
                return c;
            c = item1.crc.CompareTo(item2.crc);
            if (c != 0)
                return c;

            if (item1._type == (int)SearchIndexEntryType.Number)
            {
                double eps = 0.0;
                if (op == SearchIndexOperator.Less)
                    eps = -EPSILON;
                else if (op == SearchIndexOperator.Greater)
                    eps = EPSILON;
                c = item1.number.CompareTo(item2.number + eps);
            }
            else
                c = item1.key.CompareTo(item2.key);
            if (c != 0)
                return c;

            if (item2.score == int.MaxValue)
                return 0;
            return item1.score.CompareTo(item2.score);
        }

        public bool Equals(SearchIndexEntry x, SearchIndexEntry y)
        {
            return x.Equals(y);
        }

        public int GetHashCode(SearchIndexEntry obj)
        {
            return obj.GetHashCode();
        }
    }

    [DebuggerDisplay("{path} ({score})")]
    public readonly struct SearchEntryResult : IEquatable<SearchEntryResult>
    {
        public readonly string path;
        public readonly int index;
        public readonly int score;

        public SearchEntryResult(string document, int documentIndex, int score)
        {
            path = document;
            index = documentIndex;
            this.score = score;
        }

        public bool Equals(SearchEntryResult other)
        {
            return index == other.index && path == other.path;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return index.GetHashCode() ^ path.GetHashCode();
            }
        }

        public override bool Equals(object other)
        {
            return other is SearchEntryResult l && Equals(l);
        }
    }

    [DebuggerDisplay("{baseName} ({basePath})")]
    public struct SearchIndexerRoot
    {
        public readonly string basePath;
        public readonly string baseName;

        public SearchIndexerRoot(string _p, string _n)
        {
            basePath = _p.Replace('\\', '/');
            baseName = _n;
        }
    }

    public class SearchIndexer
    {
        public SearchIndexerRoot[] roots { get; }
        public int minIndexCharVariation { get; set; } = 2;
        public int maxIndexCharVariation { get; set; } = 8;
        public char[] entrySeparators { get; set; } = SearchUtils.entrySeparators;

        internal int documentCount => m_Documents.Count;
        internal int indexCount
        {
            get
            {
                lock (this)
                {
                    int total = 0;
                    if (m_Indexes != null && m_Indexes.Length > 0)
                        total += m_Indexes.Length;
                    if (m_BatchIndexes != null && m_BatchIndexes.Count > 0)
                        total += m_BatchIndexes.Count;
                    return total;
                }
            }
        }

        internal Dictionary<int, int> patternMatchCount { get; set; } = new Dictionary<int, int>();

        // Handler used to skip some entries. 
        public Func<string, bool> skipEntryHandler { get; set; }
            
        // Handler used to specify where the index database file should be saved. If the handler returns null, the database won't be saved at all.
        public Func<string, string> getIndexFilePathHandler { get; set; }
            
        // Handler used to parse and split the search query text into words. The tokens needs to be split similarly to how getEntryComponentsHandler was specified.
        public Func<string, string[]> getQueryTokensHandler { get; set; }

        // Handler used to split into words the entries. The order of the words matter. Words at the beginning of the array have a lower score (lower the better)
        public Func<string, int, IEnumerable<string>> getEntryComponentsHandler { get; set; }
            
        // Handler used to fetch all the entries under a given root.
        public Func<SearchIndexerRoot, IEnumerable<string>> enumerateRootEntriesHandler { get; set; }

        private Thread m_IndexerThread;
        private volatile bool m_IndexReady = false;
        private volatile bool m_ThreadAborted = false;
        private string m_IndexTempFilePath;
        private Dictionary<RangeSet, IndexRange> m_FixedRanges = new Dictionary<RangeSet, IndexRange>();

        // Final documents and entries when the index is ready.
        private List<string> m_Documents;
        private SearchIndexEntry[] m_Indexes;

        // Temporary documents and entries while the index is being built (i.e. Start/Finish).
        private List<SearchIndexEntry> m_BatchIndexes;

        public SearchIndexer(string rootPath)
            : this(rootPath, String.Empty)
        {
        }

        public SearchIndexer(string rootPath, string rootName)
            : this(new[] { new SearchIndexerRoot(rootPath, rootName) })
        {
        }

        public SearchIndexer(IEnumerable<SearchIndexerRoot> roots)
        {
            this.roots = roots.ToArray();

            skipEntryHandler = e => false;
            getIndexFilePathHandler = p => null;
            getEntryComponentsHandler = (e, i) => throw new Exception("You need to specify the get entry components handler");
            enumerateRootEntriesHandler = r => throw new Exception("You need to specify the root entries enumerator");
            getQueryTokensHandler = ParseQuery;

            m_Documents = new List<string>();
            m_Indexes = new SearchIndexEntry[0];
            m_IndexTempFilePath = Path.GetTempFileName();
        }

        public virtual void Build()
        {
            Build(true);
        }

        public void Build(bool useThread)
        {
            if (useThread)
            {
                m_ThreadAborted = false;
                m_IndexerThread = new Thread(() =>
                {
                    try
                    {
                        using (new IndexerThreadScope(AbortIndexing))
                            Build(false);
                    }
                    catch (ThreadAbortException)
                    {
                        m_ThreadAborted = true;
                        Thread.ResetAbort();
                    }
                });
                m_IndexerThread.Start();
            }
            else
            {
                BuildWordIndexes();
            }
        }

        private void FetchEntries(string document, int documentIndex, List<SearchIndexEntry> indexes, int baseScore = 0)
        {
            var components = getEntryComponentsHandler(document, documentIndex).ToArray();
            for (int compIndex = 0; compIndex < components.Length; ++compIndex)
            {
                var p = components[compIndex];
                if (p.Length == 0)
                    continue;

                AddWord(p, minIndexCharVariation, maxIndexCharVariation, baseScore + compIndex, documentIndex, indexes);
            }
        }

        internal int AddDocument(string document, bool checkIfExists = true)
        {
            // Reformat entry to have them all uniformized.
            if (skipEntryHandler(document))
                return -1;

            lock(this)
            {
                if (checkIfExists)
                {
                    var di = m_Documents.FindIndex(d => d == document);
                    if (di >= 0)
                        return di;
                }
                m_Documents.Add(document);
                return m_Documents.Count - 1;
            }
        }

        internal void AddWord(string word, int score, int documentIndex)
        {
            AddWord(word, minIndexCharVariation, maxIndexCharVariation, score, documentIndex, m_BatchIndexes);
        }

        internal void AddWord(string word, int score, int documentIndex, List<SearchIndexEntry> indexes)
        {
            AddWord(word, minIndexCharVariation, maxIndexCharVariation, score, documentIndex, indexes);
        }

        internal void AddWord(string word, int size, int score, int documentIndex)
        {
            AddWord(word, size, size, score, documentIndex, m_BatchIndexes);
        }

        internal void AddExactWord(string word, int score, int documentIndex)
        {
            AddExactWord(word, score, documentIndex, m_BatchIndexes);
        }

        internal void AddExactWord(string word, int score, int documentIndex, List<SearchIndexEntry> indexes)
        {
            indexes.Add(new SearchIndexEntry(word.GetHashCode(), long.MaxValue, SearchIndexEntryType.Word, documentIndex, score));
        }

        internal void AddWord(string word, int minVariations, int maxVariations, int score, int documentIndex)
        {
            AddWord(word, minVariations, maxVariations, score, documentIndex, m_BatchIndexes);
        }

        internal void AddWord(string word, int minVariations, int maxVariations, int score, int documentIndex, List<SearchIndexEntry> indexes)
        {
            if (word == null || word.Length == 0)
                return;

            if (word[0] == '@')
            {
                word = word.Substring(1);
                var vpPos = word.IndexOf(':');
                if (vpPos != -1)
                    minVariations = vpPos + 2;
                else
                    minVariations = word.Length;
            }

            maxVariations = Math.Min(maxVariations, word.Length);

            for (int c = Math.Min(minVariations, maxVariations); c <= maxVariations; ++c)
            {
                var ss = word.Substring(0, c);
                indexes.Add(new SearchIndexEntry(ss.GetHashCode(), ss.Length, SearchIndexEntryType.Word, documentIndex, score));
            }

            if (word.Length > maxVariations)
                indexes.Add(new SearchIndexEntry(word.GetHashCode(), word.Length, SearchIndexEntryType.Word, documentIndex, score-1));
        }

        internal void AddProperty(string key, string value, int documentIndex)
        {
            AddProperty(key, value, minIndexCharVariation, maxIndexCharVariation, 0, documentIndex, m_BatchIndexes);
        }

        internal void AddProperty(string key, string value, int score, int documentIndex)
        {
            AddProperty(key, value, minIndexCharVariation, maxIndexCharVariation, score, documentIndex, m_BatchIndexes);
        }

        internal void AddProperty(string key, double value, int documentIndex)
        {
            AddProperty(key, value, documentIndex, m_BatchIndexes);
        }

        private bool ExcludeWordVariations(string word)
        {
            if (word == "true" || word == "false")
                return true;
            return false;
        }

        internal void AddProperty(string name, string value, int minVariations, int maxVariations, int score, int documentIndex)
        {
            AddProperty(name, value, minVariations, maxVariations, score, documentIndex, m_BatchIndexes);
        }

        internal void AddProperty(string name, string value, int minVariations, int maxVariations, int score, int documentIndex, List<SearchIndexEntry> indexes)
        {
            var nameHash = name.GetHashCode();
            var valueHash = value.GetHashCode();
            maxVariations = Math.Min(maxVariations, value.Length);
            if (minVariations > value.Length)
                minVariations = value.Length;
            if (ExcludeWordVariations(value))
                minVariations = maxVariations = value.Length;
            for (int c = Math.Min(minVariations, maxVariations); c <= maxVariations; ++c)
            {
                var ss = value.Substring(0, c);
                indexes.Add(new SearchIndexEntry(ss.GetHashCode(), nameHash, SearchIndexEntryType.Property, documentIndex, score + (maxVariations - c)));
            }

            if (value.Length > maxVariations)
                indexes.Add(new SearchIndexEntry(valueHash, nameHash, SearchIndexEntryType.Property, documentIndex, score-1));

            // Add an exact match for property="match"
            nameHash = nameHash ^ name.Length.GetHashCode();
            valueHash = value.GetHashCode() ^ value.Length.GetHashCode();
            indexes.Add(new SearchIndexEntry(valueHash, nameHash, SearchIndexEntryType.Property, documentIndex, score));
        }

        internal void AddProperty(string key, double value, int documentIndex, List<SearchIndexEntry> indexes)
        {
            var keyHash = key.GetHashCode();
            var longNumber = BitConverter.DoubleToInt64Bits(value);
            indexes.Add(new SearchIndexEntry(longNumber, keyHash, SearchIndexEntryType.Number, documentIndex, 0));
        }

        internal void Start(bool clear = false)
        {
            lock (this)
            {
                m_IndexerThread = null;
                m_ThreadAborted = false;
                m_IndexReady = false;
                m_BatchIndexes = new List<SearchIndexEntry>();
                m_FixedRanges.Clear();
                patternMatchCount.Clear();

                if (clear)
                {
                    m_Documents.Clear();
                    m_Indexes = new SearchIndexEntry[0];
                }
            }
        }

        internal void Finish(bool useThread = false, string[] removedDocuments = null)
        {
            if(useThread)
            {
                m_ThreadAborted = false;
                m_IndexerThread = new Thread(() =>
                {
                    try
                    {
                        using (new IndexerThreadScope(AbortIndexing))
                            Finish(false, removedDocuments);
                    }
                    catch (ThreadAbortException)
                    {
                        m_ThreadAborted = true;
                        Thread.ResetAbort();
                    }
                });
                m_IndexerThread.Start();
            }
            else
            {
                lock (this)
                {
                    var shouldRemoveDocuments = removedDocuments != null && removedDocuments.Length > 0;
                    if (shouldRemoveDocuments)
                    {
                        var removedDocIndexes = new HashSet<int>();
                        foreach (var rd in removedDocuments)
                        {
                            var di = m_Documents.FindIndex(d => d == rd);
                            if (di > -1)
                                removedDocIndexes.Add(di);
                        }
                        m_BatchIndexes.AddRange(m_Indexes.Where(e => !removedDocIndexes.Contains(e.index)));
                    }
                    else
                    {
                        m_BatchIndexes.AddRange(m_Indexes);
                    }
                    UpdateIndexes(m_Documents, m_BatchIndexes, roots[0].basePath);
                    m_BatchIndexes.Clear();
                    //UnityEngine.Debug.Log($"Indexing Completed (Documents: {documentCount}, Indexes: {indexCount:n0})");
                }
            }
        }

        internal void Print()
        {
            #if UNITY_2020_1_OR_NEWER
            foreach (var i in m_Indexes)
            {
                UnityEngine.Debug.LogFormat(UnityEngine.LogType.Log, UnityEngine.LogOption.NoStacktrace, null, 
                    $"{i.type} - {i.crc} - {i.key} - {i.index} - {i.score}");
            }
            #endif
        }

        public bool IsReady()
        {
            return m_IndexReady;
        }

        private IEnumerable<SearchPatternMatch> SearchWord(string word, SearchIndexOperator op, int maxScore, HashSet<int> documentIndexes, int patternMatchLimit)
        {
            var comparer = new SearchIndexComparer(op);
            long crc = word.Length;
            if (op == SearchIndexOperator.Equal)
                crc = long.MaxValue;
            return SearchIndexes(word.GetHashCode(), crc, SearchIndexEntryType.Word, maxScore, comparer, documentIndexes, patternMatchLimit);
        }

        private IEnumerable<SearchPatternMatch> ExcludeWord(string word, SearchIndexOperator op, HashSet<int> inset)
        {
            if (inset == null)
                inset = GetAllDocumentIndexesSet();

            var includedDocumentIndexes = new HashSet<int>(SearchWord(word, op, int.MaxValue, null, int.MaxValue).Select(m => m.index));
            return inset.Where(d => !includedDocumentIndexes.Contains(d)).Select(d => new SearchPatternMatch(d, 0));
        }

        private IEnumerable<SearchPatternMatch> ExcludeProperty(string name, string value, SearchIndexOperator op, int maxScore, HashSet<int> inset, int limit)
        {
            if (inset == null)
                inset = GetAllDocumentIndexesSet();

            var includedDocumentIndexes = new HashSet<int>(SearchProperty(name, value, op, int.MaxValue, null, int.MaxValue).Select(m => m.index));
            return inset.Where(d => !includedDocumentIndexes.Contains(d)).Select(d => new SearchPatternMatch(d, 0));
        }

        private IEnumerable<SearchPatternMatch> SearchProperty(string name, string value, SearchIndexOperator op, int maxScore, HashSet<int> documentIndexes, int patternMatchLimit)
        {
            var comparer = new SearchIndexComparer(op);
            var valueHash = value.GetHashCode();
            var nameHash = name.GetHashCode();
            if (comparer.op == SearchIndexOperator.Equal)
            {
                nameHash ^= name.Length.GetHashCode();
                valueHash ^= value.Length.GetHashCode();
            }

            return SearchIndexes(valueHash, nameHash, SearchIndexEntryType.Property, maxScore, comparer, documentIndexes, patternMatchLimit);
        }

        private HashSet<int> m_AllDocumentIndexes;
        private HashSet<int> GetAllDocumentIndexesSet()
        {
            if (m_AllDocumentIndexes != null)
                return m_AllDocumentIndexes;
            m_AllDocumentIndexes = new HashSet<int>();
            for (int i = 0; i < documentCount; ++i)
                m_AllDocumentIndexes.Add(i);
            return m_AllDocumentIndexes;
        }

        private IEnumerable<SearchPatternMatch> ExcludeNumber(string name, double number, SearchIndexOperator op, HashSet<int> inset)
        {
            if (inset == null)
                inset = GetAllDocumentIndexesSet();

            var includedDocumentIndexes = new HashSet<int>(SearchNumber(name, number, op, int.MaxValue, null).Select(m => m.index));
            return inset.Where(d => !includedDocumentIndexes.Contains(d)).Select(d => new SearchPatternMatch(d, 0));
        }

        private IEnumerable<SearchPatternMatch> SearchNumber(string key, double value, SearchIndexOperator op, int maxScore, HashSet<int> documentIndexes)
        {
            var wiec = new SearchIndexComparer(op);
            return SearchIndexes(BitConverter.DoubleToInt64Bits(value), key.GetHashCode(), SearchIndexEntryType.Number, maxScore, wiec, documentIndexes);
        }

        public IEnumerable<SearchEntryResult> Search(string query, int maxScore = int.MaxValue, int patternMatchLimit = 2999)
        {
            //using (new DebugTimer($"Search Index ({query})"))
            {
                if (!m_IndexReady)
                    return Enumerable.Empty<SearchEntryResult>();

                var tokens = getQueryTokensHandler(query);
                Array.Sort(tokens, SortTokensByPatternMatches);

                var lengths = tokens.Select(p => p.Length).ToArray();
                var patterns = tokens.Select(p => p.GetHashCode()).ToArray();

                if (patterns.Length == 0)
                    return Enumerable.Empty<SearchEntryResult>();

                var wiec = new SearchIndexComparer();
                var entryIndexes = new HashSet<int>();
                lock (this)
                {
                    var remains = SearchIndexes(patterns[0], lengths[0], SearchIndexEntryType.Word, maxScore, wiec, entryIndexes, patternMatchLimit).ToList();
                    patternMatchCount[patterns[0]] = remains.Count;

                    if (remains.Count == 0)
                        return Enumerable.Empty<SearchEntryResult>();

                    for (int i = 1; i < patterns.Length; ++i)
                    {
                        var newMatches = SearchIndexes(patterns[i], lengths[i], SearchIndexEntryType.Word, maxScore, wiec, entryIndexes).ToArray();
                        IntersectPatternMatches(remains, newMatches);
                    }

                    return remains.Select(fi => new SearchEntryResult(m_Documents[fi.index], fi.index, fi.score));
                }
            }
        }

        readonly char[] k_OpCharacters = new char[] { ':', '=', '<', '>', '!' };
        public IEnumerable<SearchEntryResult> SearchTerms(string query, int maxScore = int.MaxValue, int patternMatchLimit = 2999)
        {
            if (!m_IndexReady)
                return Enumerable.Empty<SearchEntryResult>();

            var tokens = getQueryTokensHandler(query);
            if (tokens.Length == 0)
                return Enumerable.Empty<SearchEntryResult>();
            Array.Sort(tokens, SortTokensByPatternMatches);

            //using (new DebugTimer($"Search Terms ({String.Join(", ", tokens)})"))
            {
                var documentIndexes = tokens.Length > 1 ? new HashSet<int>() : null;
                var results = new List<SearchPatternMatch>();

                lock (this)
                {
                    for (int tokenIndex = 0; tokenIndex < tokens.Length; ++tokenIndex)
                    {
                        var token = tokens[tokenIndex];
                        if (token.Length < minIndexCharVariation)
                            continue;

                        IEnumerable<SearchPatternMatch> matches = null;
                        var opEndSepPos = token.LastIndexOfAny(k_OpCharacters);
                        if (opEndSepPos > 0)
                        {
                            // Search property
                            var opBeginSepPos = token.IndexOfAny(k_OpCharacters);
                            if (opBeginSepPos > opEndSepPos || opEndSepPos == token.Length - 1)
                                continue;
                            var name = token.Substring(0, opBeginSepPos);
                            var value = token.Substring(opEndSepPos + 1);
                            value = value.Substring(0, Math.Min(value.Length, maxIndexCharVariation));
                            var opString = token.Substring(opBeginSepPos, opEndSepPos - opBeginSepPos + 1);
                            var op = SearchIndexOperator.Contains;

                            switch (opString)
                            {
                                case "=": op = SearchIndexOperator.Equal; break;
                                case ">": op = SearchIndexOperator.Greater; break;
                                case ">=": op = SearchIndexOperator.GreaterOrEqual; break;
                                case "<": op = SearchIndexOperator.Less; break;
                                case "<=": op = SearchIndexOperator.LessOrEqual; break;
                                case "!=": op = SearchIndexOperator.NotEqual; break;
                                default: // :, etc.
                                    op = SearchIndexOperator.Contains;
                                    break;
                            }

                            double number;
                            if (double.TryParse(value, out number))
                                matches = SearchNumber(name, number, op, maxScore, documentIndexes);
                            else
                                matches = SearchProperty(name, value, op, maxScore, documentIndexes, patternMatchLimit);
                        }
                        else
                        {
                            // Search word
                            var word = token;
                            var op = SearchIndexOperator.Contains;
                            if (word[0] == '!')
                            {
                                word = word.Substring(1);
                                op = SearchIndexOperator.Equal;
                            }

                            matches = SearchWord(word, op, maxScore, documentIndexes, patternMatchLimit);
                        }

                        if (tokenIndex == 0 && results.Count == 0)
                        {
                            results = matches.ToList();
                            patternMatchCount[token.GetHashCode()] = results.Count;
                        }
                        else
                            IntersectPatternMatches(results, matches.ToArray());
                    }
                }

                return results.Select(r => new SearchEntryResult(m_Documents[r.index], r.index, r.score));
            }
        } 

        internal IEnumerable<SearchEntryResult> SearchTerm(
            string name, object value, SearchIndexOperator op, bool exclude,
            int maxScore = int.MaxValue, HashSet<int> documentIndexes = null, int limit = int.MaxValue)
        {
            if (op == SearchIndexOperator.NotEqual)
            {
                exclude = true;
                op = SearchIndexOperator.Equal;
            }

            IEnumerable<SearchPatternMatch> matches = null;
            if (!String.IsNullOrEmpty(name))
            {
                name = name.ToLowerInvariant();

                // Search property
                double number;
                if (value is double)
                {
                    number = (double)value;
                    matches = SearchNumber(name, number, op, maxScore, documentIndexes);
                }
                else if (value is string)
                {
                    var valueString = (string)value;
                    if (double.TryParse(valueString, out number))
                    {
                        if (!exclude && op != SearchIndexOperator.NotEqual)
                            matches = SearchNumber(name, number, op, maxScore, documentIndexes);
                        else
                            matches = ExcludeNumber(name, number, op, documentIndexes);
                    }
                    else
                    {
                        if (!exclude)
                            matches = SearchProperty(name, valueString.ToLowerInvariant(), op, maxScore, documentIndexes, limit);
                        else
                            matches = ExcludeProperty(name, valueString.ToLowerInvariant(), op, maxScore, documentIndexes, limit);
                    }
                }
                else
                    throw new ArgumentException($"value must be a number or a string", nameof(value));
            }
            else if (value is string)
            {
                // Search word
                if (!exclude)
                    matches = SearchWord((string)value, op, maxScore, documentIndexes, limit);
                else
                    matches = ExcludeWord((string)value, op, documentIndexes);
            }
            else
                throw new ArgumentException($"word value must be a string", nameof(value));

            if (matches == null)
                return null;
            return matches.Select(r => new SearchEntryResult(m_Documents[r.index], r.index, r.score));
        }

        private int SortTokensByPatternMatches(string item1, string item2)
        {
            patternMatchCount.TryGetValue(item1.GetHashCode(), out var item1PatternMatchCount);
            patternMatchCount.TryGetValue(item2.GetHashCode(), out var item2PatternMatchCount);
            var c = item1PatternMatchCount.CompareTo(item2PatternMatchCount);
            if (c != 0) 
                return c;
            return item1.Length.CompareTo(item2.Length) * -1;
        }

        private void IntersectPatternMatches(IList<SearchPatternMatch> remains, SearchPatternMatch[] newMatches)
        {
            for (int r = remains.Count - 1; r >= 0; r--)
            {
                bool intersects = false;
                foreach (var m in newMatches)
                {
                    if (remains[r].index == m.index)
                    {
                        intersects = true;
                        remains[r] = new SearchPatternMatch(m.index, Math.Min(remains[r].score, m.score));
                    }
                }

                if (!intersects)
                    remains.RemoveAt(r);
            }
        }

        private void SaveIndexToDisk(string basePath)
        {
            var indexFilePath = getIndexFilePathHandler(basePath);
            if (String.IsNullOrEmpty(indexFilePath))
                return;

            using (var indexStream = new FileStream(m_IndexTempFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var indexWriter = new BinaryWriter(indexStream))
            {
                indexWriter.Write(SearchIndexEntry.Version);
                indexWriter.Write(basePath);

                indexWriter.Write(m_Documents.Count);
                foreach (var p in m_Documents)
                    indexWriter.Write(p);
                indexWriter.Write(m_Indexes.Length);
                foreach (var p in m_Indexes)
                {
                    indexWriter.Write(p.key);
                    indexWriter.Write(p.crc);
                    indexWriter.Write((int)p.type);
                    indexWriter.Write(p.index);
                    indexWriter.Write(p.score);
                }
            }

            try
            {
                if (File.Exists(indexFilePath))
                    File.Delete(indexFilePath);
            }
            catch (IOException)
            {
                // ignore file index persistence operation, since it is not critical and will redone later.
            }

            try
            {
                File.Move(m_IndexTempFilePath, indexFilePath);
            }
            catch (IOException)
            {
                // ignore file index persistence operation, since it is not critical and will redone later.
            }
        }

        internal bool ReadIndexFromDisk(string indexFilePath, bool checkVersionOnly = false)
        {
            lock (this)
            {
                using (var indexStream = new FileStream(indexFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var indexReader = new BinaryReader(indexStream))
                {
                    int version = indexReader.ReadInt32();
                    if (version != SearchIndexEntry.Version)
                        return false;

                    if (checkVersionOnly)
                        return true;

                    //using (new DebugTimer($"Reading index {indexFilePath}"))
                    {
                        indexReader.ReadString(); // Skip
                        var elementCount = indexReader.ReadInt32();
                        var documents = new string[elementCount];
                        for (int i = 0; i < elementCount; ++i)
                            documents[i] = indexReader.ReadString();
                        elementCount = indexReader.ReadInt32();
                        var indexes = new List<SearchIndexEntry>(elementCount);
                        for (int i = 0; i < elementCount; ++i)
                        {
                            var key = indexReader.ReadInt64();
                            var crc = indexReader.ReadInt64();
                            var type = (SearchIndexEntryType)indexReader.ReadInt32();
                            var index = indexReader.ReadInt32();
                            var score = indexReader.ReadInt32();
                            indexes.Add(new SearchIndexEntry(key, crc, type, index, score));
                        }

                        // No need to sort the index, it is already sorted in the file stream.
                        ApplyIndexes(documents, indexes.ToArray());
                    }
                }
            }

            return true;
        }

        internal bool LoadIndexFromDisk(string basePath, bool useThread = false)
        {
            var indexFilePath = getIndexFilePathHandler(basePath);
            if (indexFilePath == null || !File.Exists(indexFilePath))
                return false;

            if (useThread)
            {
                if (!ReadIndexFromDisk(indexFilePath, true))
                    return false;

                var t = new Thread(() => ReadIndexFromDisk(indexFilePath));
                t.Start();
                return t.ThreadState != System.Threading.ThreadState.Unstarted;
            }

            return ReadIndexFromDisk(indexFilePath);
        }

        private void AbortIndexing()
        {
            if (m_IndexReady)
                return;

            m_ThreadAborted = true;
        }

        private void UpdateIndexes(IEnumerable<string> documents, List<SearchIndexEntry> entries, string saveIndexBasePath = null)
        {
            if (entries == null)
                return;

            lock (this)
            {
                m_IndexReady = false;
                var comparer = new SearchIndexComparer();

                try
                {
                    // Sort word indexes to run quick binary searches on them.
                    entries.Sort(comparer);
                    ApplyIndexes(documents, entries.Distinct(comparer).ToArray());
                }
                catch
                {
                    // This can happen while a domain reload is happening.
                    return;
                }

                if (!String.IsNullOrEmpty(saveIndexBasePath))
                    SaveIndexToDisk(saveIndexBasePath);
            }
        }

        private void ApplyIndexes(IEnumerable<string> documents, SearchIndexEntry[] entries)
        {
            m_Documents = documents.ToList();
            m_Indexes = entries;
            m_IndexReady = true;
        }

        private void BuildWordIndexes()
        {
            if (roots.Length == 0)
                return;

            lock (this)
                LoadIndexFromDisk(roots[0].basePath);
                
            int entryStart = 0;
            var documents = new List<string>();
            var wordIndexes = new List<SearchIndexEntry>();

            var baseScore = 0;
            foreach (var r in roots)
            {
                if (m_ThreadAborted)
                    return;

                var rootName = r.baseName;
                var basePath = r.basePath;
                var basePathWithSlash = basePath + "/";

                if (!String.IsNullOrEmpty(rootName))
                    rootName = rootName + "/";

                // Fetch entries to be indexed and compiled.
                documents.AddRange(enumerateRootEntriesHandler(r));
                BuildPartialIndex(wordIndexes, basePathWithSlash, entryStart, documents, baseScore);

                for (int i = entryStart; i < documents.Count; ++i)
                    documents[i] = rootName + documents[i];

                entryStart = documents.Count;
                baseScore = 100;
            }

            UpdateIndexes(documents, wordIndexes, roots[0].basePath);
        }

        private List<SearchIndexEntry> BuildPartialIndex(string basis, int entryStartIndex, IList<string> entries, int baseScore)
        {
            var wordIndexes = new List<SearchIndexEntry>(entries.Count * 3);
            BuildPartialIndex(wordIndexes, basis, entryStartIndex, entries, baseScore);
            return wordIndexes;
        }

        private void BuildPartialIndex(List<SearchIndexEntry> wordIndexes, string basis, int entryStartIndex, IList<string> entries, int baseScore)
        {
            int entryCount = entries.Count - entryStartIndex;
            for (int i = entryStartIndex; i != entries.Count; ++i)
            {
                if (m_ThreadAborted)
                    break;

                if (String.IsNullOrEmpty(entries[i]))
                    continue;

                // Reformat entry to have them all uniformized.
                if (!String.IsNullOrEmpty(basis))
                    entries[i] = entries[i].Replace('\\', '/').Replace(basis, "");

                var path = entries[i];
                if (skipEntryHandler(path))
                    continue;

                FetchEntries(path, i, wordIndexes, baseScore);
            }
        }

        private bool NumberCompare(SearchIndexOperator op, double d1, double d2)
        {
            if (op == SearchIndexOperator.Equal)
                return d1 == d2;
            if (op == SearchIndexOperator.Contains)
                return UnityEngine.Mathf.Approximately((float)d1, (float)d2);
            if (op == SearchIndexOperator.Greater)
                return d1 > d2;
            if (op == SearchIndexOperator.GreaterOrEqual)
                return d1 >= d2;
            if (op == SearchIndexOperator.Less)
                return d1 < d2;
            if (op == SearchIndexOperator.LessOrEqual)
                return d1 <= d2;

            throw new NotImplementedException($"Search index compare strategy {op} for number not defined.");
        }

        private bool Rewind(int foundIndex, SearchIndexEntry term, SearchIndexOperator op)
        {
            if (foundIndex <= 0)
                return false;

            var prevEntry =  m_Indexes[foundIndex - 1];
            if (prevEntry.crc != term.crc || prevEntry.type != term.type)
                return false;

            if (term.type == SearchIndexEntryType.Number)
                return NumberCompare(op, prevEntry.number, term.number);

            return prevEntry.key == term.key;
        }

        private bool Advance(int foundIndex, SearchIndexEntry term, SearchIndexOperator op)
        {
            if (foundIndex < 0 || foundIndex >= m_Indexes.Length || 
                    m_Indexes[foundIndex].crc != term.crc || m_Indexes[foundIndex].type != term.type)
                return false;

            if (term.type == SearchIndexEntryType.Number)
                return NumberCompare(op, m_Indexes[foundIndex].number, term.number);

            return m_Indexes[foundIndex].key == term.key;
        }

        private bool Lower(ref int foundIndex, SearchIndexEntry term, SearchIndexOperator op)
        {
            if (op == SearchIndexOperator.Less || op == SearchIndexOperator.LessOrEqual)
            {
                var cont = !Advance(foundIndex, term, op);
                if (cont)
                    foundIndex--;
                return IsIndexValid(foundIndex, term.key, term.type) && cont;
            }

            {
                var cont = Rewind(foundIndex, term, op);
                if (cont)
                    foundIndex--;
                return cont;
            }
        }

        private bool Upper(ref int foundIndex, SearchIndexEntry term, SearchIndexOperator op)
        {
            if (op == SearchIndexOperator.Less || op == SearchIndexOperator.LessOrEqual)
            {
                var cont = Rewind(foundIndex, term, op);
                if (cont)
                    foundIndex--;
                return IsIndexValid(foundIndex, term.crc, term.type) && cont;
            }

            return Advance(++foundIndex, term, op);
        }

        private bool IsIndexValid(int foundIndex, long crc, SearchIndexEntryType type)
        {
            return foundIndex >= 0 && foundIndex < m_Indexes.Length && m_Indexes[foundIndex].crc == crc && m_Indexes[foundIndex].type == type;
        }

        struct IndexRange
        {
            public readonly int start;
            public readonly int end;

            public IndexRange(int s, int e)
            {
                start = s;
                end = e;
            }

            public bool valid => start != -1;

            public static IndexRange Invalid = new IndexRange(-1, -1);
        }

        private IndexRange FindRange(SearchIndexEntry term, SearchIndexComparer comparer)
        {
            // Find a first match in the sorted indexes.
            int foundIndex = Array.BinarySearch(m_Indexes, term, comparer);
            if (foundIndex < 0 && comparer.op != SearchIndexOperator.Contains && comparer.op != SearchIndexOperator.Equal)
            {
                // Potential range insertion, only used for not exact matches
                foundIndex = (-foundIndex) - 1;
            }

            if (!IsIndexValid(foundIndex, term.crc, term.type))
                return IndexRange.Invalid;

            // Rewind to first element
            while (Lower(ref foundIndex, term, comparer.op))
                ;

            if (!IsIndexValid(foundIndex, term.crc, term.type))
                return IndexRange.Invalid;

            int startRange = foundIndex;

            // Advance to last matching element
            while (Upper(ref foundIndex, term, comparer.op))
                ;

            return new IndexRange(startRange, foundIndex);
        }

        readonly struct RangeSet : IEquatable<RangeSet>
        {
            public readonly SearchIndexEntryType type;
            public readonly long crc;

            public RangeSet(SearchIndexEntryType type, long crc)
            {
                this.type = type;
                this.crc = crc;
            }

            public override int GetHashCode() => (type, crc).GetHashCode();
            public override bool Equals(object other) => other is RangeSet l && Equals(l);
            public bool Equals(RangeSet other) => type == other.type && crc == other.crc;
        }

        private IndexRange FindTypeRange(int hitIndex, SearchIndexEntry term)
        {
            if (term.type == SearchIndexEntryType.Word)
            {
                if (m_Indexes[0].type != SearchIndexEntryType.Word || m_Indexes[hitIndex].type != SearchIndexEntryType.Word)
                    return IndexRange.Invalid; // No words

                IndexRange range;
                var rangeSet = new RangeSet(term.type, 0);
                if (m_FixedRanges.TryGetValue(rangeSet, out range))
                    return range;

                int endRange = hitIndex;
                while (m_Indexes[endRange+1].type == SearchIndexEntryType.Word)
                    endRange++;

                range = new IndexRange(0, endRange);
                m_FixedRanges[rangeSet] = range;
                return range;
            }
            else if (term.type == SearchIndexEntryType.Property || term.type == SearchIndexEntryType.Number)
            {
                if (m_Indexes[hitIndex].type != SearchIndexEntryType.Property)
                    return IndexRange.Invalid;

                IndexRange range;
                var rangeSet = new RangeSet(term.type, term.crc);
                if (m_FixedRanges.TryGetValue(rangeSet, out range))
                    return range;

                int startRange = hitIndex, prev = hitIndex - 1;
                while (prev >= 0 && m_Indexes[prev].type == SearchIndexEntryType.Property && m_Indexes[prev].crc == term.crc)
                    startRange = prev--;

                var indexCount = m_Indexes.Length;
                int endRange = hitIndex, next = hitIndex + 1;
                while (next < indexCount && m_Indexes[next].type == SearchIndexEntryType.Property && m_Indexes[next].crc == term.crc)
                    endRange = next++;

                range = new IndexRange(startRange, endRange);
                m_FixedRanges[rangeSet] = range;
                return range;
            }
            
            return IndexRange.Invalid;
        }

        private IEnumerable<SearchPatternMatch> SearchRange(
                int foundIndex, SearchIndexEntry term, 
                int maxScore, SearchIndexComparer comparer,
                HashSet<int> entryIndexes, int limit)
        {
            if (foundIndex < 0 && comparer.op != SearchIndexOperator.Contains && comparer.op != SearchIndexOperator.Equal)
            {
                // Potential range insertion, only used for not exact matches
                foundIndex = (-foundIndex) - 1;
            }

            if (!IsIndexValid(foundIndex, term.crc, term.type))
                return Enumerable.Empty<SearchPatternMatch>();

            // Rewind to first element
            while (Lower(ref foundIndex, term, comparer.op))
                ;

            if (!IsIndexValid(foundIndex, term.crc, term.type))
                return Enumerable.Empty<SearchPatternMatch>();

            var matches = new List<SearchPatternMatch>();
            bool findAll = entryIndexes == null || entryIndexes.Count == 0;
            do
            {
                var indexEntry = m_Indexes[foundIndex];
                bool intersects = findAll || entryIndexes.Contains(indexEntry.index);
                if (intersects && indexEntry.score < maxScore)
                {
                    if (findAll && entryIndexes != null)
                        entryIndexes.Add(indexEntry.index);

                    if (term.type == SearchIndexEntryType.Number)
                        matches.Add(new SearchPatternMatch(indexEntry.index, indexEntry.score + (int)Math.Abs(term.number - indexEntry.number)));
                    else
                        matches.Add(new SearchPatternMatch(indexEntry.index, indexEntry.score));

                    if (matches.Count >= limit)
                        return matches;
                }

                // Advance to last matching element
            } while (Upper(ref foundIndex, term, comparer.op));

            return matches;
        }

        private IEnumerable<SearchPatternMatch> SearchIndexes(
                long key, long crc, SearchIndexEntryType type, int maxScore, 
                SearchIndexComparer comparer, HashSet<int> entryIndexes, int limit = int.MaxValue)
        {
            // Find a first match in the sorted indexes.
            int foundIndex = Array.BinarySearch(m_Indexes, new SearchIndexEntry(key, crc, type), comparer);
            return SearchRange(foundIndex, new SearchIndexEntry(key, crc, type), maxScore, comparer, entryIndexes, limit);
        }

        protected void UpdateIndexWithNewContent(string[] updated, string[] removed, string[] moved)
        {
            lock (this)
            {
                Start();
                foreach (var id in updated.Concat(moved).Distinct())
                {
                    var documentIndex = AddDocument(id, true);
                    FetchEntries(id, documentIndex, m_BatchIndexes, 0);
                }
                Finish(true, removed);
            }
        }

        private string[] ParseQuery(string query)
        {
            return Regex.Matches(query, @"([\!]*([\""](.+?)[\""]|[^\s_\/]))+").Cast<Match>()
                .Select(m => m.Value.Replace("\"", "").ToLowerInvariant())
                .Where(t => t.Length > 0)
                .OrderBy(t => -t.Length)
                .ToArray();
        }

        struct IndexerThreadScope : IDisposable
        {
            private bool m_Disposed;
            private readonly AssemblyReloadEvents.AssemblyReloadCallback m_AbortHandler;

            public IndexerThreadScope(AssemblyReloadEvents.AssemblyReloadCallback abortHandler)
            {
                m_Disposed = false;
                m_AbortHandler = abortHandler;
                AssemblyReloadEvents.beforeAssemblyReload -= abortHandler;
                AssemblyReloadEvents.beforeAssemblyReload += abortHandler;
            }

            public void Dispose()
            {
                if (m_Disposed)
                    return;
                AssemblyReloadEvents.beforeAssemblyReload -= m_AbortHandler;
                m_Disposed = true;
            }
        }
    }

    class SearchIndexerQuery : SearchIndexerQuery<SearchEntryResult> {}

    class SearchIndexerQuery<T> : IQueryHandler<T, SearchIndexerQuery<T>.EvalHandler, object>
    {
        private QueryGraph graph { get; set; }

        public delegate EvalResult EvalHandler(EvalHandlerArgs args);

        public readonly struct EvalHandlerArgs
        {
            public readonly string name;
            public readonly object value;
            public readonly bool exclude;
            public readonly SearchIndexOperator op;

            public readonly IEnumerable<T> andSet;
            public readonly IEnumerable<T> orSet;

            public readonly object payload;

            public EvalHandlerArgs(string name, object value, SearchIndexOperator op, bool exclude,
                IEnumerable<T> andSet, IEnumerable<T> orSet, object payload)
            {
                this.name = name;
                this.value = value;
                this.op = op;
                this.exclude = exclude;
                this.andSet = andSet;
                this.orSet = orSet;
                this.payload = payload;
            }
        }

        public readonly struct EvalResult
        {
            public readonly bool combined;
            public readonly IEnumerable<T> results;

            public static EvalResult None = new EvalResult(false, null);

            public EvalResult(bool combined, IEnumerable<T> results = null)
            {
                this.combined = combined;
                this.results = results;
            }

            public static EvalResult Combined(IEnumerable<T> results)
            {
                return new EvalResult(true, results);
            }

            public static void Print(EvalHandlerArgs args, IEnumerable<T> results = null, HashSet<int> subset = null, double elapsedTime = -1)
            {
                var combineString = GetCombineString(args.andSet, args.orSet);
                var elapsedTimeString = "";
                if (elapsedTime > 0)
                    elapsedTimeString = $"{elapsedTime:F2} ms";
                UnityEngine.Debug.Log($"Eval -> {combineString} | op:{args.op,14} | exclude:{args.exclude,7} | " +
                    $"[{args.name,4}, {args.value,8}] | i:{GetAddress(args.andSet)} | a:{GetAddress(args.orSet)} | " +
                    $"h:{GetAddress(subset)} | results:{GetAddress(results)} | " + elapsedTimeString);
            }

            private static string GetCombineString(object inputSet, object addedSet)
            {
                var combineString = "  -  ";
                if (addedSet != null && inputSet != null)
                    combineString = "\u2229\u222A";
                else if (addedSet != null)
                    combineString = " \u2229 ";
                else if (inputSet != null)
                    combineString = " \u222A ";
                return combineString;
            }

            private static int GetHandle(object obj)
            {
                return obj.GetHashCode();
            }

            private static string GetAddress(object obj)
            {
                if (obj == null)
                    return "0x00000000";
                var addr = GetHandle(obj);
                return $"0x{addr.ToString("X8")}";
            }

            private static string GetAddress(ICollection<T> list)
            {
                if (list == null)
                    return "0x00000000";
                var addr = GetHandle(list);
                return $"({list.Count}) 0x{addr.ToString("X8")}";
            }
        }

        public void Initialize(QueryEngine<T> engine, QueryGraph graph)
        {
            this.graph = graph;
            graph.Optimize(true, true);
        }

        public IEnumerable<T> Eval(EvalHandler handler, object payload)
        {
            var root = BuildInstruction(graph.root, handler, payload, false);
            root.Execute(Combine.None);
            if (root.results == null)
                return new List<T>();
            else
                return root.results.Distinct();
        }

        private static Instruction BuildInstruction(IQueryNode node, EvalHandler eval, object payload, bool not)
        {
            switch (node.type)
            {
                case QueryNodeType.And:
                {
                    Assert.IsFalse(node.leaf, "And node cannot be leaf.");
                    if (!not)
                        return new AndInstruction(node, eval, payload, not);
                    else
                        return new OrInstruction(node, eval, payload, not);
                }
                case QueryNodeType.Or:
                {
                    Assert.IsFalse(node.leaf, "Or node cannot be leaf.");
                    if (!not)
                        return new OrInstruction(node, eval, payload, not);
                    else
                        return new AndInstruction(node, eval, payload, not);
                }
                case QueryNodeType.Not:
                {
                    Assert.IsFalse(node.leaf, "Not node cannot be leaf.");
                    Instruction instruction = BuildInstruction(node.children[0], eval, payload, !not);
                    return instruction;
                }
                case QueryNodeType.Filter:
                case QueryNodeType.Search:
                {
                    Assert.IsNotNull(node);
                    return new ResultInstruction(node, eval, payload, not);
                }
            }

            return null;
        }

        private static SearchIndexOperator ParseOperatorToken(string token)
        {
            switch (token)
            {
                case ":": return SearchIndexOperator.Contains;
                case "=": return SearchIndexOperator.Equal;
                case ">": return SearchIndexOperator.Greater;
                case ">=": return SearchIndexOperator.GreaterOrEqual;
                case "<": return SearchIndexOperator.Less;
                case "<=": return SearchIndexOperator.LessOrEqual;
                case "!=": return SearchIndexOperator.NotEqual;
            }
            return SearchIndexOperator.None;
        }

        private abstract class Instruction
        {
            public object payload;
            public IEnumerable<T> andSet; //not null for a AND
            public IEnumerable<T> orSet; //not null for a OR
            public IEnumerable<T> results;
            protected EvalHandler eval;
            public abstract bool Execute(Combine combine);
        }

        private abstract class OperandInstruction : Instruction
        {
            public Instruction leftInstruction;
            public Instruction rightInstruction;
            protected Combine combine;

            public OperandInstruction(IQueryNode node, EvalHandler eval, object payload, bool not)
            {
                leftInstruction = BuildInstruction(node.children[0], eval, payload, not);
                rightInstruction = BuildInstruction(node.children[1], eval, payload, not);
                this.eval = eval;
                this.payload = payload;
            }

            public override bool Execute(Combine combine)
            {
                // Pass the inputSet from the parent
                if (andSet != null)
                {
                    leftInstruction.andSet = andSet;
                    //rightInstruction.inputSet = inputSet;
                }
                // Pass the addedSet from the parent (only added by the right instruction)
                if (orSet != null)
                {
                    //leftInstruction.addedSet = addedSet;
                    rightInstruction.orSet = orSet;
                }
                bool combinedLeft = leftInstruction.Execute(combine);

                UpdateRightInstruction(leftInstruction.results);

                bool combinedRight = rightInstruction.Execute(this.combine);

                UpdateOutputSet(combinedLeft, combinedRight, leftInstruction.results, rightInstruction.results);
                return IsCombineHandled(combinedLeft, combinedRight);
            }

            internal abstract bool IsCombineHandled(bool combinedLeft, bool combinedRight);
            internal abstract void UpdateOutputSet(bool combinedLeft, bool combinedRight, IEnumerable<T> leftReturn, IEnumerable<T> rightReturn);
            internal abstract void UpdateRightInstruction(IEnumerable<T> leftReturn);
        }

        private class AndInstruction : OperandInstruction
        {
            public AndInstruction(IQueryNode node, EvalHandler eval, object dataSet, bool not) : base(node, eval, dataSet, not)
            {
                combine = Combine.Intersection;
            }

            internal override bool IsCombineHandled(bool combinedLeft, bool combinedRight)
            {
                return combinedLeft; // For a AND the inputSet is used by the left
            }

            internal override void UpdateOutputSet(bool combinedLeft, bool combinedRight, IEnumerable<T> leftReturn, IEnumerable<T> rightReturn)
            {
                if (!combinedRight)
                {
                    var result = eval(new EvalHandlerArgs(null, null, SearchIndexOperator.None, false, leftReturn, rightReturn, payload));
                    results = result.results;
                    if (!result.combined)
                    {
                        results = leftReturn.Intersect(rightReturn).ToList();
                    }
                }
                else
                    results = rightReturn;
            }

            internal override void UpdateRightInstruction(IEnumerable<T> leftReturn)
            {
                rightInstruction.andSet = leftReturn;
            }
        }

        private class OrInstruction : OperandInstruction
        {
            public OrInstruction(IQueryNode node, EvalHandler eval, object payload, bool not) : base(node, eval, payload, not)
            {
                combine = Combine.Union;
            }

            internal override bool IsCombineHandled(bool combinedLeft, bool combinedRight)
            {
                return combinedRight; // For a OR the addedSet is used by the right
            }

            internal override void UpdateOutputSet(bool combinedLeft, bool combinedRight, IEnumerable<T> leftReturn, IEnumerable<T> rightReturn)
            {
                if (!combinedRight)
                {
                    int rightReturnOriginalCount = rightReturn.Count();
                    int leftReturnOriginalCount = leftReturn.Count();
                    var result = eval(new EvalHandlerArgs(null, null, SearchIndexOperator.None, false, leftReturn, rightReturn, payload));
                    results = result.results;
                    if (!result.combined)
                    {
                        results = leftReturn.Union(rightReturn);
                    }
                }
                else
                    results = rightReturn;
            }

            internal override void UpdateRightInstruction(IEnumerable<T> leftReturn)
            {
                // Pass the input Set from the parent
                if (andSet != null)
                    rightInstruction.andSet = andSet;

                // might be wrong to modify that list because it's used somewhere else
                if (rightInstruction.orSet == null)
                    rightInstruction.orSet = leftReturn;
                else
                {
                    rightInstruction.orSet = rightInstruction.orSet.Union(leftReturn);
                }
            }
        }

        private class ResultInstruction : Instruction
        {
            public readonly IQueryNode node;
            public readonly bool exclude;

            public ResultInstruction(IQueryNode node, EvalHandler eval, object payload, bool exclude)
            {
                this.node = node;
                this.eval = eval;
                this.payload = payload;
                this.exclude = exclude;
            }

            public override bool Execute(Combine combine)
            {
                string searchName = null;
                object searchValue = null;
                var searchOperator = SearchIndexOperator.None;
                if (node is FilterNode filterNode)
                {
                    searchName = filterNode.filterOperation.filterName;
                    searchValue = filterNode.filterOperation.filterValue;
                    searchOperator = ParseOperatorToken(filterNode.filterOperation.filterOperator.token);
                }
                else if (node is SearchNode searchNode)
                {
                    searchName = null;
                    searchValue = searchNode.searchValue;
                    searchOperator = searchNode.exact ? SearchIndexOperator.Equal : SearchIndexOperator.Contains;
                }

                var result = eval(new EvalHandlerArgs(searchName, searchValue, searchOperator, exclude, andSet, orSet, payload));
                results = result.results;
                return result.combined;
            }
        }
    }
}
