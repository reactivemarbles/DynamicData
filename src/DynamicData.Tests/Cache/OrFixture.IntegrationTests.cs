namespace DynamicData.Tests.Cache;

public partial class OrFixture
{
    public sealed class IntegrationTests
        : IntegrationTestFixtureBase
    {
        private readonly Randomizer _rand = new(0x0C0B1);

        /// <summary>
        /// One thread is parked inside the delivery of a change from the first source while another thread changes
        /// the second source. The second change must wait for the first delivery to finish rather than being delivered
        /// alongside it, which would break the Rx contract and lets operators such as <c>TakeUntil</c> drop it.
        /// </summary>
        [Theory(Timeout = 10_000)]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ChangesFromDifferentThreads_AreDeliveredSerially(bool useCollectionOverload)
        {
            // Arrange
            var first = new Person(_rand.String2(8), _rand.Number(1, 100));
            var second = new Person(_rand.String2(9), _rand.Number(1, 100));

            using var source1 = new SourceCache<Person, string>(p => p.Name);
            using var source2 = new SourceCache<Person, string>(p => p.Name);
            using var release = new ManualResetEventSlim();
            var observer = new ParkingObserver(release);

            var combined = useCollectionOverload
                ? new List<IObservable<IChangeSet<Person, string>>> { source1.Connect(), source2.Connect() }.Or()
                : source1.Connect().Or(source2.Connect());
            using var subscription = combined.SubscribeSafe(observer);

            // Act
            var firstWriter = Task.Factory.StartNew(() => source1.AddOrUpdate(first), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            await observer.Parked;
            source2.AddOrUpdate(second);
            release.Set();
            await firstWriter;

            // Assert
            observer.HasOverlapped.Should().BeFalse("a change must not be delivered while another is still being delivered");
            observer.Received.Should().Equal(first, second);
        }

        /// <summary>
        /// Records every item it receives and whether two deliveries ever overlapped. Its first delivery parks until
        /// <paramref name="release"/> is set.
        /// </summary>
        private sealed class ParkingObserver(ManualResetEventSlim release) : IObserver<IChangeSet<Person, string>>
        {
            private readonly TaskCompletionSource _parked = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private int _inFlight;
            private int _deliveries;

            public Task Parked => _parked.Task;

            public bool HasOverlapped { get; private set; }

            public List<Person> Received { get; } = [];

            public void OnNext(IChangeSet<Person, string> value)
            {
                if (Interlocked.Increment(ref _inFlight) != 1)
                    HasOverlapped = true;

                lock (Received)
                    Received.AddRange(value.Select(change => change.Current));

                if (Interlocked.Increment(ref _deliveries) == 1)
                {
                    _parked.SetResult();
                    release.Wait();
                }

                Interlocked.Decrement(ref _inFlight);
            }

            public void OnError(Exception error) => _parked.TrySetException(error);

            public void OnCompleted() { }
        }
    }
}
