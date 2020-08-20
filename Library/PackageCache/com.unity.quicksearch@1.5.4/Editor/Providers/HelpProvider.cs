using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;

namespace Unity.QuickSearch
{
    namespace Providers
    {
        [UsedImplicitly]
        static class HelpProvider
        {
            internal static string type = "help";
            internal static string displayName = "Help";

            static Dictionary<SearchItem, Action<SearchItem, SearchContext>> m_StaticItemToAction;

            [UsedImplicitly, SearchItemProvider]
            internal static SearchProvider CreateProvider()
            {
                var helpProvider = new SearchProvider(type, displayName)
                {
                    priority = -1,
                    filterId = "?",
                    isExplicitProvider = true,
                    fetchItems = (context, items, provider) =>
                    {
                        if (m_StaticItemToAction == null)
                            BuildHelpItems(provider);

                        var dynamicItems = GetRecentSearchItems(provider);
                        var helpItems = m_StaticItemToAction.Keys.Concat(dynamicItems);

                        if (string.IsNullOrEmpty(context.searchQuery) || string.IsNullOrWhiteSpace(context.searchQuery))
                        {
                            items.AddRange(helpItems);
                            return null;
                        }

                        items.AddRange(helpItems.Where(item => SearchProvider.MatchSearchGroups(context, item.label) || SearchProvider.MatchSearchGroups(context, item.description)));
                        return null;
                    }
                };

                return helpProvider;
            }

            static IEnumerable<SearchItem> GetRecentSearchItems(SearchProvider provider)
            {
                int recentIndex = 0;
                return SearchService.s_RecentSearches
                    .Where(search => !string.IsNullOrEmpty(search) && !string.IsNullOrWhiteSpace(search))
                    .Select(search =>
                    {
                        var item = provider.CreateItem($"help_recent_{recentIndex}", $"Recent search: {search}", "Use Alt + Up/Down Arrow to cycle through recent searches");
                        item.thumbnail = Icons.quicksearch;
                        item.score = m_StaticItemToAction.Count + recentIndex++;
                        item.data = search;
                        return item;
                    });
            }

            [UsedImplicitly, SearchActionsProvider]
            internal static IEnumerable<SearchAction> ActionHandlers()
            {
                return new[]
                {
                    new SearchAction(type, "help", null, "Help") {
                        closeWindowAfterExecution = false,
                        handler = (item, context) =>
                        {
                            if (item.id.StartsWith("help_recent_"))
                            {
                                context.searchView.SetSearchText((string)item.data);
                            }
                            else if (m_StaticItemToAction.TryGetValue(item, out var helpHandler))
                            {
                                helpHandler(item, context);
                            }
                        }
                    }
                };
            }

            static void BuildHelpItems(SearchProvider helpProvider)
            {
                m_StaticItemToAction = new Dictionary<SearchItem, Action<SearchItem, SearchContext>>();
                {
                    var helpItem = helpProvider.CreateItem("help_open_quicksearch_doc", "Open Quick Search Documentation");
                    helpItem.score = m_StaticItemToAction.Count;
                    helpItem.thumbnail = Icons.settings;
                    m_StaticItemToAction.Add(helpItem, (item, context) => QuickSearch.OpenDocumentationUrl());
                }

                // Settings provider: id, Search for...
                foreach (var provider in SearchService.OrderedProviders)
                {
                    var helpItem = provider.isExplicitProvider ?
                        helpProvider.CreateItem($"help_provider_{provider.name.id}",
                            $"Activate only <b>{provider.name.displayName}</b>",
                            $"Type <b>{provider.filterId}</b> to activate <b>{provider.name.displayName}</b>") :
                        helpProvider.CreateItem($"help_provider_{provider.name.id}",
                            $"Search only <b>{provider.name.displayName}</b>",
                            $"Type <b>{provider.filterId}</b> to search <b>{provider.name.displayName}</b>");

                    helpItem.score = m_StaticItemToAction.Count;
                    helpItem.thumbnail = Icons.search;
                    m_StaticItemToAction.Add(helpItem, (item, context) =>
                    {
                        context.searchView.SetSearchText(provider.filterId);
                    });
                }

                // Action queries
                foreach(var kvp in SearchService.ActionIdToProviders)
                {
                    var actionId = kvp.Key;
                    var supportedProviderIds = kvp.Value;
                    var provider = SearchService.GetProvider(supportedProviderIds[0]);
                    var action = SearchService.GetAction(provider, actionId);
                    if (action == null)
                        continue;

                    var desc = Utils.FormatProviderList(supportedProviderIds.Select(providerId => SearchService.GetProvider(providerId)));
                    var helpItem = helpProvider.CreateItem($"help_action_query_{actionId}",
                        $"{action.DisplayName} for {desc}",
                        $"Type <b> >{actionId}</b>"
                        );
                    helpItem.thumbnail = Icons.shortcut;
                    helpItem.score = m_StaticItemToAction.Count;
                    m_StaticItemToAction.Add(helpItem, (item, context) => context.searchView.SetSearchText($">{actionId} "));
                }

                {
                    var helpItem = helpProvider.CreateItem("help_open_pref", "Open Quick Search Preferences");
                    helpItem.score = m_StaticItemToAction.Count;
                    helpItem.thumbnail = Icons.settings;
                    m_StaticItemToAction.Add(helpItem, (item, context) => SettingsService.OpenUserPreferences(SearchSettings.settingsPreferencesKey));
                }

                {
                    var helpItem = helpProvider.CreateItem("help_open_filter_window", "Open Filter Window", "Type Alt + Left Arrow to open the Filter Window");
                    helpItem.score = m_StaticItemToAction.Count;
                    helpItem.thumbnail = Icons.settings;
                    m_StaticItemToAction.Add(helpItem, (item, context) =>
                    {
                        context.searchView.PopFilterWindow();
                    });
                }
            }
        }

    }
}
