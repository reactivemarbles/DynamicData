namespace DynamicData.Tests.Binding;

public static partial class WhenPropertyChangedFixture
{
    public partial class UnitTests
    {
        [Fact]
        public void Shallow_InitialGetterThrows_DefaultErrorHandler_DetachesHandler()
        {
            // Arrange
            var error = new InvalidOperationException();
            var model = new ObservablePrice { Amount = ObservedAmount, ReadError = error };
            var source = model.WhenValueChanged(static price => price.Amount);

            // Act
            Action subscribe = () =>
            {
                using var subscription = source.Subscribe();
            };

            // Assert
            subscribe.Should().Throw<InvalidOperationException>(because: "the default Rx error handler must rethrow the getter failure")
                .Which.Should().BeSameAs(error, because: "the original getter failure must be preserved");
            model.WasSubscribed.Should().BeTrue(because: "registration must precede the initial value read");
            model.HandlerCount.Should().Be(0, because: "failed initialization must not retain the event handler");
        }

        [Fact]
        public void Shallow_InitialGetterThrows_ErrorIsRecordedAndHandlerDetached()
        {
            // Arrange
            var error = new InvalidOperationException();
            var model = new ObservablePrice { Amount = ObservedAmount, ReadError = error };

            // Act
            using var subscription = model.WhenPropertyChanged(static price => price.Amount)
                .RecordValues(out var results);

            // Assert
            results.Error.Should().BeSameAs(error, because: "getter failures must be delivered through OnError");
            results.RecordedValues.Should().BeEmpty(because: "the initial getter did not produce a value");
            results.HasCompleted.Should().BeFalse(because: "OnError is the terminal notification");
            model.WasSubscribed.Should().BeTrue(because: "registration must precede the initial value read");
            model.HandlerCount.Should().Be(0, because: "OnError must release the handler before Subscribe returns");
        }

        [Fact]
        public void Shallow_InitialObserverThrows_DetachesHandler()
        {
            // Arrange
            var model = new ObservablePrice { Amount = ObservedAmount };
            var error = new InvalidOperationException();
            var results = new ValueRecordingObserver<double>(ImmediateScheduler.Instance);
            IObserver<double> observer = results;
            var source = model.WhenValueChanged(static price => price.Amount);

            // Act
            Action subscribe = () =>
            {
                using var subscription = source.Subscribe(value =>
                {
                    observer.OnNext(value);
                    throw error;
                }, observer.OnError);
            };

            // Assert
            subscribe.Should().Throw<InvalidOperationException>(because: "observer failures must escape Subscribe")
                .Which.Should().BeSameAs(error, because: "the original observer failure must be preserved");
            results.RecordedValues.Should().Equal(new[] { ObservedAmount }, because: "the failure occurs during initial delivery");
            results.Error.Should().BeNull(because: "an observer failure must not be converted into an OnError notification");
            model.WasSubscribed.Should().BeTrue(because: "registration must precede the initial value read");
            model.HandlerCount.Should().Be(0, because: "a throwing Subscribe cannot return a disposable to its caller");
        }

        [Fact]
        public void Shallow_NotifyInitialFalse_DoesNotDedupSameValuedEvents()
        {
            var model = new TestModel { Value = 10 };
            var emissions = new List<int>();

            using var sub = model.WhenPropertyChanged(m => m.Value, notifyOnInitialValue: false)
                .Subscribe(pv => emissions.Add(pv.Value));

            model.Value = 42;
            model.Value = 42;

            emissions.Should().Equal(new[] { 42, 42 });
        }

        [Fact]
        public void Shallow_NotifyInitialFalse_SubscribesHandlerBeforeReturning()
        {
            // notifyOnInitialValue=false: Subscribe must return only after the PropertyChanged handler
            // is attached. A setter that fires immediately after Subscribe returns must reach the
            // observer.
            var model = new TestModel { Value = 10 };
            var emissions = new List<int>();

            using var sub = model.WhenPropertyChanged(m => m.Value, notifyOnInitialValue: false)
                .Subscribe(pv => emissions.Add(pv.Value));

            model.Value = 20;

            emissions.Should().Equal(new[] { 20 });
        }

        [Fact]
        public void Shallow_NotifyInitialTrue_DoesNotDedupSameValuedEvents()
        {
            var model = new TestModel { Value = 10 };
            var emissions = new List<int>();

            using var sub = model.WhenPropertyChanged(m => m.Value, notifyOnInitialValue: true)
                .Subscribe(pv => emissions.Add(pv.Value));

            model.Value = 10;
            model.Value = 10;
            model.Value = 10;

            emissions.Should().Equal(new[] { 10, 10, 10, 10 });
        }
    }
}
