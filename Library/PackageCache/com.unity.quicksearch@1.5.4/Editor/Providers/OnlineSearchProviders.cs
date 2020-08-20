using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

namespace Unity.QuickSearch
{
    namespace Providers
    {
        static class SearchUtility
        {
            public static void Goto(string baseUrl, List<Tuple<string, string>> query = null)
            {
                var url = baseUrl;

                if (query != null)
                {
                    url += "?";
                    for (var i = 0; i < query.Count; ++i)
                    {
                        var item = query[i];
                        url += item.Item1 + "=" + item.Item2;
                        if (i < query.Count - 1)
                        {
                            url += "&";
                        }
                    }
                }

                var uri = new Uri(url);
                Process.Start(uri.AbsoluteUri);
            }
        }

        static class AnswersHelper
        {
            internal static string searchUrl = "https://answers.unity.com/search.html";
            internal static OnlineSearchItemTemplate template = new OnlineSearchItemTemplate()
            {
                name = new NameEntry("answers", "Answers"),
                icon = Icons.search,
                descriptionTitle = "answers.unity.com",
                actionHandler = Goto
            };

            internal static void Goto(SearchItem item, SearchContext context)
            {
                // ex: https://answers.unity.com/search.html?f=&type=question&sort=relevance&q=Visual+scripting
                var query = new List<Tuple<string, string>>
                {
                    Tuple.Create("type", "question"), Tuple.Create("sort", "relevance"), Tuple.Create("q", string.Join("+", context.searchWords))
                };
                SearchUtility.Goto(searchUrl, query);
            }
        }

        static class DocManualHelper
        {
            internal static string searchUrl = "https://docs.unity3d.com/Manual/30_search.html";
            internal static OnlineSearchItemTemplate template = new OnlineSearchItemTemplate()
            {
                name = new NameEntry("manual", "Manual"),
                icon = Icons.search,
                descriptionTitle = "docs.unity3d.com/Manual",
                actionHandler = Goto
            };

            internal static void Goto(SearchItem item, SearchContext context)
            {
                // ex: https://docs.unity3d.com/Manual/30_search.html?q=Visual+Scripting
                var query = new List<Tuple<string, string>> {Tuple.Create("q", string.Join("+", context.searchWords))};
                SearchUtility.Goto(searchUrl, query);
            }
        }

        static class DocScriptingHelper
        {
            internal static string searchUrl = "https://docs.unity3d.com/ScriptReference/30_search.html";
            internal static OnlineSearchItemTemplate template = new OnlineSearchItemTemplate()
            {
                name = new NameEntry("scripting", "Scripting API"),
                icon = Icons.search,
                descriptionTitle = "docs.unity3d.com/ScriptReference",
                actionHandler = Goto
            };

            internal static void Goto(SearchItem item, SearchContext context)
            {
                // ex: https://docs.unity3d.com/ScriptReference/30_search.html?q=Visual+Scripting
                var query = new List<Tuple<string, string>> {Tuple.Create("q", string.Join("+", context.searchWords))};
                SearchUtility.Goto(searchUrl, query);
            }
        }

        static class AssetStoreHelper
        {
            internal static string searchUrl = "https://assetstore.unity.com/";
            internal static OnlineSearchItemTemplate template = new OnlineSearchItemTemplate()
            {
                name = new NameEntry("store", "Asset Store"),
                icon = Icons.store,
                descriptionTitle = "assetstore.unity.com",
                actionHandler = Goto
            };

            internal static void Goto(SearchItem item, SearchContext context)
            {
                // ex: https://assetstore.unity.com/?q=Visual+Scripting
                var query = new List<Tuple<string, string>>();                
                query.Add(Tuple.Create("q",context.searchQuery));
                query.Add(Tuple.Create("orderBy", "1"));
                SearchUtility.Goto(searchUrl, query);
            }
        }

        internal class OnlineSearchItemTemplate
        {
            public NameEntry name;
            public Action<SearchItem, SearchContext> actionHandler;
            public Texture2D icon;
            public string descriptionTitle;
        }

        [UsedImplicitly]
        static class OnlineSearchProvider
        {
            internal static string type = "web";
            internal static string displayName = "Online Search";
            static OnlineSearchItemTemplate[] s_ItemTemplates;

            static OnlineSearchItemTemplate FindById(string id)
            {
                var result = Array.Find(s_ItemTemplates, template => template.name.id == id);
                return result ?? s_ItemTemplates[0];
            }

            [UsedImplicitly, SearchItemProvider]
            internal static SearchProvider CreateProvider()
            {
                if (s_ItemTemplates == null)
                {
                    s_ItemTemplates = new[]
                    {
                        DocScriptingHelper.template,
                        DocManualHelper.template,
                        AssetStoreHelper.template,
                        AnswersHelper.template
                    };
                }

                return new SearchProvider(type, displayName)
                {
                    priority = 10000,
                    filterId = "web:",
                    fetchItems = (context, items, provider) =>
                    {
                        foreach (var category in context.categories)
                        {
                            if (!category.isEnabled)
                                continue;
                            var template = FindById(category.id);
                            var item = provider.CreateItem(category.id, "Search " + template.descriptionTitle, "Search for: " + context.searchQuery, template.icon);
                            items.Add(item);
                        }

                        return null;
                    },
                    subCategories = s_ItemTemplates.Select(template => template.name).ToList()
                };
            }

            [UsedImplicitly, SearchActionsProvider]
            internal static IEnumerable<SearchAction> ActionHandlers()
            {
                return new []
                {
                    new SearchAction(type, "browse", null, "Search online...")
                    {
                        handler = (item, context) => FindById(item.id).actionHandler(item, context)
                    }
                };
            }
        }
    }
}
