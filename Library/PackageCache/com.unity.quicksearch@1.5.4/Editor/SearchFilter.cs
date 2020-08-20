using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Unity.QuickSearch
{
    public class SearchFilter
    {
        [DebuggerDisplay("{name.displayName} expanded:{isExpanded}")]
        internal class ProviderDesc
        {
            public ProviderDesc(NameEntry name, SearchProvider provider)
            {
                this.name = name;
                categories = new List<NameEntry>();
                isExpanded = false;
                this.provider = provider;
            }

            public int priority => provider.priority;

            public NameEntry name;
            public bool isExpanded;
            public List<NameEntry> categories;
            public SearchProvider provider;
        }

        public bool allActive { get; internal set; }

        internal List<SearchProvider> filteredProviders;
        internal List<ProviderDesc> providerFilters;

        private List<SearchProvider> m_Providers;
        public List<SearchProvider> Providers
        {
            get => m_Providers;

            set
            {
                m_Providers = value;
                providerFilters.Clear();
                filteredProviders.Clear();
                foreach (var provider in m_Providers.Where(p => p.active))
                {
                    var providerFilter = new ProviderDesc(new NameEntry(provider.name.id, GetProviderNameWithFilter(provider)), provider);
                    providerFilters.Add(providerFilter);
                    foreach (var subCategory in provider.subCategories)
                    {
                        providerFilter.categories.Add(subCategory);
                    }
                }
                UpdateFilteredProviders();
            }
        }

        public SearchFilter()
        {
            filteredProviders = new List<SearchProvider>();
            providerFilters = new List<ProviderDesc>();
        }

        public void ResetFilter(bool enableAll, bool preserveSubFilters = false)
        {
            allActive = enableAll;
            foreach (var providerDesc in providerFilters)
                SetFilterInternal(enableAll, providerDesc.name.id, null, preserveSubFilters);
            UpdateFilteredProviders();
        }

        public void SetFilter(bool isEnabled, string providerId, string subCategory = null, bool preserveSubFilters = false)
        {
            if (SetFilterInternal(isEnabled, providerId, subCategory, preserveSubFilters))
                UpdateFilteredProviders();
        }

        public void SetExpanded(bool isExpanded, string providerId)
        {
            var providerDesc = providerFilters.Find(pd => pd.name.id == providerId);
            if (providerDesc != null)
            {
                providerDesc.isExpanded = isExpanded;
            }
        }

        public bool IsEnabled(string providerId, string subCategory = null)
        {
            var desc = providerFilters.Find(pd => pd.name.id == providerId);
            if (desc != null)
            {
                if (subCategory == null)
                {
                    return desc.name.isEnabled;
                }

                foreach (var cat in desc.categories)
                {
                    if (cat.id == subCategory)
                        return cat.isEnabled;
                }
            }

            return false;
        }

        public static string GetProviderNameWithFilter(SearchProvider provider)
        {
            return string.IsNullOrEmpty(provider.filterId) ? provider.name.displayName : provider.name.displayName + " (" + provider.filterId + ")";
        }

        public List<NameEntry> GetSubCategories(SearchProvider provider)
        {
            var desc = providerFilters.Find(pd => pd.name.id == provider.name.id);
            return desc?.categories;
        }

        internal void UpdateFilteredProviders()
        {
            filteredProviders = Providers.Where(p => IsEnabled(p.name.id)).ToList();
        }

        internal bool SetFilterInternal(bool isEnabled, string providerId, string subCategory = null, bool preserveSubFilters = false)
        {
            var providerDesc = providerFilters.Find(pd => pd.name.id == providerId);
            if (providerDesc == null) 
                return false;

            if (subCategory == null)
            {
                providerDesc.name.isEnabled = isEnabled;
                if (preserveSubFilters) 
                    return true;

                foreach (var cat in providerDesc.categories)
                    cat.isEnabled = isEnabled;
            }
            else
            {
                foreach (var cat in providerDesc.categories)
                {
                    if (cat.id == subCategory)
                    {
                        cat.isEnabled = isEnabled;
                        if (isEnabled)
                            providerDesc.name.isEnabled = true;
                    }
                }
            }

            return true;
        }
    }
}