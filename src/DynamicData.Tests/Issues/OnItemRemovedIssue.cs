using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using DynamicData.Binding;
using Xunit;

namespace DynamicData.Tests.Issues
{
    public class OnItemRemovedIssue
    {

        //Fix for https://github.com/reactivemarbles/DynamicData/issues/268

        [Fact]
        public void ListAndCacheShouldHaveEquivalentBehaviour()
        {
            var source = new ObservableCollection<Item>
            {
                new() { Id = 1 },
                new() { Id = 2 }
            };

            var list = source.ToObservableChangeSet()
                .Transform(item => new Proxy { Item = item })
                .OnItemAdded(proxy => proxy.Active = true)
                .OnItemRemoved(proxy => proxy.Active = false)
                .Bind(out var listOutput)
                .Subscribe();

            var cache = source.ToObservableChangeSet(item => item.Id)
                .Transform(item => new Proxy { Item = item })
                .OnItemAdded(proxy => proxy.Active = true)
                .OnItemRemoved(proxy => proxy.Active = false)
                .Bind(out var cacheOutput)
                .Subscribe();

            Assert.Equal(listOutput, cacheOutput, new ProxyEqualityComparer());

            list.Dispose();
            cache.Dispose();

            Assert.Equal(listOutput, cacheOutput, new ProxyEqualityComparer());
        }

        public class Item
        {
            public int Id { get; set; }
        }

        public class Proxy
        {
            public Item Item { get; set; }

            public bool? Active { get; set; }


        }

        public class ProxyEqualityComparer : IEqualityComparer<Proxy>
        {
            public bool Equals(Proxy x, Proxy y) => x?.Item.Id == y?.Item.Id && x.Active == y.Active;

            public int GetHashCode(Proxy obj) => HashCode.Combine(obj?.Active, obj.Item);
        }
    }
}
