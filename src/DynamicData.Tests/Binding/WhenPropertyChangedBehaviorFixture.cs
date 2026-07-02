// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;

using DynamicData.Binding;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Binding;

/// <summary>
/// Single-threaded contract tests for <see cref="NotifyPropertyChangedEx.WhenPropertyChanged{TObject, TProperty}"/>:
/// handler attachment ordering, no-dedup semantics, deep-chain re-walks on swaps.
/// </summary>
public sealed class WhenPropertyChangedBehaviorFixture
{
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

    [Fact]
    public void Shallow_ObserverThrowsInOnNext_RoutedToOnError_DoesNotEscapeSetter()
    {
        // Emissions originate from an INotifyPropertyChanged callback. A subscriber whose OnNext
        // throws must not surface that exception raw out of the property setter; it is routed
        // through OnError so a subscriber that supplied an error handler can observe it, and the
        // setter that raised PropertyChanged stays safe.
        var model = new TestModel { Value = 10 };

        using var sub = model.WhenPropertyChanged(m => m.Value, notifyOnInitialValue: false)
            .Subscribe(_ => throw new InvalidOperationException("boom"));

        var setter = () => model.Value = 20;
        var laterSetter = () => model.Value = 30;

        setter.Should().NotThrow("a PropertyChanged-sourced OnNext throw is routed to OnError, not escaped to the setter");
        laterSetter.Should().NotThrow("the terminated subscription must not resurface the exception on a later setter");
    }

    [Fact]
    public void Shallow_AccessorThrows_ErrorHandlerRethrows_PropagatesToSetter()
    {
        // We route accessor/OnNext failures to OnError but do NOT swallow a throw from OnError
        // itself: an unhandled downstream error (here a rethrowing error handler) propagates to
        // the setter, matching Subject<T>.OnNext semantics. Locks in the non-swallow decision.
        var model = new ThrowingGetterModel { Value = 10 };

        using var sub = model.WhenPropertyChanged(m => m.Value, notifyOnInitialValue: false)
            .Subscribe(_ => { }, _ => throw new InvalidOperationException("onerror"));

        model.ThrowOnGet = true;
        var setter = () => model.Value = 20;

        setter.Should().Throw<InvalidOperationException>()
            .WithMessage("onerror", "a rethrowing error handler must not be swallowed");
    }

    [Fact]
    public void DeepChain_ObserverThrowsInOnNext_RoutedToOnError_DoesNotEscapeSetter()
    {
        var parent = new ParentModel { Child = new ChildModel { Age = 10 } };

        using var sub = parent.WhenPropertyChanged(p => p.Child!.Age, notifyOnInitialValue: false)
            .Subscribe(_ => throw new InvalidOperationException("boom"));

        var setter = () => parent.Child!.Age = 20;
        var laterSetter = () => parent.Child!.Age = 30;

        setter.Should().NotThrow("a PropertyChanged-sourced OnNext throw is routed to OnError, not escaped to the setter");
        laterSetter.Should().NotThrow("the terminated subscription must not resurface the exception on a later setter");
    }

    [Fact]
    public void DeepChain_AccessorThrows_ErrorHandlerRethrows_PropagatesToSetter()
    {
        var parent = new ThrowingLeafParent { Child = new ThrowingGetterModel { Value = 10 } };

        using var sub = parent.WhenPropertyChanged(p => p.Child!.Value, notifyOnInitialValue: false)
            .Subscribe(_ => { }, _ => throw new InvalidOperationException("onerror"));

        parent.Child!.ThrowOnGet = true;
        var setter = () => parent.Child!.Value = 20;

        setter.Should().Throw<InvalidOperationException>()
            .WithMessage("onerror", "a rethrowing error handler must not be swallowed");
    }

    private sealed class ThrowingGetterModel : INotifyPropertyChanged
    {
        private int _value;

        public event PropertyChangedEventHandler? PropertyChanged;

        public bool ThrowOnGet { get; set; }

        public int Value
        {
            get => ThrowOnGet ? throw new InvalidOperationException("getter") : _value;
            set
            {
                _value = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }
    }

    private sealed class ThrowingLeafParent : INotifyPropertyChanged
    {
        private ThrowingGetterModel? _child;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ThrowingGetterModel? Child
        {
            get => _child;
            set
            {
                _child = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Child)));
            }
        }
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

    private sealed class ChildModel : INotifyPropertyChanged
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
