namespace DynamicData.Tests;

public class OptionalInitialRegressionFixture
{
    [Test]
    public async Task EmptySourceEmitsInitialNoneBeforeCompletion()
    {
        var values = new List<ReactiveUI.Primitives.Optional<int>>();
        var completed = false;
        using var subscription = Observable.Empty<IChangeSet<int, int>>()
            .ToObservableOptional(1, initialOptionalWhenMissing: true)
            .Subscribe(values.Add, () => completed = true);

        await Assert.That(values).HasCount(1);
        await Assert.That(values[0].HasValue).IsFalse();
        await Assert.That(completed).IsTrue();
    }

    [Test]
    public async Task SynchronousSourceErrorDoesNotEmitSyntheticNone()
    {
        var expected = new InvalidOperationException("Source failed");
        var values = new List<ReactiveUI.Primitives.Optional<int>>();
        Exception? observed = null;
        var completed = false;
        using var subscription = Observable.Throw<IChangeSet<int, int>>(expected)
            .ToObservableOptional(1, initialOptionalWhenMissing: true)
            .Subscribe(values.Add, error => observed = error, () => completed = true);

        await Assert.That(values).IsEmpty();
        await Assert.That(observed).IsSameReferenceAs(expected);
        await Assert.That(completed).IsFalse();
    }
}
