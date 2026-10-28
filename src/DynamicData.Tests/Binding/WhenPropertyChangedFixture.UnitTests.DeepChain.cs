namespace DynamicData.Tests.Binding;

public static partial class WhenPropertyChangedFixture
{
    public partial class UnitTests
    {
        [Theory]
        [InlineData(true, false)]
        [InlineData(true, true)]
        [InlineData(false, true)]
        public void DeepChain_InitialGetterThrows_DefaultErrorHandler_DetachesEveryHandler(bool notifyOnInitialValue, bool failBeforeLeaf)
        {
            // Arrange
            var error = new InvalidOperationException();
            var leaf = new ObservablePrice { Amount = ObservedAmount, ReadError = failBeforeLeaf ? null : error };
            var child = new ObservablePrice { Child = leaf, ChildReadError = failBeforeLeaf ? error : null };
            var root = new ObservablePrice { Child = child };
            var models = new[] { root, child, leaf };
            var source = root.WhenValueChanged(static price => price.Child!.Child!.Amount, notifyOnInitialValue);

            // Act
            Action subscribe = () =>
            {
                using var subscription = source.Subscribe();
            };

            // Assert
            subscribe.Should().Throw<Exception>(because: "the default Rx error handler must rethrow initialization failures")
                .Which.GetBaseException().Should().BeSameAs(error, because: "the failure must originate in the observed getter");
            root.WasSubscribed.Should().BeTrue(because: "the root handler must attach before its child is read");
            child.WasSubscribed.Should().BeTrue(because: "the intermediate handler must attach before its child is read");
            leaf.WasSubscribed.Should().Be(!failBeforeLeaf, because: "the leaf is reachable only if the intermediate getter succeeds");
            models.Select(model => model.HandlerCount).Should().OnlyContain(count => count == 0,
                because: "failed initialization must release handlers at every visited level");
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(true, true)]
        [InlineData(false, true)]
        public void DeepChain_InitialGetterThrows_ErrorIsRecordedAndEveryHandlerDetached(bool notifyOnInitialValue, bool failBeforeLeaf)
        {
            // Arrange
            var error = new InvalidOperationException();
            var leaf = new ObservablePrice { Amount = ObservedAmount, ReadError = failBeforeLeaf ? null : error };
            var child = new ObservablePrice { Child = leaf, ChildReadError = failBeforeLeaf ? error : null };
            var root = new ObservablePrice { Child = child };
            var models = new[] { root, child, leaf };

            // Act
            using var subscription = root.WhenPropertyChanged(static price => price.Child!.Child!.Amount, notifyOnInitialValue)
                .RecordValues(out var results);

            // Assert
            results.Error.Should().NotBeNull(because: "chain getter failures must be delivered through OnError");
            results.Error!.GetBaseException().Should().BeSameAs(error, because: "the failure must originate in the observed getter");
            results.RecordedValues.Should().BeEmpty(because: "the chain did not produce an obtainable value");
            results.HasCompleted.Should().BeFalse(because: "OnError is the terminal notification");
            root.WasSubscribed.Should().BeTrue(because: "the root handler must attach before its child is read");
            child.WasSubscribed.Should().BeTrue(because: "the intermediate handler must attach before its child is read");
            leaf.WasSubscribed.Should().Be(!failBeforeLeaf, because: "the leaf is reachable only if the intermediate getter succeeds");
            models.Select(model => model.HandlerCount).Should().OnlyContain(count => count == 0,
                because: "OnError must release every handler before Subscribe returns");
        }

        [Fact]
        public void DeepChain_InitialObserverThrows_DetachesEveryHandler()
        {
            // Arrange
            var leaf = new ObservablePrice { Amount = ObservedAmount };
            var child = new ObservablePrice { Child = leaf };
            var root = new ObservablePrice { Child = child };
            var models = new[] { root, child, leaf };
            var error = new InvalidOperationException();
            var results = new ValueRecordingObserver<double>(ImmediateScheduler.Instance);
            IObserver<double> observer = results;
            var source = root.WhenValueChanged(static price => price.Child!.Child!.Amount);

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
            models.Should().OnlyContain(model => model.WasSubscribed, because: "each observable level must be registered before it is read");
            models.Select(model => model.HandlerCount).Should().OnlyContain(count => count == 0,
                because: "failed initialization must release every handler, not just the root handler");
        }

        [Fact]
        public void DeepChain_MidChainSwap_DeeperLevelsRetargetCorrectly()
        {
            // Mid-chain swap on a 4-level chain. When level 3 is reassigned, the leaf subscription
            // must re-attach against the new subtree; events on the old subtree must be ignored
            // (its notifier subscription was disposed).
            var l1 = new Level1
            {
                Child = new Level2
                {
                    Child = new Level3
                    {
                        Child = new Level4 { Leaf = 10 },
                    },
                },
            };

            var emissions = new List<int>();
            using var sub = l1.WhenPropertyChanged(x => x.Child!.Child!.Child!.Leaf, notifyOnInitialValue: true)
                .Subscribe(pv => emissions.Add(pv.Value));

            emissions.Should().Equal(new[] { 10 }, "initial emission");

            var originalLeaf = l1.Child!.Child!.Child!;

            var newL4 = new Level4 { Leaf = 20 };
            l1.Child!.Child!.Child = newL4;

            emissions.Should().Equal(new[] { 10, 20 }, "mid-chain swap emits the new leaf value");

            newL4.Leaf = 30;
            emissions.Should().Equal(new[] { 10, 20, 30 }, "leaf event on new subtree is captured");

            originalLeaf.Leaf = 999;
            emissions.Should().Equal(new[] { 10, 20, 30 }, "leaf event on detached subtree is ignored");
        }

        [Fact]
        public void DeepChain_NotifyInitialFalse_DoesNotDedupSameValuedEvents()
        {
            var parent = new ParentModel { Child = new ChildModel { Age = 1 } };
            var emissions = new List<int>();

            using var sub = parent.WhenPropertyChanged(p => p.Child!.Age, notifyOnInitialValue: false)
                .Subscribe(pv => emissions.Add(pv.Value));

            parent.Child!.Age = 7;
            parent.Child!.Age = 7;

            emissions.Should().Equal(new[] { 7, 7 });
        }

        [Fact]
        public void DeepChain_PostSwap_LeafEventOnNewChild_Captured()
        {
            // After parent.Child is reassigned, the leaf-level subscription must be re-attached
            // against the new child. A subsequent leaf mutation on the new child must be captured.
            var parent = new ParentModel { Child = new ChildModel { Age = 10 } };
            var emissions = new List<int>();

            using var sub = parent.WhenPropertyChanged(p => p.Child!.Age, notifyOnInitialValue: true)
                .Subscribe(pv => emissions.Add(pv.Value));

            var newChild = new ChildModel { Age = 20 };
            parent.Child = newChild;
            newChild.Age = 30;

            emissions.Should().Equal(new[] { 10, 20, 30 });
        }

        [Fact]
        public void DeepChain_NotifyInitialTrue_DoesNotDedupSameValuedEvents()
        {
            var parent = new ParentModel { Child = new ChildModel { Age = 1 } };
            var emissions = new List<int>();

            using var sub = parent.WhenPropertyChanged(p => p.Child!.Age, notifyOnInitialValue: true)
                .Subscribe(pv => emissions.Add(pv.Value));

            parent.Child!.Age = 1;
            parent.Child!.Age = 1;
            parent.Child!.Age = 1;

            emissions.Should().Equal(new[] { 1, 1, 1, 1 });
        }
    }
}
