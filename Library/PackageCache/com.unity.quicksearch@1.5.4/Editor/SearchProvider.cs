using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace Unity.QuickSearch
{
    /// <summary>
    /// Defines at set of options that indicates to the search provider how the preview should be fetched.
    /// </summary>
    [Flags]
    public enum FetchPreviewOptions
    {
        /// <summary>
        /// No options are defined.
        /// </summary>
        None = 0,

        /// <summary>
        /// Indicates that the provider should generate a 2D preview.
        /// </summary>
        Preview2D = 1,

        /// <summary>
        /// Indicates that the provider should generte a 3D preview.
        /// </summary>
        Preview3D = 1 << 1,

        /// <summary>
        /// Indicate that the preview size should be around 128x128.
        /// </summary>
        Normal    = 1 << 10,

        /// <summary>
        /// Indicates that the preview resolution should be higner than 256x256.
        /// </summary>
        Large     = 1 << 11
    }

    /// <summary>
    /// The name entry holds a name and an identifier at once.
    /// </summary>
    [DebuggerDisplay("{displayName} ({id})")]
    public class NameEntry
    {
        /// <summary>
        /// Construct a new name identifier
        /// </summary>
        /// <param name="id"></param>
        /// <param name="displayName"></param>
        public NameEntry(string id, string displayName = null)
        {
            this.id = id;
            this.displayName = displayName ?? id;
        }

        /// <summary> Unique name for an object </summary>
        public string id;

        /// <summary> Display name (use by UI) </summary>
        public string displayName;

        /// <summary>
        /// Indicates if the entry is enabled
        /// </summary>
        public bool isEnabled;
    }

    /// <summary>
    /// SearchProvider manages search for specific type of items and manages thumbnails, description and subfilters, etc.
    /// </summary>
    [DebuggerDisplay("{name.id}")]
    public class SearchProvider
    {
        internal const int k_RecentUserScore = -99;

        public SearchProvider(string id, string displayName = null)
        {
            active = true;
            name = new NameEntry(id, displayName);
            actions = new List<SearchAction>();
            fetchItems = (context, items, provider) => null;
            fetchThumbnail = (item, context) => item.thumbnail ?? Icons.quicksearch;
            fetchPreview = null;
            fetchLabel = (item, context) => item.label ?? item.id ?? String.Empty;
            fetchDescription = (item, context) => item.description ?? String.Empty;
            subCategories = new List<NameEntry>();
            priority = 100;
            fetchTimes = new double[10];
            fetchTimeWriteIndex = 0;
            showDetails = false;
        }

        /// <summary>
        /// Helper function to create a new search item for the current provider.
        /// </summary>
        /// <param name="id">Unique id of the search item. This is used to remove duplicates to the user view.</param>
        /// <param name="score">Score of the search item. The score is used to sort all the result per provider. Lower score are shown first.</param>
        /// <param name="label">The search item label is displayed on the first line of the search item UI widget.</param>
        /// <param name="description">The search item description is displayed on the second line of the search item UI widget.</param>
        /// <param name="thumbnail">The search item thumbnail is displayed left to the item label and description as a preview.</param>
        /// <param name="data">User data used to recover more information about a search item. Generally used in fetchLabel, fetchDescription, etc.</param>
        /// <returns>The newly created search item attached to the current search provider.</returns>
        public SearchItem CreateItem(string id, int score, string label, string description, Texture2D thumbnail, object data)
        {
            // If the user searched that item recently,
            // let give it a good score so it gets sorted first.
            if (SearchService.IsRecent(id))
                score = Math.Min(k_RecentUserScore, score);

            #if false // Debug sorting
            description = $"DEBUG: id={id} - label={label} - description={description} - thumbnail={thumbnail} - data={data}";
            label = $"{label ?? id} ({score})";
            #endif

            return new SearchItem(id)
            {
                score = score,
                label = label,
                description = description,
                descriptionFormat = SearchItemDescriptionFormat.Highlight | SearchItemDescriptionFormat.Ellipsis,
                thumbnail = thumbnail,
                provider = this,
                data = data
            };
        }

        /// <summary>
        /// Create a Search item that will be bound to the SeaechProvider.
        /// </summary>
        /// <param name="id">Unique id of the search item. This is used to remove duplicates to the user view.</param>
        /// <param name="label">The search item label is displayed on the first line of the search item UI widget.</param>
        /// <param name="description">The search item description is displayed on the second line of the search item UI widget.</param>
        /// <param name="thumbnail">The search item thumbnail is displayed left to the item label and description as a preview.</param>
        /// <param name="data">User data used to recover more information about a search item. Generally used in fetchLabel, fetchDescription, etc.</param>
        /// <returns>New SearchItem</returns>
        public SearchItem CreateItem(string id, string label = null, string description = null, Texture2D thumbnail = null, object data = null)
        {
            return CreateItem(id, 0, label, description, thumbnail, data);
        }

        /// <summary>
        /// Helper function to match a string against the SearchContext. This will try to match the search query against each tokens of content (similar to the AddComponent menu workflow)
        /// </summary>
        /// <param name="context">Search context containing the searchQuery that we try to match.</param>
        /// <param name="content">String content that will be tokenized and use to match the search query.</param>
        /// <param name="useLowerTokens">Perform matching ignoring casing.</param>
        /// <returns>Has a match occurred.</returns>
        public static bool MatchSearchGroups(SearchContext context, string content, bool useLowerTokens = false)
        {
            return MatchSearchGroups(context.searchQuery, context.searchWords, content, out _, out _,
                                     useLowerTokens ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
        }

        internal void RecordFetchTime(double t)
        {
            fetchTimes[fetchTimeWriteIndex] = t;
            fetchTimeWriteIndex = SearchService.Wrap(fetchTimeWriteIndex + 1, fetchTimes.Length);
        }

        private static bool MatchSearchGroups(string searchContext, string[] tokens, string content, out int startIndex, out int endIndex, StringComparison sc = StringComparison.OrdinalIgnoreCase)
        {
            startIndex = endIndex = -1;
            if (String.IsNullOrEmpty(content))
                return false;

            if (string.IsNullOrEmpty(searchContext))
                return false;

            if (searchContext == content)
            {
                startIndex = 0;
                endIndex = content.Length - 1;
                return true;
            }

            // Each search group is space separated
            // Search group must match in order and be complete.
            var searchGroups = tokens;
            var startSearchIndex = 0;
            foreach (var searchGroup in searchGroups)
            {
                if (searchGroup.Length == 0)
                    continue;

                startSearchIndex = content.IndexOf(searchGroup, startSearchIndex, sc);
                if (startSearchIndex == -1)
                {
                    return false;
                }

                startIndex = startIndex == -1 ? startSearchIndex : startIndex;
                startSearchIndex = endIndex = startSearchIndex + searchGroup.Length - 1;
            }

            return startIndex != -1 && endIndex != -1;
        }

        /// <summary> Average time it takes to query that provider.</summary>
        public double avgTime
        {
            get
            {
                double total = 0.0;
                int validTimeCount = 0;
                foreach (var t in fetchTimes)
                {
                    if (t > 0.0)
                    {
                        total += t;
                        validTimeCount++;
                    }
                }

                if (validTimeCount == 0)
                    return 0.0;

                return total / validTimeCount;
            }
        }

        /// <summary> Unique id of the provider.</summary>
        public NameEntry name;
        
        /// <summary>
        /// Indicates if the provider is active or not. Inactive providers are completely ignored by the search service. The active state can be toggled in the search settings.
        /// </summary>
        public bool active;
        
        /// <summary> Text token use to "filter" a provider (ex:  "me:", "p:", "s:")</summary>
        public string filterId;
        
        /// <summary> This provider is only active when specified explicitly using his filterId</summary>
        public bool isExplicitProvider;
        
        /// <summary> Indicates if the provider can show additional details or not.</summary>
        public bool showDetails;

        /// <summary> Handler used to fetch and format the label of a search item.</summary>
        public Func<SearchItem, SearchContext, string> fetchLabel;
        
        /// <summary>
        /// Handler to provider an async description for an item. Will be called when the item is about to be displayed.
        /// Allows a plugin provider to only fetch long description when they are needed.
        /// </summary>
        public Func<SearchItem, SearchContext, string> fetchDescription;
        
        /// <summary>
        /// Handler to provider an async thumbnail for an item. Will be called when the item is about to be displayed.
        /// Compared to preview a thumbnail should be small and returned as fast as possible. Use fetchPreview if you want to generate a preview that is bigger and slower to return.
        /// Allows a plugin provider to only fetch/generate preview when they are needed.
        /// </summary>
        public Func<SearchItem, SearchContext, Texture2D> fetchThumbnail;
        
        /// <summary>
        /// Similar to fetchThumbnail, fetchPreview usually returns a bigger preview. The QuickSearch UI will progressively show one preview each frame,
        /// preventing the UI to block if many preview needs to be generated at the same time.
        /// </summary>
        public Func<SearchItem, SearchContext, Vector2, FetchPreviewOptions, Texture2D> fetchPreview;
        
        /// <summary> If implemented, it means the item supports drag. It is up to the SearchProvider to properly setup the DragAndDrop manager.</summary>
        public Action<SearchItem, SearchContext> startDrag;
        
        /// <summary> Called when the selection changed and can be tracked.</summary>
        public Action<SearchItem, SearchContext> trackSelection;
        
        /// <summary> MANDATORY: Handler to get items for a given search context.
        /// The return value is an object that can be of type IEnumerable or IEnumerator.
        /// The enumeration of those objects should return SearchItems.
        /// </summary>
        public Func<SearchContext, List<SearchItem>, SearchProvider, object> fetchItems;
        
        /// <summary> Provider can return a list of words that will help the user complete his search query</summary>
        public Action<SearchContext, string, List<string>> fetchKeywords;
        
        /// <summary> List of subfilters that will be visible in the FilterWindow for a given SearchProvider (see AssetProvider for an example).</summary>
        public List<NameEntry> subCategories;
        
        /// <summary> Called when the QuickSearchWindow is opened. Allow the Provider to perform some caching.</summary>
        public Action onEnable;
        
        /// <summary> Called when the QuickSearchWindow is closed. Allow the Provider to release cached resources.</summary>
        public Action onDisable;
        
        /// <summary> Hint to sort the Provider. Affect the order of search results and the order in which provider are shown in the FilterWindow.</summary>
        public int priority;
        
        /// <summary> Called when quicksearch is invoked in "contextual mode". If you return true it means the provider is enabled for this search context.</summary>
        public Func<bool> isEnabledForContextualSearch;

        // INTERNAL
        internal List<SearchAction> actions;
        internal double[] fetchTimes;
        internal double loadTime;
        internal double enableTime;
        internal int fetchTimeWriteIndex;
    }
}