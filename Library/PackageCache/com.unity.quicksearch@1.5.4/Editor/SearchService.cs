//#define QUICKSEARCH_DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using JetBrains.Annotations;

namespace Unity.QuickSearch
{
    /// <summary>
    /// Attribute used to declare a static method that will create a new search provider at load time.
    /// </summary>
    public class SearchItemProviderAttribute : Attribute
    {
    }

    /// <summary>
    /// Attribute used to declare a static method that define new actions for specific search providers.
    /// </summary>
    public class SearchActionsProviderAttribute : Attribute
    {
    }

    /// <summary>
    /// Search view interface used by the search context to execute a few UI operations.
    /// </summary>
    public interface ISearchView
    {
        /// <summary>
        /// Sets the search query text.
        /// </summary>
        /// <param name="searchText">Text to be displayed in the search view.</param>
        void SetSearchText(string searchText);

        /// <summary>
        /// Open the associated filter window.
        /// </summary>
        void PopFilterWindow();

        /// <summary>
        /// Make sure the search is now focused.
        /// </summary>
        void Focus();

        /// <summary>
        /// Triggers a refresh of the search view, re-fetching all the search items from enabled search providers.
        /// </summary>
        void Refresh();
    }

    /// <summary>
    /// Principal Quick Search API to initiate searches and fetch results.
    /// </summary>
    public static class SearchService
    {
        internal const string prefKey = "quicksearch";
        // Global settings
        const string k_FilterPrefKey = prefKey + ".filters";
        const string k_DefaultActionPrefKey = prefKey + ".defaultactions.";
        // Session settings
        const string k_LastSearchPrefKey = "last_search";
        const string k_RecentsPrefKey = "recents";

        const string k_ActionQueryToken = ">";

        private static string s_LastSearch;
        private static int s_RecentSearchIndex = -1;
        private const int k_MaxFetchTimeMs = 100;
        private static List<int> s_UserScores = new List<int>();
        private static HashSet<int> s_SortedUserScores = new HashSet<int>();
        private static Dictionary<string, AsyncSearchSession> s_SearchSessions = new Dictionary<string, AsyncSearchSession>();

        internal static List<string> s_RecentSearches = new List<string>(10);
        internal static Dictionary<string, string> TextFilterIds { get; private set; }
        internal static Dictionary<string, List<string>> ActionIdToProviders { get; private set; }
        internal static SearchFilter OverrideFilter { get; private set; }

        internal static string LastSearch
        {
            get => s_LastSearch;
            set
            {
                if (value == s_LastSearch)
                    return;
                s_LastSearch = value;
                if (String.IsNullOrEmpty(value))
                    return;
                s_RecentSearchIndex = 0;
                s_RecentSearches.Insert(0, value);
                if (s_RecentSearches.Count > 10)
                    s_RecentSearches.RemoveRange(10, s_RecentSearches.Count - 10);
                s_RecentSearches = s_RecentSearches.Distinct().ToList();
            }
        }

        /// <summary>
        /// Returns the current search filter being applied.
        /// </summary>
        public static SearchFilter Filter { get; private set; }

        /// <summary>
        /// Returns the list of all providers (active or not)
        /// </summary>
        public static List<SearchProvider> Providers { get; private set; }

        /// <summary>
        /// Returns the list of providers sorted by priority.
        /// </summary>
        public static IEnumerable<SearchProvider> OrderedProviders
        {
            get
            {
                return Providers.OrderBy(p => p.priority + (p.isExplicitProvider ? 100000 : 0));
            }
        }

        static SearchService()
        {
            Refresh();
        }

        /// <summary>
        /// Sets the last activated search item.
        /// </summary>
        /// <param name="item">The item to be marked as been used recently.</param>
        public static void SetRecent(SearchItem item)
        {
            int itemKey = item.id.GetHashCode();
            s_UserScores.Add(itemKey);
            s_SortedUserScores.Add(itemKey);
        }

        /// <summary>
        /// Checks if a search item has been used recently.
        /// </summary>
        /// <param name="id">Unique ID of the search item.</param>
        /// <returns>True if the item was used recently or false otherwise.</returns>
        public static bool IsRecent(string id)
        {
            return s_SortedUserScores.Contains(id.GetHashCode());
        }

        /// <summary>
        /// Returns the data of a search provider given its ID.
        /// </summary>
        /// <param name="providerId">Unique ID of the provider</param>
        /// <returns>The matching provider</returns>
        public static SearchProvider GetProvider(string providerId)
        {
            return Providers.Find(p => p.name.id == providerId);
        }

        /// <summary>
        /// Returns the search action data for a given provider and search action id.
        /// </summary>
        /// <param name="provider">Provider to lookup</param>
        /// <param name="actionId">Unique action ID within the provider.</param>
        /// <returns>The matching action</returns>
        public static SearchAction GetAction(SearchProvider provider, string actionId)
        {
            if (provider == null)
                return null;
            return provider.actions.Find(a => a.Id == actionId);
        }

        /// <summary>
        /// Clears everything and reloads all search providers.
        /// </summary>
        /// <remarks>Use with care. Useful for unit tests.</remarks>
        public static void Refresh()
        {
            Providers = new List<SearchProvider>();
            Filter = new SearchFilter();
            OverrideFilter = new SearchFilter();
            var settingsValid = FetchProviders();
            settingsValid = LoadGlobalSettings() || settingsValid;
            SortActionsPriority();

            if (!settingsValid)
            {
                // Override all settings
                SaveGlobalSettings();
            }
        }

        /// <summary>
        /// Returns a list of keywords used by auto-completion for the active providers.
        /// </summary>
        /// <param name="context">Current search context</param>
        /// <param name="lastToken">Search token currently being typed.</param>
        /// <returns>A list of keywords that can be shown in an auto-complete dropdown.</returns>
        public static string[] GetKeywords(SearchContext context, string lastToken)
        {
            var keywords = new List<string>();
            if (context.isActionQuery && lastToken.StartsWith(k_ActionQueryToken, StringComparison.Ordinal))
            {
                keywords.AddRange(ActionIdToProviders.Keys.Select(k => k_ActionQueryToken + k));
            }
            else
            {
                var activeProviders = OverrideFilter.filteredProviders.Count > 0 ? OverrideFilter.filteredProviders : Filter.filteredProviders;
                foreach (var provider in activeProviders)
                {
                    try
                    {
                        provider.fetchKeywords?.Invoke(context, lastToken, keywords);
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogError($"Failed to get keywords with {provider.name.displayName}.\r\n{ex}");
                    }
                }
            }

            return keywords.Distinct().ToArray();
        }

        /// <summary>
        /// Initiate a search and return all search items matching the search context. Other items can be found later using the asynchronous searches.
        /// </summary>
        /// <param name="context">The current search context</param>
        /// <returns>A list of search items matching the search query.</returns>
        public static List<SearchItem> GetItems(SearchContext context)
        {
            // Stop all search sessions every time there is a new search.
            StopAllAsyncSearchSessions();

            PrepareSearch(context);

            if (context.isActionQuery || OverrideFilter.filteredProviders.Count > 0)
                return GetItems(context, OverrideFilter);

            if (string.IsNullOrEmpty(context.searchText))
                return new List<SearchItem>(0);

            return GetItems(context, Filter);
        }

        /// <summary>
        /// Setup the search service before initiating a search session. A search session can be composed of many searches (different words, etc.)
        /// </summary>
        /// <param name="context">The search context to be initialized.</param>
        public static void Enable(SearchContext context)
        {
            LoadSessionSettings();
            PrepareSearch(context);
            foreach (var provider in Providers.Where(p => p.active))
            {
                using (var enableTimer = new DebugTimer(null))
                {
                    provider.onEnable?.Invoke();
                    provider.enableTime = enableTimer.timeMs;
                }
            }
        }

        /// <summary>
        /// Indicates that a search session should be terminated.
        /// </summary>
        /// <param name="context">The search context ending the search session.</param>
        /// <remarks>Any asynchronously running search query will be stopped and all search result will be lost.</remarks>
        public static void Disable(SearchContext context)
        {
            LastSearch = context.searchText;

            StopAllAsyncSearchSessions();
            s_SearchSessions.Clear();

            foreach (var provider in Providers.Where(p => p.active))
                provider.onDisable?.Invoke();

            SaveSessionSettings();
            SaveGlobalSettings();
        }

        internal static bool LoadSessionSettings()
        {
            LastSearch = LoadSessionSetting(k_LastSearchPrefKey, String.Empty);
            return LoadRecents();
        }

        internal static void SaveSessionSettings()
        {
            SaveSessionSetting(k_LastSearchPrefKey, LastSearch);
            SaveRecents();
        }

        internal static bool LoadGlobalSettings()
        {
            return LoadFilters();
        }

        internal static void SaveGlobalSettings()
        {
            if (SearchService.Filter.allActive)
                SaveFilters();
        }

        [UsedImplicitly]
        internal static void Reset()
        {
            EditorPrefs.SetString(k_FilterPrefKey, null);
            Refresh();
        }

        internal static string CyclePreviousSearch(int shift)
        {
            if (s_RecentSearches.Count == 0)
                return s_LastSearch;

            s_RecentSearchIndex = Wrap(s_RecentSearchIndex + shift, s_RecentSearches.Count);

            return s_RecentSearches[s_RecentSearchIndex];
        }

        internal static int Wrap(int index, int n)
        {
            return ((index % n) + n) % n;
        }

        internal static void SetDefaultAction(string providerId, string actionId)
        {
            if (string.IsNullOrEmpty(providerId) || string.IsNullOrEmpty(actionId))
                return;

            EditorPrefs.SetString(k_DefaultActionPrefKey + providerId, actionId);
            SortActionsPriority();
        }

        internal static void SortActionsPriority(SearchProvider searchProvider)
        {
            if (searchProvider.actions.Count == 1)
                return;

            var defaultActionId = EditorPrefs.GetString(k_DefaultActionPrefKey + searchProvider.name.id);
            if (string.IsNullOrEmpty(defaultActionId))
                return;
            if (searchProvider.actions.Count == 0 || defaultActionId == searchProvider.actions[0].Id)
                return;

            searchProvider.actions.Sort((action1, action2) =>
            {
                if (action1.Id == defaultActionId)
                    return -1;

                if (action2.Id == defaultActionId)
                    return 1;

                return 0;
            });
        }

        internal static void SortActionsPriority()
        {
            foreach (var searchProvider in Providers)
                SortActionsPriority(searchProvider);
        }

        internal static void PrepareSearch(SearchContext context)
        {
            string[] overrideFilterId = null;
            context.searchQuery = context.searchText ?? String.Empty;
            context.isActionQuery = context.searchQuery.StartsWith(">", StringComparison.Ordinal);
            if (context.isActionQuery)
            {
                var searchIndex = 1;
                var potentialCommand = Utils.GetNextWord(context.searchQuery, ref searchIndex).ToLowerInvariant();
                if (ActionIdToProviders.ContainsKey(potentialCommand))
                {
                    // We are in command mode:
                    context.actionQueryId = potentialCommand;
                    overrideFilterId = ActionIdToProviders[potentialCommand].ToArray();
                    context.searchQuery = context.searchQuery.Remove(0, searchIndex).Trim();
                }
                else
                {
                    overrideFilterId = new string[0];
                }
            }
            else
            {
                foreach (var kvp in TextFilterIds)
                {
                    if (context.searchQuery.StartsWith(kvp.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        overrideFilterId = new [] {kvp.Value};
                        context.searchQuery = context.searchQuery.Remove(0, kvp.Key.Length).Trim();
                        break;
                    }
                }
            }

            var tokens = context.searchQuery.Split(' ').Select(t => t.ToLowerInvariant()).ToArray();
            context.searchWords = tokens.Where(t => !t.Contains(":")).ToArray();
            context.textFilters = tokens.Where(t => t.Contains(":")).ToArray();

            if (overrideFilterId != null)
            {
                OverrideFilter.ResetFilter(false);
                foreach (var provider in Providers)
                {
                    if (overrideFilterId.Contains(provider.name.id))
                    {
                        OverrideFilter.SetFilter(true, provider.name.id);
                    }
                }
            }
            else if (OverrideFilter.filteredProviders.Count > 0)
            {
                OverrideFilter.ResetFilter(false);
            }
        }

        private static List<SearchItem> GetItems(SearchContext context, SearchFilter filter)
        {
            #if QUICKSEARCH_DEBUG
            using (new DebugTimer("==> Search Items"))
            #endif
            {
                var allItems = new List<SearchItem>(3);
                var maxFetchTimePerProviderMs = k_MaxFetchTimeMs / Math.Max(1, filter.filteredProviders.Count);
                foreach (var provider in filter.filteredProviders)
                {
                    using (var fetchTimer = new DebugTimer(null))
                    {
                        context.categories = filter.GetSubCategories(provider);
                        try
                        {
                            var enumerable = provider.fetchItems(context, allItems, provider);
                            if (enumerable != null)
                            {
                                if (!s_SearchSessions.TryGetValue(provider.name.id, out var session))
                                {
                                    session = new AsyncSearchSession();
                                    s_SearchSessions.Add(provider.name.id, session);
                                }
                                session.Reset(enumerable, maxFetchTimePerProviderMs);
                                if (!session.FetchSome(allItems, maxFetchTimePerProviderMs))
                                    session.Stop();
                            }
                            provider.RecordFetchTime(fetchTimer.timeMs);
                        }
                        catch (Exception ex)
                        {
                            UnityEngine.Debug.LogException(new Exception($"Failed to get fetch {provider.name.displayName} provider items.", ex));
                        }
                    }
                }

                #if QUICKSEARCH_DEBUG
                using (new DebugTimer("<== Sort Items"))
                #endif
                {
                    SortItemList(allItems);
                    return allItems.GroupBy(i => i.id).Select(i => i.First()).ToList();
                }
            }
        }

        internal static void SortItemList(List<SearchItem> items)
        {
            items.Sort(SortItemComparer);
        }

        private static int SortItemComparer(SearchItem item1, SearchItem item2)
        {
            var po = item1.provider.priority.CompareTo(item2.provider.priority);
            if (po != 0)
                return po;
            po = item1.score.CompareTo(item2.score);
            if (po != 0)
                return po;
            return String.Compare(item1.id, item2.id, StringComparison.Ordinal);
        }

        private static bool FetchProviders()
        {
            Providers = Utils.GetAllMethodsWithAttribute<SearchItemProviderAttribute>().Select(methodInfo =>
            {
                try
                {
                    SearchProvider fetchedProvider = null;
                    using (var fetchLoadTimer = new DebugTimer(null))
                    {
                        fetchedProvider = methodInfo.Invoke(null, null) as SearchProvider;
                        if (fetchedProvider == null) 
                            return null;

                        fetchedProvider.loadTime = fetchLoadTimer.timeMs;

                        // Load per provider user settings
                        fetchedProvider.active = EditorPrefs.GetBool($"{prefKey}.{fetchedProvider.name.id}.active", fetchedProvider.active);
                        fetchedProvider.priority = EditorPrefs.GetInt($"{prefKey}.{fetchedProvider.name.id}.priority", fetchedProvider.priority);
                    }
                    return fetchedProvider;
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogException(ex);
                    return null;
                }
            }).Where(provider => provider != null).ToList();

            RefreshProviders();
            return true;
        }

        internal static void RefreshProviders()
        {
            ActionIdToProviders = new Dictionary<string, List<string>>();
            foreach (var action in Utils.GetAllMethodsWithAttribute<SearchActionsProviderAttribute>()
                                        .SelectMany(methodInfo => methodInfo.Invoke(null, null) as IEnumerable<object>).Where(a => a != null).Cast<SearchAction>())
            {
                var provider = Providers.Find(p => p.name.id == action.providerId);
                if (provider == null) 
                    continue;
                provider.actions.Add(action);
                if (!ActionIdToProviders.TryGetValue(action.Id, out var providerIds))
                {
                    providerIds = new List<string>();
                    ActionIdToProviders[action.Id] = providerIds;
                }
                providerIds.Add(provider.name.id);
            }

            Filter.Providers = Providers.Where(p => !p.isExplicitProvider).ToList();
            OverrideFilter.Providers = Providers;
            TextFilterIds = new Dictionary<string, string>();
            foreach (var provider in Providers)
            {
                if (string.IsNullOrEmpty(provider.filterId))
                    continue;

                if (char.IsLetterOrDigit(provider.filterId[provider.filterId.Length - 1]))
                {
                    UnityEngine.Debug.LogWarning($"Provider: {provider.name.id} filterId: {provider.filterId} must ends with non-alphanumeric character.");
                    continue;
                }

                TextFilterIds.Add(provider.filterId, provider.name.id);
            }
        }

        /// <summary>
        /// Load user default filters.
        /// </summary>
        /// <returns>True if filters were properly loaded, otherwise false is returned.</returns>
        public static bool LoadFilters()
        {
            try
            {
                var filtersStr = EditorPrefs.GetString(k_FilterPrefKey, null);
                Filter.ResetFilter(true);

                if (!string.IsNullOrEmpty(filtersStr))
                {
                    var filters = Utils.JsonDeserialize(filtersStr) as List<object>;
                    foreach (var filterObj in filters)
                    {
                        var filter = filterObj as Dictionary<string, object>;
                        if (filter == null)
                            continue;

                        var providerId = filter["providerId"] as string;
                        Filter.SetExpanded(filter["isExpanded"].ToString() == "True", providerId);
                        Filter.SetFilterInternal(filter["isEnabled"].ToString() == "True", providerId);
                        var categories = filter["categories"] as List<object>;
                        foreach (var catObj in categories)
                        {
                            var cat = catObj as Dictionary<string, object>;
                            Filter.SetFilterInternal(cat["isEnabled"].ToString() == "True", providerId, cat["id"] as string);
                        }
                    }
                }

                Filter.UpdateFilteredProviders();
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        private static bool LoadRecents()
        {
            try
            {
                var ro = Utils.JsonDeserialize(LoadSessionSetting(k_RecentsPrefKey));
                if (!(ro is List<object> recents))
                    return false;

                s_UserScores = recents.Select(Convert.ToInt32).ToList();
                s_SortedUserScores = new HashSet<int>(s_UserScores);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string FilterToString()
        {
            var filters = new List<object>();
            foreach (var providerDesc in Filter.providerFilters)
            {
                var filter = new Dictionary<string, object>
                {
                    ["providerId"] = providerDesc.name.id,
                    ["isEnabled"] = providerDesc.name.isEnabled,
                    ["isExpanded"] = providerDesc.isExpanded
                };
                var categories = new List<object>();
                filter["categories"] = categories;
                foreach (var cat in providerDesc.categories)
                {
                    categories.Add(new Dictionary<string, object>()
                    {
                        { "id", cat.id },
                        { "isEnabled", cat.isEnabled }
                    });
                }
                filters.Add(filter);
            }

            return Utils.JsonSerialize(filters);
        }

        private static string GetPrefKeyName(string suffix)
        {
            if (Filter.filteredProviders.Count == 0)
                return $"{prefKey}.noscope.{suffix}";

            var scope = Filter.filteredProviders.Select(p => p.filterId.GetHashCode()).Aggregate((h1, h2) => (h1 ^ h2).GetHashCode());
            return $"{prefKey}.{scope}.{suffix}";
        }

        private static void SaveSessionSetting(string key, string value)
        {
            var prefKeyName = GetPrefKeyName(key);
            //UnityEngine.Debug.Log($"Saving session setting {prefKeyName} with {value}");
            EditorPrefs.SetString(prefKeyName, value);
        }

        private static string LoadSessionSetting(string key, string defaultValue = default)
        {
            var prefKeyName = GetPrefKeyName(key);
            var value = EditorPrefs.GetString(prefKeyName, defaultValue);
            //UnityEngine.Debug.Log($"Loading session setting {prefKeyName} with {value}");
            return value;
        }

        internal static void SaveFilters()
        {
            var filter = FilterToString();
            EditorPrefs.SetString(k_FilterPrefKey, filter);
        }

        private static void SaveRecents()
        {
            // We only save the last 40 most recent items.
            SaveSessionSetting(k_RecentsPrefKey, Utils.JsonSerialize(s_UserScores.Skip(s_UserScores.Count - 40).ToArray()));
        }

        private static void StopAllAsyncSearchSessions()
        {
            foreach (var searchSession in s_SearchSessions)
            {
                searchSession.Value.Stop();
            }
        }
    }
}
