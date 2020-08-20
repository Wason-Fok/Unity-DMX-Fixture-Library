# Changelog

## [1.5.4] - 2020-05-22
- [UX] Remove experimental indexing in 1.5 as it will be officially released in version 1.6
- [FIX] Remove usage of Progress.RunTask

## [1.5.3] - 2020-02-23
- [FIX] Increase word character variation indexing to 32.
- [FIX] Ensure package and store search providers are not enabled while runnign tests.

## [1.5.2] - 2020-02-20
- [FIX] The asset store provider will only be available for 2020.1 and newer.
- [FIX] Improve scene provider performances
- [FIX] Fix Unity crash when dragging and dropping from quick search (1215420)
- [Fix] Fix complete file name indexing (case 1214270)

## [1.5.1] - 2020-01-24
- [FIX] Fix Progress API usage.

## [1.5.0] - 2020-01-22
- [UX] You can now search scene objects with a given component using c:<component name>.
- [UX] We've removed the dockable window mode of Quick Search since it wasn't playing nice with some loading and refreshing workflows and optimizations.
- [UX] Update the quick search spinning wheel when async results are still being fetched.
- [UX] Select search item on mouse up instead of mouse down.
- [UX] fetchPreview of AssetStoreProvider uses the PurchaseInfo to get a bigger/more detailed preview.
- [UX] Change the Resources Provider to use the QueryEngine. Some behaviors may have changed.
- [UX] Asset Store provider fetches multiple screenshots and populates the preview panel carousel with those.
- [UX] Add UMPE quick search indexing to build the search index in another process.
- [UX] Add selected search item preview panel.
- [UX] Add Resource provider, which lets you search all resources loaded by Unity.
- [UX] Add new Unity 2020.1 property editor support.
- [UX] Add drag and drop support to the resource search provider.
- [UX] Add documentation link to Help provider and version label.
- [UX] Add Asset Store provider populating items with asset store package.
- [UX] Add a new settings to enable the new asset indexer in the user preferences.
- [UX] Add a new asset indexer that indexes many asset properties, such as dependencies, size, serialized properties, etc.
- [UX] Add a carrousel to display images of asset store search results.
- [FIX] Only enable the search asset watcher once the quick search tool is used the first time.
- [FIX] Do not load the LogProvider if the application console log path is not valid.
- [FIX] Add support for digits when splitting camel cases of file names.
- [FIX] Prevent search callback errors when not validating queries.
- [DOC] Quick Search Manual has been reviewed and edited.
- [DOC] Document more APIs.
- [DOC] Add some sample packages to Quick Search to distribute more search provider and query engine examples.
- [API] Make Unity.QuickSearch.QuickSearch public to allow user to open quick search explicitly with specific context data.
- [API] Improved the SearchIndexer API and performances
- [API] Change the signature of `fetchItems` to return an object instead of an `IEnumerable<SearchItem>`. This item can be an `IEnumerable<SearchItem>` as before, or an `IEnumerator` to allow yield returns of `IEnumerator` or `IEnumerable`.
- [API] Add the ability to configure string comparisons with the QueryEngine.
- [API] Add the `QueryEngine` API.
- [API] Add `QuickSearch.ShowWindow(float width, float height)` to allow opening Quick Search at any size.

## [1.4.1] - 2019-09-03
- Quick Search is now a verified package.
- [UX] Add UIElements experimental search provider.
- [FIX] Add to the asset search provider type filter all ScriptableObject types.
- [FIX] Fix Asset store URL.
- [DOC] Document more public APIs.
- [API] Add programming hooks to the scene search provider.
