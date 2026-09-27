// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;

using DynamicData.Binding;
using DynamicData.Tests.Utilities;
using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Binding;

/// <summary>
/// Single-threaded contract tests for <see cref="NotifyPropertyChangedEx.WhenPropertyChanged{TObject, TProperty}"/>:
/// handler attachment ordering, subscription cleanup, no-dedup semantics, and deep-chain swaps.
/// </summary>
public sealed class WhenPropertyChangedBehaviorFixture
{
    /// <summary>An arbitrary observed value; these tests assert handler lifetime, not the value itself.</summary>
    private const double ObservedAmount = 41.375;

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

    // https://github.com/reactivemarbles/DynamicData/issues/1149
    [Fact]
    public void ExpressionContainsImplicitInterfaceCast()
    {
        var child = new ChildModel()
        {
            Age = 10
        };
        
        using var subscription = ObserveAge(child)
            .RecordValues(out var results);

        results.Error.Should().BeNull("no errors should have occurred");
        results.RecordedValues.Should().ContainSingle("the initial value of the observed expression should have been published");
        results.RecordedValues[0].Should().Be(child.Age, "the initial value of the observed expression should have been published");

        ++child.Age;

        results.Error.Should().BeNull("no errors should have occurred");
        results.RecordedValues.Skip(1).Should().ContainSingle("the value of the observed expression changed once");
        results.RecordedValues[1].Should().Be(child.Age, "the correct value should have been published");

        static IObservable<int> ObserveAge<T>(T source)
                where T : IHasAge
            => source.WhenValueChanged(source => source.Age);
    }

    /// <summary>Verifies that a throwing initial observer leaves no property-change handler attached.</summary>
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

    /// <summary>Verifies that a throwing initial observer releases property-change handlers at every chain level.</summary>
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

    /// <summary>Verifies that an initial getter failure releases the handler when the default error handler throws.</summary>
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

    /// <summary>Verifies that a failing chain getter releases every handler when the default error handler throws.</summary>
    /// <param name="notifyOnInitialValue">Whether subscribing requests an initial value notification.</param>
    /// <param name="failBeforeLeaf">Whether an intermediate getter fails before the leaf can be subscribed.</param>
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

    /// <summary>Verifies that a handled initial getter failure terminates observation and releases its event handler.</summary>
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

    /// <summary>Verifies that a handled chain getter failure terminates observation and releases every event handler.</summary>
    /// <param name="notifyOnInitialValue">Whether subscribing requests an initial value notification.</param>
    /// <param name="failBeforeLeaf">Whether an intermediate getter fails before the leaf can be subscribed.</param>
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

    /// <summary>Verifies that live property handlers belong to the returned subscription until it is disposed.</summary>
    /// <param name="deepChain">Whether the observed property is reached through intermediate objects.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Subscription_ExplicitDisposal_ReleasesHandlers(bool deepChain)
    {
        // Arrange
        var leaf = new ObservablePrice { Amount = ObservedAmount };
        var child = new ObservablePrice { Child = leaf };
        var root = new ObservablePrice { Child = child };
        var models = deepChain ? new[] { root, child, leaf } : new[] { leaf };
        var source = deepChain
            ? root.WhenValueChanged(static price => price.Child!.Child!.Amount)
            : leaf.WhenValueChanged(static price => price.Amount);
        using var subscription = source.RecordValues(out var results);
        var attachedHandlerCounts = models.Select(model => model.HandlerCount).ToArray();

        // Act
        subscription.Dispose();

        // Assert
        attachedHandlerCounts.Should().OnlyContain(count => count == 1, because: "each visited object must stay subscribed after initialization");
        models.Select(model => model.HandlerCount).Should().OnlyContain(count => count == 0,
            because: "disposing the returned subscription must release every retained handler");
        results.RecordedValues.Should().Equal(new[] { ObservedAmount }, because: "initialization must publish the observed value");
        results.Error.Should().BeNull(because: "explicit disposal is not an observation failure");
        results.HasCompleted.Should().BeFalse(because: "unsubscribing does not publish a completion notification");
    }

    /// <summary>Verifies that synchronous completion during initial delivery releases every property handler.</summary>
    /// <param name="deepChain">Whether the observed property is reached through intermediate objects.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Subscription_SynchronousCompletion_ReleasesHandlers(bool deepChain)
    {
        // Arrange
        var leaf = new ObservablePrice { Amount = ObservedAmount };
        var child = new ObservablePrice { Child = leaf };
        var root = new ObservablePrice { Child = child };
        var models = deepChain ? new[] { root, child, leaf } : new[] { leaf };
        var source = deepChain
            ? root.WhenValueChanged(static price => price.Child!.Child!.Amount)
            : leaf.WhenValueChanged(static price => price.Amount);

        // Act
        using var subscription = source.Take(1)
            .RecordValues(out var results);

        // Assert
        results.RecordedValues.Should().Equal(new[] { ObservedAmount }, because: "the requested initial value must be delivered");
        results.Error.Should().BeNull(because: "taking an initial value is normal completion");
        results.HasCompleted.Should().BeTrue(because: "Take completes after receiving its requested value");
        models.Should().OnlyContain(model => model.WasSubscribed, because: "handlers must attach before initial delivery");
        models.Select(model => model.HandlerCount).Should().OnlyContain(count => count == 0,
            because: "synchronous completion must release handlers before Subscribe returns");
    }

    /// <summary>An observable input whose custom event accessors expose property subscription lifetimes.</summary>
    public sealed class ObservablePrice : INotifyPropertyChanged
    {
        private double _amount;
        private ObservablePrice? _child;
        private PropertyChangedEventHandler? _propertyChanged;

        /// <inheritdoc />
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add
            {
                WasSubscribed = true;
                _propertyChanged += value;
            }

            remove => _propertyChanged -= value;
        }

        /// <summary>Gets or sets the amount and raises a property-change notification when set.</summary>
        public double Amount
        {
            get => ReadError is null ? _amount : throw ReadError;
            set
            {
                _amount = value;
                _propertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Amount)));
            }
        }

        /// <summary>Gets the next object in a nested property path.</summary>
        public ObservablePrice? Child
        {
            get => ChildReadError is null ? _child : throw ChildReadError;
            init => _child = value;
        }

        /// <summary>Gets an optional failure raised when reading <see cref="Child"/>.</summary>
        public InvalidOperationException? ChildReadError { get; init; }

        /// <summary>Gets the number of event handlers retained by this object.</summary>
        public int HandlerCount => _propertyChanged?.GetInvocationList().Length ?? 0;

        /// <summary>Gets an optional failure raised when reading <see cref="Amount"/>.</summary>
        public InvalidOperationException? ReadError { get; init; }

        /// <summary>Gets whether any observer has registered a property-change handler.</summary>
        public bool WasSubscribed { get; private set; }
    }
    
    private interface IHasAge
        : INotifyPropertyChanged
    {
        int Age { get; }
    }

    private sealed class TestModel : INotifyPropertyChanged
    {
        private int _value;

        public event PropertyChangedEventHandler? PropertyChanged;

        public int Value
        {
            get => _value;
            set
            {
                _value = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }
    }

    private sealed class ParentModel : INotifyPropertyChanged
    {
        private ChildModel? _child;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ChildModel? Child
        {
            get => _child;
            set
            {
                _child = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Child)));
            }
        }
    }

    private sealed class ChildModel
        : IHasAge
    {
        private int _age;

        public event PropertyChangedEventHandler? PropertyChanged;

        public int Age
        {
            get => _age;
            set
            {
                _age = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Age)));
            }
        }
    }

    private sealed class Level1 : INotifyPropertyChanged
    {
        private Level2? _child;

        public event PropertyChangedEventHandler? PropertyChanged;

        public Level2? Child
        {
            get => _child;
            set
            {
                _child = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Child)));
            }
        }
    }

    private sealed class Level2 : INotifyPropertyChanged
    {
        private Level3? _child;

        public event PropertyChangedEventHandler? PropertyChanged;

        public Level3? Child
        {
            get => _child;
            set
            {
                _child = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Child)));
            }
        }
    }

    private sealed class Level3 : INotifyPropertyChanged
    {
        private Level4? _child;

        public event PropertyChangedEventHandler? PropertyChanged;

        public Level4? Child
        {
            get => _child;
            set
            {
                _child = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Child)));
            }
        }
    }

    private sealed class Level4 : INotifyPropertyChanged
    {
        private int _leaf;

        public event PropertyChangedEventHandler? PropertyChanged;

        public int Leaf
        {
            get => _leaf;
            set
            {
                _leaf = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Leaf)));
            }
        }
    }
}
