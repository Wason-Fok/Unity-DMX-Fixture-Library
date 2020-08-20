using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEditor;

namespace Unity.QuickSearch
{
    /// <summary>
    /// The search context contains many fields to process a search query.
    /// </summary>
    [DebuggerDisplay("{searchQuery}")]
    public class SearchContext
    {
        /// <summary>
        /// Raw search text (i.e. what is in the search text box)
        /// </summary>
        public string searchText { get; internal set; }

        /// <summary>
        /// Processed search query (no filterId, no textFilters)
        /// </summary>
        public string searchQuery { get; internal set; }
        
        /// <summary>
        /// Search query tokenized by words. All text filters are discarded and all words are lower cased.
        /// </summary>
        public string[] searchWords { get; internal set; }

        /// <summary>
        /// Returns a phrase that contains only words separated by spaces
        /// </summary>
        internal string searchPhrase
        {
            get
            {
                if (m_CachedPhrase == null && searchWords.Length > 0)
                    m_CachedPhrase = String.Join(" ", searchWords).Trim();
                return m_CachedPhrase ?? String.Empty;
            }
        }
        private string m_CachedPhrase;
        
        /// <summary>
        /// All tokens containing a colon (':')
        /// </summary>
        public string[] textFilters { get; internal set; }

        /// <summary>
        /// All sub categories related to this provider and their enabled state.
        /// </summary>
        public List<NameEntry> categories { get; internal set; }

        /// <summary>
        /// Mark the number of item found after running the search.
        /// </summary>
        public int totalItemCount { get; internal set; }

        /// <summary>
        /// Editor window that initiated the search.
        /// </summary>
        public EditorWindow focusedWindow { get; internal set; }

        /// <summary>
        /// Indicates if the search should return results as many as possible.
        /// </summary>
        public bool wantsMore { get; internal set; }

        /// <summary>
        /// Indicates if the current search tries to execute a specific action on the search results.
        /// </summary>
        public bool isActionQuery { get; internal set; }

        /// <summary>
        /// The search action id to be executed.
        /// </summary>
        public string actionQueryId { get; internal set; }

        /// <summary>
        /// Search view holding and presenting the search results.
        /// </summary>
        internal ISearchView searchView;

        /// <summary>
        /// Checks if a specific filter or sub filter (i.e. category) is enabled for the current search.
        /// </summary>
        /// <param name="filterId"></param>
        /// <returns></returns>
        internal bool IsFilterEnabled(string filterId)
        {
            return categories.Any(c => c.isEnabled && c.id == filterId);
        }

        /// <summary>
        /// Default empty search context.
        /// </summary>
        static public readonly SearchContext Empty = new SearchContext {searchText = String.Empty, searchQuery = String.Empty};
    }
}