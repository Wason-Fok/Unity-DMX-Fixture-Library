using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using UnityEditor;

namespace Unity.QuickSearch
{
    namespace Providers
    {
        [UsedImplicitly]
        static class Settings
        {
            private const string type = "settings";
            private const string displayName = "Settings";

            static class SettingsPaths
            {
                public readonly static string[] value;

                static SettingsPaths()
                {
                    value = FetchSettingsProviders().Select(provider => provider.settingsPath).ToArray();
                }

                private static SettingsProvider[] FetchSettingsProviders()
                {
                    var type = typeof(SettingsService);
                    var method = type.GetMethod("FetchSettingsProviders", BindingFlags.NonPublic | BindingFlags.Static);
                    return (SettingsProvider[])method.Invoke(null, null);
                }
            }

            [UsedImplicitly, SearchItemProvider]
            private static SearchProvider CreateProvider()
            {
                return new SearchProvider(type, displayName)
                {
                    filterId = "se:",
                    fetchItems = (context, items, provider) =>
                    {
                        items.AddRange(SettingsPaths.value
                                       .Where(path => SearchProvider.MatchSearchGroups(context, path))
                                       .Select(path => provider.CreateItem(path, null, path)));
                        return null;
                    },

                    fetchLabel = (item, context) => item.label ?? (item.label = Utils.GetNameFromPath(item.id)),

                    fetchThumbnail = (item, context) => Icons.settings
                };
            }

            [UsedImplicitly, SearchActionsProvider]
            private static IEnumerable<SearchAction> ActionHandlers()
            {
                return new[]
                {
                    new SearchAction(type, "open", null, "Open project settings...")
                    {
                        handler = (item, context) =>
                        {
                            if (item.id.StartsWith("Project/"))
                                SettingsService.OpenProjectSettings(item.id);
                            else
                                SettingsService.OpenUserPreferences(item.id);
                        }
                    }
                };
            }
        }
    }
}
