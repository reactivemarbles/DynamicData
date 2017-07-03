using System;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace DynamicData.Tests.Cache
{
    
    public class DisposeManyFixture: IDisposable
    {
        private class DisposableObject : IDisposable
        {
            public bool IsDisposed { get; private set; }
            public int Id { get; private set; }

            public DisposableObject(int id)
            {
                Id = id;
            }

            public void Dispose()
            {
                IsDisposed = true;
            }
        }

        private readonly ISourceCache<DisposableObject, int> _source;
        private readonly ChangeSetAggregator<DisposableObject, int> _results;

        public DisposeManyFixture()
        {
            _source = new SourceCache<DisposableObject, int>(p => p.Id);
            _results = new ChangeSetAggregator<DisposableObject, int>(_source.Connect().DisposeMany());
        }

        public void Dispose()
        {
            _source.Dispose();
            _results.Dispose();
        }

        [Fact]
        public void AddWillNotCallDispose()
        {
            _source.AddOrUpdate(new DisposableObject(1));

            _results.Messages.Count.Should().Be(1, "Should be 1 updates");
            _results.Data.Count.Should().Be(1, "Should be 1 item in the cache");
            _results.Data.Items.First().IsDisposed.Should().Be(false, "Should not be disposed");
        }

        [Fact]
        public void RemoveWillCallDispose()
        {
            _source.AddOrUpdate(new DisposableObject(1));
            _source.Remove(1);

            _results.Messages.Count.Should().Be(2, "Should be 2 updates");
            _results.Data.Count.Should().Be(0, "Should be 0 items in the cache");
            _results.Messages[1].First().Current.IsDisposed.Should().Be(true, "Should be disposed");
        }

        [Fact]
        public void UpdateWillCallDispose()
        {
            _source.AddOrUpdate(new DisposableObject(1));
            _source.AddOrUpdate(new DisposableObject(1));

            _results.Messages.Count.Should().Be(2, "Should be 2 updates");
            _results.Data.Count.Should().Be(1, "Should be 1 items in the cache");
            _results.Messages[1].First().Current.IsDisposed.Should().Be(false, "Current should not be disposed");
            _results.Messages[1].First().Previous.Value.IsDisposed.Should().Be(true, "Previous should be disposed");
        }

        [Fact]
        public void EverythingIsDisposedWhenStreamIsDisposed()
        {
            _source.AddOrUpdate(Enumerable.Range(1, 10).Select(i => new DisposableObject(i)));
            _source.Clear();

            _results.Messages.Count.Should().Be(2, "Should be 2 updates");
            _results.Messages[1].All(d => d.Current.IsDisposed).Should().BeTrue();
        }
    }
}
