using System;
using UnityEngine;

namespace Unity.QuickSearch
{
    public class SearchAction
    {
        /// <summary>
        /// Unique ID used for contextual action (i.e. to pop a contextual menu when the user right click on an item).
        /// </summary>
        public const string kContextualMenuAction = "context";

        /// <summary>
        /// Default constructor to build a search action.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="content"></param>
        public SearchAction(string type, GUIContent content)
        {
            providerId = type;
            this.content = content;
            isEnabled = (item, context) => true;
        }

        /// <summary>
        /// Extended constructor to build a search action.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="name"></param>
        /// <param name="icon"></param>
        /// <param name="tooltip"></param>
        public SearchAction(string type, string name, Texture2D icon = null, string tooltip = null)
            : this(type, new GUIContent(name, icon, tooltip ?? name))
        {
        }

        /// <summary>
        /// Action unique identifier.
        /// </summary>
        public string Id => content.text;

        /// <summary>
        /// Name used to display
        /// </summary>
        public string DisplayName => content.tooltip;

        /// <summary>
        /// Indicates if the search view should be closed after the action execution.
        /// </summary>
        public bool closeWindowAfterExecution = true;

        /// <summary>
        /// Unique (for a given provider) id of the action
        /// </summary>
        internal string providerId;

        /// <summary>
        /// GUI content used to display the action in the search view.
        /// </summary>
        internal GUIContent content;
        
        /// <summary>
        /// Called when an item is executed with this action
        /// </summary>
        public Action<SearchItem, SearchContext> handler;

        /// <summary>
        /// Called before displaying the menu to see if an action is available for a given item.
        /// </summary>
        public Func<SearchItem, SearchContext, bool> isEnabled;
    }
}