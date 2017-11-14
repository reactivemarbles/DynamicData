using System;
using System.Collections.Generic;
using System.Linq;
using DynamicData.Tests.Domain;
using Xunit;

namespace DynamicData.Tests.Playground
{
    public class Cache_FilterPerf
    {
        private static readonly ICollection<Person> People = Enumerable.Range(1, 1_000_000).Select(i=>new Person(i.ToString(),i)).ToList();

        [Fact]
        public void CacheOnly_Range()
        {
            using (var list = new SourceCache<Person, string>(i=>i.Name))
            {
                list.AddOrUpdate(People);
            }

        }

        [Fact]
        public void CacheOnly_Many()
        {
            using (var list = new SourceCache<Person, string>(i => i.Name))
            {
                foreach (var item in People)
                    list.AddOrUpdate(item);
            }
        }

        [Fact]
        public void Filter_RangeBeforeSubscribe()
        {
            using (var list =new SourceCache<Person, string>(i => i.Name))
            {
                list.AddOrUpdate(People);

                list.Connect().Filter(v => v.Age % 2 == 0).Subscribe(v => { });
            }
        }

        [Fact]
        public void Filter_ItemsBeforeSubscribe()
        {
            using (var list = new SourceCache<Person, string>(i => i.Name))
            {
                foreach (var item in People)
                    list.AddOrUpdate(item);
                list.Connect().Filter(v => v.Age % 2 == 0).Subscribe(v => { });
            }
        }

        [Fact]
        public void Filter_ItemsAfterSubscribe()
        {
            using (var list = new SourceCache<Person, string>(i => i.Name))
            {
                list.Connect().Filter(v => v.Age % 2 == 0).Subscribe(v => { });

                foreach (var item in People)
                    list.AddOrUpdate(item);
            }

        }

        [Fact]
        public void Filter_RangeAfterSubscribe()
        {
            using (var list = new SourceCache<Person, string>(i => i.Name))
            {
                list.Connect().Filter(v => v.Age % 2 == 0).Subscribe(v => { });

                list.AddOrUpdate(People);
            }
        }

        // [Fact]
        //public void BufferInitial()
        //{
        //    var sheduler = new TestScheduler();
        //    using (var list = new SourceList<int>())
        //    {

        //        int count = 0;
        //        list.Connect()
        //            .BufferInitial(TimeSpan.FromMilliseconds(250), sheduler)
        //            .Filter(v => v % 2 == 0)
        //            .Subscribe(v => { count = v.TotalChanges; });

        //        foreach (var item in Items)
        //            list.Add(item);

        //        sheduler.AdvanceBy(TimeSpan.FromMilliseconds(251).Ticks);

        //        count.Should().Be(500_000);

        //        list.Add(2);
        //        count.Should().Be(1);
        //    }

        //}

    }

    public class ListFilterPerf
    {

        private static readonly ICollection<int> Items = Enumerable.Range(1, 1_000_000).ToList();

        [Fact]
        public void ListOnly_Range()
        {
            using (var list = new SourceList<int>())
            {
                list.AddRange(Items);
            }

        }

        [Fact]
        public void ListOnly_Many()
        {
            using (var list = new SourceList<int>())
            {
                foreach (var item in Items)
                    list.Add(item);
            }
        }

        [Fact]
        public void Filter_RangeBeforeSubscribe()
        {
            using (var list = new SourceList<int>())
            {
                list.AddRange(Items);

                list.Connect().Filter(v => v % 2 == 0).Subscribe(v => { });
            }
        }

        [Fact]
        public void Filter_ItemsBeforeSubscribe()
        {
            using (var list = new SourceList<int>())
            {
                foreach (var item in Items)
                    list.Add(item);

                list.Connect().Filter(v => v % 2 == 0).Subscribe(v => { });
            }
        }

        [Fact]
        public void Filter_ItemsAfterSubscribe()
        {
            using (var list = new SourceList<int>())
            {
                list.Connect().Filter(v => v % 2 == 0).Subscribe(v => { });

                foreach (var item in Items)
                    list.Add(item);
            }

        }

        [Fact]
        public void Filter_RangeAfterSubscribe()
        {
            using (var list = new SourceList<int>())
            {
                list.Connect().Filter(v => v % 2 == 0).Subscribe(v => { });

                list.AddRange(Items);
            }
        }

        // [Fact]
        //public void BufferInitial()
        //{
        //    var sheduler = new TestScheduler();
        //    using (var list = new SourceList<int>())
        //    {

        //        int count = 0;
        //        list.Connect()
        //            .BufferInitial(TimeSpan.FromMilliseconds(250), sheduler)
        //            .Filter(v => v % 2 == 0)
        //            .Subscribe(v => { count = v.TotalChanges; });

        //        foreach (var item in Items)
        //            list.Add(item);

        //        sheduler.AdvanceBy(TimeSpan.FromMilliseconds(251).Ticks);

        //        count.Should().Be(500_000);

        //        list.Add(2);
        //        count.Should().Be(1);
        //    }

        //}

    }
}
