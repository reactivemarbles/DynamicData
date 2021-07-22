﻿using System;
using System.Reactive.Linq;

using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache
{
    public class SourceCacheFixture : IDisposable
    {
        private readonly ChangeSetAggregator<Person, string> _results;

        private readonly ISourceCache<Person, string> _source;

        public SourceCacheFixture()
        {
            _source = new SourceCache<Person, string>(p => p.Key);
            _results = _source.Connect().AsAggregator();
        }

        [Fact]
        public void CanHandleABatchOfUpdates()
        {
            _source.Edit(
                updater =>
                    {
                        var torequery = new Person("Adult1", 44);

                        updater.AddOrUpdate(new Person("Adult1", 40));
                        updater.AddOrUpdate(new Person("Adult1", 41));
                        updater.AddOrUpdate(new Person("Adult1", 42));
                        updater.AddOrUpdate(new Person("Adult1", 43));
                        updater.Refresh(torequery);
                        updater.Remove(torequery);
                        updater.Refresh(torequery);
                    });

            _results.Summary.Overall.Count.Should().Be(6, "Should be  6 up`dates");
            _results.Messages.Count.Should().Be(1, "Should be 1 message");
            _results.Messages[0].Adds.Should().Be(1, "Should be 1 update");
            _results.Messages[0].Updates.Should().Be(3, "Should be 3 updates");
            _results.Messages[0].Removes.Should().Be(1, "Should be  1 remove");
            _results.Messages[0].Refreshes.Should().Be(1, "Should be 1 evaluate");

            _results.Data.Count.Should().Be(0, "Should be 1 item in` the cache");
        }

        [Fact]
        public void CountChanged()
        {
            int count = 0;
            int invoked = 0;
            using (_source.CountChanged.Subscribe(
                c =>
                    {
                        count = c;
                        invoked++;
                    }))
            {
                invoked.Should().Be(1);
                count.Should().Be(0);

                _source.AddOrUpdate(new RandomPersonGenerator().Take(100));
                invoked.Should().Be(2);
                count.Should().Be(100);

                _source.Clear();
                invoked.Should().Be(3);
                count.Should().Be(0);
            }
        }

        [Fact]
        public void CountChangedShouldAlwaysInvokeUponeSubscription()
        {
            int? result = null;
            var subscription = _source.CountChanged.Subscribe(count => result = count);

            result.HasValue.Should().BeTrue();

            if (result is null)
            {
                throw new InvalidOperationException(nameof(result));
            }

            result.Value.Should().Be(0, "Count should be zero");

            subscription.Dispose();
        }

        [Fact]
        public void CountChangedShouldReflectContentsOfCacheInvokeUponSubscription()
        {
            var generator = new RandomPersonGenerator();
            int? result = null;
            var subscription = _source.CountChanged.Subscribe(count => result = count);

            _source.AddOrUpdate(generator.Take(100));

            if (result is null)
            {
                throw new InvalidOperationException(nameof(result));
            }

            result.HasValue.Should().BeTrue();
            result.Value.Should().Be(100, "Count should be 100");
            subscription.Dispose();
        }

        public void Dispose()
        {
            _source.Dispose();
            _results.Dispose();
        }

        [Fact]
        public void SubscribesDisposesCorrectly()
        {
            bool called = false;
            bool errored = false;
            bool completed = false;
            var subscription = _source.Connect().Finally(() => completed = true).Subscribe(updates => { called = true; }, ex => errored = true, () => completed = true);
            _source.AddOrUpdate(new Person("Adult1", 40));

            subscription.Dispose();
            _source.Dispose();

            errored.Should().BeFalse();
            called.Should().BeTrue();
            completed.Should().BeTrue();
        }

        [Fact]
        public void EmptyChanges()
        {
            IChangeSet<Person, string>? change = null;

            using var subscription = _source.Connect(suppressEmptyChangeSets: false)
                .Subscribe(c=> change = c);

            change.Should().NotBeNull();
            change!.Count.Should().Be(0);

        }

        [Fact]
        public void EmptyChangesWithFilter()
        {
            IChangeSet<Person, string>? change = null;

            using var subscription = _source.Connect(p=>p.Age == 20, suppressEmptyChangeSets: false)
                .Subscribe(c => change = c);

            change.Should().NotBeNull();
            change!.Count.Should().Be(0);
        }
    }
}