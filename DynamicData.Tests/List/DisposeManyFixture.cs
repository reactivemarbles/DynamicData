using System;
using System.Linq;
using Xunit;
using FluentAssertions;

namespace DynamicData.Tests.List
{
    
    public class DisposeManyFixture: IDisposable
    {
        private readonly ISourceList<DisposableObject> _source;
        private readonly ChangeSetAggregator<DisposableObject> _results;

        public  DisposeManyFixture()
        {
            _source = new SourceList<DisposableObject>();
            _results = new ChangeSetAggregator<DisposableObject>(_source.Connect().DisposeMany());
        }

        public void Dispose()
        {
            _source.Dispose();
            _results.Dispose();
        }

        [Fact]
        public void AddWillNotCallDispose()
        {
            _source.Add(new DisposableObject(1));

            _results.Messages.Count.Should().Be(1, "Should be 1 updates");
            _results.Data.Count.Should().Be(1, "Should be 1 item in the cache");
            _results.Data.Items.First().IsDisposed.Should().Be(false, "Should not be disposed");
        }

        [Fact]
        public void RemoveWillCallDispose()
        {
            _source.Add(new DisposableObject(1));
            _source.RemoveAt(0);

            _results.Messages.Count.Should().Be(2, "Should be 2 updates");
            _results.Data.Count.Should().Be(0, "Should be 0 items in the cache");
            _results.Messages[1].First().Item.Current.IsDisposed.Should().Be(true, "Should be disposed");
        }

        [Fact]
        public void UpdateWillCallDispose()
        {
            _source.Add(new DisposableObject(1));
            _source.ReplaceAt(0, new DisposableObject(1));

            _results.Messages.Count.Should().Be(2, "Should be 2 updates");
            _results.Data.Count.Should().Be(1, "Should be 1 items in the cache");
            _results.Messages[1].First().Item.Current.IsDisposed.Should().Be(false, "Current should not be disposed");
            _results.Messages[1].First().Item.Previous.Value.IsDisposed.Should().Be(true, "Previous should be disposed");
        }

        [Fact]
        public void EverythingIsDisposedWhenStreamIsDisposed()
        {
            var toadd = Enumerable.Range(1, 10).Select(i => new DisposableObject(i));
            _source.AddRange(toadd);
            _source.Clear();

            _results.Messages.Count.Should().Be(2, "Should be 2 updates");

            var itemsCleared = _results.Messages[1].First().Range;
            itemsCleared.All(d => d.IsDisposed).Should().BeTrue();
        }

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
    }
}
