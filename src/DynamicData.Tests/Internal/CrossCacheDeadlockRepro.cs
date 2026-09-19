// CrossCacheDeadlockRepro.cs
// Reproduction for: Cross-cache deadlock when concurrent updates notify subscribers that modify other SourceCache instances
// Requires: DynamicData 9.x.
//
// This test deadlocks on DynamicData main. It should complete in under 10 seconds.

namespace DynamicData.Tests.Internal;

[NotInParallel]
public class CrossCacheDeadlockRepro : IDisposable
{
    private readonly SourceCache<string, int> _cacheA = new(static x => x.GetHashCode());
    private readonly SourceCache<string, int> _cacheB = new(static x => x.GetHashCode());

    [Test]
    public async Task ConcurrentPopulateIntoShouldNotDeadlock()
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
        await Assert.That(completed).IsTrue();
        await Assert.That(count).IsGreaterThan(0);
    }

    public void Dispose()
    {
        _cacheA.Dispose();
        _cacheB.Dispose();
    }
}
