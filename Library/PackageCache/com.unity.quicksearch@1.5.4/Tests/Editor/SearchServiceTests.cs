using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Unity.QuickSearch
{
    class AsyncSearchProvider : SearchProvider
    {
        public const int NumberSearchItems = 10;
        public const int NumberNullItems = 10;

        public bool UseSleep { get; set; }
        public int SleepTimeMS { get; set; }

        public AsyncSearchProvider(string id, string displayName = null)
            : base(id, displayName)
        {
            fetchItems = (context, items, provider) => FetchItems(provider);
            UseSleep = false;
            SleepTimeMS = 0;
        }

        private IEnumerable<SearchItem> FetchItems(SearchProvider provider)
        {
            for (var i = 0; i < NumberNullItems; ++i)
            {
                if (UseSleep)
                    Thread.Sleep(SleepTimeMS);
                yield return null;
            }
            for (var i = 0; i < NumberSearchItems; ++i)
            {
                if (UseSleep)
                    Thread.Sleep(SleepTimeMS);
                var item = provider.CreateItem(i.ToString(), i.ToString());
                yield return item;
            }
        }
    }

    class AsyncSearchProviderEnumerator : AsyncSearchProvider
    {
        public AsyncSearchProviderEnumerator(string id, string displayName = null)
            : base(id, displayName)
        {
            fetchItems = (context, items, provider) => FetchItemsIEnumerator(provider);
            UseSleep = false;
            SleepTimeMS = 0;
        }

        private IEnumerator FetchNullItems()
        {
            for (var i = 0; i < NumberNullItems; ++i)
            {
                if (UseSleep)
                    Thread.Sleep(SleepTimeMS);
                yield return null;
            }
        }

        private IEnumerator FetchSearchItems(SearchProvider provider)
        {
            for (var i = 0; i < NumberSearchItems; ++i)
            {
                if (UseSleep)
                    Thread.Sleep(SleepTimeMS);
                var item = provider.CreateItem(i.ToString(), i.ToString());
                yield return item;
            }
        }

        private IEnumerator FetchItemsIEnumerator(SearchProvider provider)
        {
            yield return FetchNullItems();
            yield return FetchSearchItems(provider);
        }
    }

    class AsyncSearchProviderEnumeratorEnumerable : AsyncSearchProvider
    {
        public AsyncSearchProviderEnumeratorEnumerable(string id, string displayName = null)
            : base(id, displayName)
        {
            fetchItems = (context, items, provider) => FetchItemsIEnumerator(provider);
            UseSleep = false;
            SleepTimeMS = 0;
        }

        private IEnumerable<SearchItem> FetchSearchItems(SearchProvider provider)
        {
            for (var i = 0; i < NumberSearchItems; ++i)
            {
                if (UseSleep)
                    Thread.Sleep(SleepTimeMS);
                var item = provider.CreateItem(i.ToString(), i.ToString());
                yield return item;
            }
        }

        private IEnumerator FetchItemsIEnumerator(SearchProvider provider)
        {
            for (var i = 0; i < NumberNullItems; ++i)
            {
                if (UseSleep)
                    Thread.Sleep(SleepTimeMS);
                yield return null;
            }

            yield return FetchSearchItems(provider);
        }
    }

    class SearchServiceTests
    {
        const string k_TestFileName = "Tests/Editor/Content/test_material_42.mat";

        internal static void SetProviderActive(string providerId, bool isActive)
        {
            var provider = SearchService.Providers.FirstOrDefault(p => p.name.id == providerId);
            if (provider != null)
            {
                provider.active = isActive;
            }
            SearchService.Filter.SetFilter(isActive, providerId);
        }

        [SetUp]
        public void EnableService()
        {
            SearchService.SaveFilters();
            SearchService.Enable(SearchContext.Empty);
            SearchService.Filter.ResetFilter(true);
            SetProviderActive("store", false);
            SetProviderActive("packages", false);
        }

        [TearDown]
        public void DisableService()
        {
            SearchService.Disable(SearchContext.Empty);
            SearchService.LoadFilters();
        }

        [UnityTest]
        public IEnumerator FetchItems1()
        {
            var ctx = new SearchContext {searchText = "p:test_material_42", wantsMore = true};

            var fetchedItems = SearchService.GetItems(ctx);
            while (AsyncSearchSession.SearchInProgress)
                yield return null;

            Assert.IsNotEmpty(fetchedItems);
            var foundItem = fetchedItems.Find(item => item.label == Path.GetFileName(k_TestFileName));
            Assert.IsNotNull(foundItem);
            Assert.IsNotNull(foundItem.id);
            Assert.IsNull(foundItem.description);

            Assert.IsNotNull(foundItem.provider);
            Assert.IsNotNull(foundItem.provider.fetchDescription);
            var fetchedDescription = foundItem.provider.fetchDescription(foundItem, ctx);
            Assert.IsTrue(fetchedDescription.Contains(k_TestFileName));
        }

        [UnityTest]
        public IEnumerator FetchItems2()
        {
            var fetchedItems = SearchService.GetItems(new SearchContext { searchText = "p:test material 42" });
            while (AsyncSearchSession.SearchInProgress)
                yield return null;

            Assert.IsNotEmpty(fetchedItems);
            var foundItem = fetchedItems.Find(item => item.label == Path.GetFileName(k_TestFileName));
            Assert.IsNotNull(foundItem);
            Assert.IsTrue(foundItem.id.EndsWith(k_TestFileName));
        }

        [UnityTest]
        public IEnumerator FetchItems3()
        {
            var fetchedItems = SearchService.GetItems(new SearchContext { searchText = "p:t:material 42" });
            while (AsyncSearchSession.SearchInProgress)
                yield return null;

            Assert.IsNotEmpty(fetchedItems);
            var foundItem = fetchedItems.Find(item => item.label == Path.GetFileName(k_TestFileName));
            Assert.IsNotNull(foundItem);
            Assert.IsTrue(foundItem.id.EndsWith(k_TestFileName));
        }

        [UnityTest]
        public IEnumerator FetchItemsAsync()
        {
            const string k_SearchType = "___test_async";
            var ctx = new SearchContext { searchText = k_SearchType+":async", wantsMore = true };

            bool wasCalled = false;

            void OnAsyncCallback(IEnumerable<SearchItem> items)
            {
                wasCalled = items.Any(i => i.id == k_SearchType);
            }

            IEnumerable<SearchItem> TestFetchAsyncCallback(SearchProvider provider)
            {
                var ft = UnityEditor.EditorApplication.timeSinceStartup + 1;
                while (UnityEditor.EditorApplication.timeSinceStartup < ft)
                    yield return null;

                yield return provider.CreateItem(k_SearchType);
            }

            var testSearchProvider = new SearchProvider(k_SearchType, k_SearchType)
            {
                filterId = k_SearchType+":",
                isExplicitProvider = true,
                fetchItems = (context, items, provider) => TestFetchAsyncCallback(provider)
            };

            AsyncSearchSession.asyncItemReceived += OnAsyncCallback;

            SearchService.Providers.Add(testSearchProvider);
            SearchService.RefreshProviders();

            SearchService.GetItems(ctx);
            yield return null;

            while (AsyncSearchSession.SearchInProgress)
                yield return null;

            while (!wasCalled)
                yield return null;

            Assert.True(wasCalled);

            SearchService.Providers.Remove(testSearchProvider);
            SearchService.RefreshProviders();
            AsyncSearchSession.asyncItemReceived -= OnAsyncCallback;
        }
    }

    internal class AsyncSearchSessionTests
    {
        [SetUp]
        public void EnableService()
        {
            SearchService.Filter.ResetFilter(true);
            SearchServiceTests.SetProviderActive("store", false);
            SearchServiceTests.SetProviderActive("packages", false);
            SearchService.Enable(SearchContext.Empty);
        }

        [TearDown]
        public void DisableService()
        {
            SearchService.Disable(SearchContext.Empty);
        }

        [Test]
        public void FetchSomeIEnumerable()
        {
            var provider = new AsyncSearchProvider(Guid.NewGuid().ToString());
            TestProvider(provider);
        }

        [Test]
        public void FetchSomeIEnumerator()
        {
            var provider = new AsyncSearchProviderEnumerator(Guid.NewGuid().ToString());
            TestProvider(provider);
        }

        [Test]
        public void FetchSomeIEnumeratorIEnumerable()
        {
            var provider = new AsyncSearchProviderEnumeratorEnumerable(Guid.NewGuid().ToString());
            TestProvider(provider);
        }

        private static void TestProvider(AsyncSearchProvider provider)
        {
            var session = new AsyncSearchSession();
            var ctx = SearchContext.Empty;
            var items = new List<SearchItem>();
            var enumerable = provider.fetchItems(ctx, items, provider);
            Assert.IsEmpty(items);
            session.Reset(enumerable);

            // Test fetching all objects
            var total = AsyncSearchProvider.NumberNullItems + AsyncSearchProvider.NumberSearchItems;
            session.FetchSome(items, total, false);
            Assert.AreEqual(AsyncSearchProvider.NumberSearchItems, items.Count);

            items = new List<SearchItem>();
            session.FetchSome(items, total, false);
            Assert.AreEqual(0, items.Count); // Should be empty since enumerator is at the end

            enumerable = provider.fetchItems(ctx, items, provider);
            session.Reset(enumerable);
            session.FetchSome(items, AsyncSearchProvider.NumberSearchItems, false);
            Assert.AreEqual(AsyncSearchProvider.NumberSearchItems - AsyncSearchProvider.NumberNullItems, items.Count);

            // Test fetching non-null objects
            enumerable = provider.fetchItems(ctx, items, provider);
            session.Reset(enumerable);
            session.FetchSome(items, total, true);
            Assert.AreEqual(AsyncSearchProvider.NumberSearchItems, items.Count);

            items = new List<SearchItem>();
            session.FetchSome(items, total, false);
            Assert.AreEqual(0, items.Count); // Should be empty since enumerator is at the end

            enumerable = provider.fetchItems(ctx, items, provider);
            session.Reset(enumerable);
            session.FetchSome(items, AsyncSearchProvider.NumberSearchItems, true);
            Assert.AreEqual(AsyncSearchProvider.NumberSearchItems, items.Count);

            // Fetch items time constrained
            var maxFetchTimeMs = 1;
            items = new List<SearchItem>();
            provider.UseSleep = true;
            provider.SleepTimeMS = 5;
            session.Reset(enumerable);
            session.FetchSome(items, maxFetchTimeMs);
            Assert.AreEqual(0, items.Count);

            // Fetch items with time and size constraints
            items = new List<SearchItem>();
            enumerable = provider.fetchItems(ctx, items, provider);
            session.Reset(enumerable);
            provider.SleepTimeMS = 1;
            maxFetchTimeMs = provider.SleepTimeMS * AsyncSearchProvider.NumberNullItems;
            session.FetchSome(items, AsyncSearchProvider.NumberSearchItems, true, maxFetchTimeMs);
            Assert.Less(items.Count, AsyncSearchProvider.NumberSearchItems);
        }
    }
}
