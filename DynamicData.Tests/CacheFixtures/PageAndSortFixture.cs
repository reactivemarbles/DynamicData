using System;
using DynamicData.Binding;
using DynamicData.Controllers;
using NUnit.Framework;

namespace DynamicData.Tests.CacheFixtures
{
    [TestFixture]
    public class PageAndSortFixture
    {
        [Test]
        public void DoNotThrowAWobblyWhenRemovingaMutatedValue()
        {
            var pageController = new PageController();
            var sortController = new SortController<TestVm>(SortExpressionComparer<TestVm>.Ascending(t => t.DateFavorited ?? DateTime.MinValue));
            var filterController = new FilterController<TestVm>(myVm => myVm.Id != 0);
            var items = new ObservableCollectionExtended<TestVm>();
            var itemCache = new SourceCache<TestVm, int>(myVm => myVm.Id);

            var item1 = new TestVm(1) { DateFavorited = DateTime.Now };
            var item2 = new TestVm(2) { DateFavorited = DateTime.Now };

            itemCache.AddOrUpdate(item1);
            itemCache.AddOrUpdate(item2);

            bool error = false;
            itemCache.Connect()
                     .Filter(filterController)
                     .Sort(sortController)
                     .Page(pageController) //error doesnt occur with paging disabled
                     .Bind(items)
                     .Subscribe(changes => { }, ex => error = true);

            pageController.Change(new PageRequest(1, 100));

            //NB: never errored if it was the first item which was removed
            item2.DateFavorited = null;
            itemCache.Remove(item2); //ERROR!

            Assert.IsFalse(error, "Error has been thrown");
        }

        private class TestVm
        {
            public int Id { get; }
            public DateTime? DateFavorited { get; set; }

            public TestVm(int id)
            {
                Id = id;
            }
        }
    }
}
