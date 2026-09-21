// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;

using DynamicData.Binding;
using DynamicData.Tests.Utilities;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Binding;

public sealed partial class WhenPropertyChangedBehaviorFixture
{
    /// <summary>Verifies that a throwing initial observer leaves no property-change handler attached.</summary>
    [Fact]
    public void Shallow_InitialObserverThrows_DetachesHandler()
    {
        // Arrange
        var amount = _randomizer.Double();
        var model = new ObservablePrice { Amount = amount };
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
        results.RecordedValues.Should().Equal(new[] { amount }, because: "the failure occurs during initial delivery");
        results.Error.Should().BeNull(because: "an observer failure must not be converted into an OnError notification");
        model.WasSubscribed.Should().BeTrue(because: "registration must precede the initial value read");
        model.HandlerCount.Should().Be(0, because: "a throwing Subscribe cannot return a disposable to its caller");
    }

    /// <summary>Verifies that a throwing initial observer releases property-change handlers at every chain level.</summary>
    [Fact]
    public void DeepChain_InitialObserverThrows_DetachesEveryHandler()
    {
        // Arrange
        var amount = _randomizer.Double();
        var leaf = new ObservablePrice { Amount = amount };
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
        results.RecordedValues.Should().Equal(new[] { amount }, because: "the failure occurs during initial delivery");
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
        var model = new ObservablePrice { Amount = _randomizer.Double(), ReadError = error };
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
        var leaf = new ObservablePrice { Amount = _randomizer.Double(), ReadError = failBeforeLeaf ? null : error };
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
        var model = new ObservablePrice { Amount = _randomizer.Double(), ReadError = error };

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
        var leaf = new ObservablePrice { Amount = _randomizer.Double(), ReadError = failBeforeLeaf ? null : error };
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
        var amount = _randomizer.Double();
        var leaf = new ObservablePrice { Amount = amount };
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
        results.RecordedValues.Should().Equal(new[] { amount }, because: "initialization must publish the observed value");
        results.Error.Should().BeNull(because: "explicit disposal is not an observation failure");
        results.HasCompleted.Should().BeFalse(because: "unsubscribing does not publish a completion notification");
    }

    /// <summary>Verifies that synchronous completion during initial delivery releases every property handler.</summary>
    /// <param name="deepChain">Whether the observed property is reached through intermediate objects.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Subscription_TakeInitialValue_ReleasesHandlers(bool deepChain)
    {
        // Arrange
        var amount = _randomizer.Double();
        var leaf = new ObservablePrice { Amount = amount };
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
        results.RecordedValues.Should().Equal(new[] { amount }, because: "the requested initial value must be delivered");
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
}
