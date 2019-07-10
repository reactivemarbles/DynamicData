// Copyright (c) 2011-2019 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Linq;
using Xunit;

namespace DynamicData.Profile
{

    public class CacheAllocationChecks
    {
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
