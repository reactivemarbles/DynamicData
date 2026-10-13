namespace DynamicData.Tests.Cache;

public class LockFreeObservableCacheFixture
{
    [Fact]
    public void WatchFailsWhenTheSourceFails()
    {
        using var source = new Subject<IChangeSet<Person, string>>();
        using var cache = source.AsObservableCache(applyLocking: false);

        var actualError = default(Exception);
        var isCompleted = false;
        using var subscription = cache.Watch("Name1").Subscribe(static _ => { }, error => actualError = error, () => isCompleted = true);

        var expectedError = new Exception("Test Exception");
        source.OnError(expectedError);

        actualError.Should().BeSameAs(expectedError, "a watcher should be told when the source fails");
        isCompleted.Should().BeFalse("the source failed, it did not complete");
    }

    [Fact]
    public void WatchCompletesWhenTheSourceCompletes()
    {
        using var source = new Subject<IChangeSet<Person, string>>();
        using var cache = source.AsObservableCache(applyLocking: false);

        var actualError = default(Exception);
        var isCompleted = false;
        using var subscription = cache.Watch("Name1").Subscribe(static _ => { }, error => actualError = error, () => isCompleted = true);

        source.OnCompleted();

        isCompleted.Should().BeTrue("the source completed");
        actualError.Should().BeNull("no error occurred");
    }

    [Fact]
    public void WatchCompletesWhenTheCacheIsDisposed()
    {
        using var source = new Subject<IChangeSet<Person, string>>();
        var cache = source.AsObservableCache(applyLocking: false);

        var isCompleted = false;
        using var subscription = cache.Watch("Name1").Subscribe(static _ => { }, () => isCompleted = true);

        cache.Dispose();

        isCompleted.Should().BeTrue("a disposed cache can never change again");
    }

    [Fact]
    public void CountChangedAndPreviewFailWhenTheSourceFails()
    {
        using var source = new Subject<IChangeSet<Person, string>>();
        using var cache = source.AsObservableCache(applyLocking: false);

        var countError = default(Exception);
        var previewError = default(Exception);
        using var countSubscription = cache.CountChanged.Subscribe(static _ => { }, error => countError = error);
        using var previewSubscription = cache.Preview().Subscribe(static _ => { }, error => previewError = error);

        var expectedError = new Exception("Test Exception");
        source.OnError(expectedError);

        countError.Should().BeSameAs(expectedError, "a count subscriber should be told when the source fails");
        previewError.Should().BeSameAs(expectedError, "a preview subscriber should be told when the source fails");
    }

    [Fact]
    public void CountChangedAndPreviewCompleteWhenTheSourceCompletes()
    {
        using var source = new Subject<IChangeSet<Person, string>>();
        using var cache = source.AsObservableCache(applyLocking: false);

        var isCountCompleted = false;
        var isPreviewCompleted = false;
        using var countSubscription = cache.CountChanged.Subscribe(static _ => { }, () => isCountCompleted = true);
        using var previewSubscription = cache.Preview().Subscribe(static _ => { }, () => isPreviewCompleted = true);

        source.OnCompleted();

        isCountCompleted.Should().BeTrue("no count change can follow the end of the source");
        isPreviewCompleted.Should().BeTrue("no preview can follow the end of the source");
    }

    [Fact]
    public void PreviewCompletesWhenACacheWithoutASourceIsDisposed()
    {
        var cache = new LockFreeObservableCache<Person, string>();

        var isCompleted = false;
        using var subscription = cache.Preview().Subscribe(static _ => { }, () => isCompleted = true);

        cache.Dispose();

        isCompleted.Should().BeTrue("a disposed cache can never change again");
    }
}
