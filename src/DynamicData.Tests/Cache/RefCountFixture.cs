using System;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache;

public class RefCountFixture : IDisposable
{
    private readonly ISourceCache<Person, string> _source;

    public RefCountFixture() => _source = new SourceCache<Person, string>(p => p.Key);

    [Fact]
    public void CanResubscribe()
    {
        var created = 0;
        var disposals = 0;

        // must have data so transform is invoked
        _source.AddOrUpdate(new Person("Name", 10));

        // Some expensive transform (or chain of operations)
        var longChain = _source.Connect().Transform(p => p).Do(_ => created++).Finally(() => disposals++).RefCount();

        var subscriber = longChain.Subscribe();
        subscriber.Dispose();

        subscriber = longChain.Subscribe();
        subscriber.Dispose();

        created.Should().Be(2);
        disposals.Should().Be(2);
    }

    [Fact]
    public void ChainIsInvokedOnceForMultipleSubscribers()
    {
        var created = 0;
        var disposals = 0;

        // Some expensive transform (or chain of operations)
        var longChain = _source.Connect().Transform(p => p).Do(_ => created++).Finally(() => disposals++).RefCount();

        var subscriber1 = longChain.Subscribe();
        var subscriber2 = longChain.Subscribe();
        var subscriber3 = longChain.Subscribe();

        _source.AddOrUpdate(new Person("Name", 10));
        subscriber1.Dispose();
        subscriber2.Dispose();
        subscriber3.Dispose();

        created.Should().Be(1);
        disposals.Should().Be(1);
    }

    public void Dispose() => _source.Dispose();

    // This test is probabilistic, it could be cool to be able to prove RefCount's thread-safety
    // more accurately but I don't think that there is an easy way to do this.
    // At least this test can catch some bugs in the old implementation.
    //   [Fact]
    private async Task IsHopefullyThreadSafe()
    {
        var refCount = _source.Connect().RefCount();

        await Task.WhenAll(
            Enumerable.Range(0, 100).Select(
                _ => Task.Run(
                    () =>
                    {
                        for (var i = 0; i < 1000; ++i)
                        {
                            var subscription = refCount.Subscribe();
                            subscription.Dispose();
                        }
                    })));
    }
}
