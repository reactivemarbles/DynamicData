using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Xunit;
using DynamicData.Cache.Internal;

namespace DynamicData.Profile
{

    public class CacheAllocationChecks
    {
        [Fact]
        public void List()
        {
            var changes = Enumerable.Range(1, 10_000).Select(j => new Person("P" + j, j))
                .Select(x => new Change<Person, string>(ChangeReason.Add, x.Name, x))
                .ToList();

            var changeset = new ChangeSet<Person, string>(changes);
            var target = ImmutableList<Person>.Empty;
            
            var result = Allocations.Run(() =>
            {
                target = target.Clone(changeset);
            });
            Console.WriteLine(result);

            var changeset2 = new ChangeSet<Person, string>(new []
            {
                new Change<Person, string>(ChangeReason.Add,"XXXX",new Person("XXXX",0)),
            });
            var result2 = Allocations.Run(() =>
            {
                target = target.Clone(changeset2);
            });
            Console.WriteLine(result2);
        }

        [Fact]
        public void PopulateCache_NoObservers()
        {
            var items = Enumerable.Range(1, 10_000).Select(j => new Person("P" + j, j)).ToList();

            var cache = new SourceCache<Person, string>(p=>p.Name);

            var result = Allocations.Run(() =>
            {
                cache.AddOrUpdate(items);
            });
            Console.WriteLine(result);
        }

        [Fact]
        public void PopulateCache_Observers()
        {
            var items = Enumerable.Range(1, 10_000).Select(j => new Person("P" + j, j)).ToList();
    
            var cache = new SourceCache<Person, string>(p => p.Name);
            var subscribed = cache.Connect().Subscribe();
            var result = Allocations.Run(() =>
            {
                cache.AddOrUpdate(items);
            });
            Console.WriteLine(result);
        }

        [Fact]
        public void Connect()
        {
            var items = Enumerable.Range(1, 10_000).Select(j => new Person("P" + j, j)).ToList();

            var cache = new SourceCache<Person, string>(p => p.Name);
            cache.Connect().Subscribe();//do warm up


            var added = Allocations.Run(() => cache.AddOrUpdate(items));
            Console.WriteLine(added);

            var connected = Allocations.Run(() => cache.Connect().Subscribe());
            Console.WriteLine(connected);

            var filtered = Allocations.Run(() => cache.Connect(p=>p.Age <5000).Subscribe());
            Console.WriteLine(filtered);
        }
    }
}
