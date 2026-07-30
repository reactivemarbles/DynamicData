// CrossCacheDeadlockRepro.cs
// Reproduction for: Cross-cache deadlock when concurrent updates notify subscribers that modify other SourceCache instances
// Requires: DynamicData 9.x, xunit, FluentAssertions
//
// This test deadlocks on DynamicData main. It should complete in under 10 seconds.

namespace DynamicData.Tests.Internal;

public class CrossCacheDeadlockRepro : IDisposable
{
    private readonly SourceCache<string, int> _cacheA = new(static x => x.GetHashCode());
    private readonly SourceCache<string, int> _cacheB = new(static x => x.GetHashCode());

    [Fact]
    public void ConcurrentPopulateIntoShouldNotDeadlock()
    {
        // Arrange
        using var destination = new SourceCache<string, int>(static x => x.GetHashCode());
        using var subA = _cacheA.Connect().PopulateInto(destination);
        using var subB = _cacheB.Connect().PopulateInto(destination);

        var count = 0;
        using var feedback = destination.Connect().Subscribe(_ => Interlocked.Increment(ref count));

        // Act — concurrent updates from two threads
        var completed = Task.WaitAll(
            [
                Task.Run(() =>
                {
                    for (var i = 0; i < 100; i++)
                    {
                        _cacheA.AddOrUpdate($"A-{i}");
                    }
                }),
                Task.Run(() =>
                {
                    for (var i = 0; i < 100; i++)
                    {
                        _cacheB.AddOrUpdate($"B-{i}");
                    }
                }),
            ],
            TimeSpan.FromSeconds(10));

        // Assert
        completed.Should().BeTrue("concurrent PopulateInto should not deadlock");
        count.Should().BeGreaterThan(0, "destination should have received changeset notifications");
    }

    public void Dispose()
    {
        _cacheA.Dispose();
        _cacheB.Dispose();
    }
}
