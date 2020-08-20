using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace Unity.QuickSearch
{
    /// <summary>
    /// Indicates how the search item description needs to be formatted when presented to the user.
    /// </summary>
    [Flags]
    public enum SearchItemDescriptionFormat
    {
        None = 0,
        Ellipsis = 1 << 0,
        RightToLeft = 1 << 1,
        Highlight = 1 << 2,
        FuzzyHighlight = 1 << 3
    }

    /// <summary>
    /// Search items are returned by the search provider when some results need to be shown to the user after a search is made.
    /// The search item holds all the data that will be used to sort and present the search results.
    /// </summary>
    [DebuggerDisplay("{id} | {label}")]
    public class SearchItem : IEqualityComparer<SearchItem>, IEquatable<SearchItem>
    {
        /// <summary>Unique id of this item among this provider items.</summary>
        public readonly string id;
        /// <summary>The item score can affect how the item gets sorted within the same provider.</summary>
        public int score;
        /// <summary>Display name of the item</summary>
        public string label;
        /// <summary>If no description is provided, SearchProvider.fetchDescription will be called when the item is first displayed.</summary>
        public string description;
        /// <summary>If true - description already has formatting / rich text</summary>
        public SearchItemDescriptionFormat descriptionFormat;
        /// <summary>If no thumbnail are provider, SearchProvider.fetchThumbnail will be called when the item is first displayed.</summary>
        public Texture2D thumbnail;
        /// <summary>Large preview of the search item. Usually cached by fetchPreview.</summary>
        public Texture2D preview;
        /// <summary>Back pointer to the provider.</summary>
        public SearchProvider provider;
        /// <summary>Search provider defined content. It can be used to transport any data to custom search provider handlers (i.e. `fetchDescription`).</summary>
        public object data;

        /// <summary>
        /// Construct a search item. Minimally a search item need to have a unique id for a given search query.
        /// </summary>
        /// <param name="_id"></param>
        public SearchItem(string _id)
        {
            id = _id;
        }

        public bool Equals(SearchItem x, SearchItem y)
        {
            return x.id == y.id;
        }

        public int GetHashCode(SearchItem obj)
        {
            return obj.id.GetHashCode();
        }

        public bool Equals(SearchItem other)
        {
            return id == other.id;
        }
    }
}