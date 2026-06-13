// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;

using DynamicData.Binding;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Binding;

/// <summary>
/// Regression tests for the TOCTOU race in <c>WhenPropertyChanged</c> / <c>WhenValueChanged</c>.
/// The bug: <c>ObservablePropertyFactory</c> uses <c>initial.Concat(events)</c>, which subscribes
/// to the event source only AFTER the initial value is delivered. Any <c>PropertyChanged</c>
/// notification fired during that gap is silently dropped.
/// </summary>
public sealed class WhenPropertyChangedRaceFixture
{
    [Fact]
    public void Shallow_ConcurrentMutationDuringInitialEmit_NotDropped()
    {
        var model = new TestModel { Value = 10 };
        var emissions = new List<int>();
        var observerInitialReceived = new ManualResetEventSlim(false);
        var observerCanContinue = new ManualResetEventSlim(false);

        var observer = Observer.Create<PropertyValue<TestModel, int>>(pv =>
        {
            bool isFirst;
            lock (emissions)
            {
                isFirst = emissions.Count == 0;
                emissions.Add(pv.Value);
            }

            if (isFirst)
            {
                // Hold the OnNext open. With the BUG, Concat is blocked here and the propertyChanged
                // event handler has NOT yet been attached; a mutation now will be silently lost.
                observerInitialReceived.Set();
                observerCanContinue.Wait();
            }
        });

        // Subscribe on a worker thread so the test thread can mutate and release while Subscribe is blocked.
        var subscribeTask = Task.Run(() =>
            model.WhenPropertyChanged(m => m.Value, notifyOnInitialValue: true).Subscribe(observer));

        observerInitialReceived.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue("the worker thread must reach the initial emission");

        // Mutate while Subscribe is parked inside observer.OnNext for the initial value.
        model.Value = 20;

        observerCanContinue.Set();
        subscribeTask.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue("Subscribe must complete");

        // Allow any concurrent OnNext delivery to settle (handler may fire on another thread).
        Thread.Sleep(100);

        lock (emissions)
        {
            emissions.Should().Contain(20,
                "a PropertyChanged notification fired during the subscribe gap must not be dropped");
        }
    }

    [Fact]
    public void Shallow_NotifyInitialFalse_StillSubscribesEventBeforeReturning()
    {
        var model = new TestModel { Value = 10 };
        var emissions = new List<int>();
        var subscribeCompleted = new ManualResetEventSlim(false);

        // Subscribe on this thread; with notifyOnInitialValue: false, Subscribe should not block.
        using var sub = model.WhenPropertyChanged(m => m.Value, notifyOnInitialValue: false)
            .Subscribe(pv =>
            {
                lock (emissions) emissions.Add(pv.Value);
            });

        // The act of returning from Subscribe must mean the event handler is attached.
        model.Value = 20;

        Thread.Sleep(50);

        lock (emissions)
        {
            emissions.Should().Equal(new[] { 20 }, "no initial value requested; first emission must be the mutation");
        }
    }

    [Fact]
    public void DeepChain_ConcurrentLeafMutationDuringInitialEmit_NotDropped()
    {
        var parent = new ParentModel { Child = new ChildModel { Age = 10 } };
        var emissions = new List<int>();
        var observerInitialReceived = new ManualResetEventSlim(false);
        var observerCanContinue = new ManualResetEventSlim(false);

        var observer = Observer.Create<PropertyValue<ParentModel, int>>(pv =>
        {
            bool isFirst;
            lock (emissions)
            {
                isFirst = emissions.Count == 0;
                emissions.Add(pv.Value);
            }

            if (isFirst)
            {
                observerInitialReceived.Set();
                observerCanContinue.Wait();
            }
        });

        var subscribeTask = Task.Run(() =>
            parent.WhenPropertyChanged(p => p.Child!.Age, notifyOnInitialValue: true).Subscribe(observer));

        observerInitialReceived.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue("the worker thread must reach the initial emission");

        // Mutate the LEAF while the worker is parked inside the initial OnNext.
        parent.Child!.Age = 20;

        observerCanContinue.Set();
        subscribeTask.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue("Subscribe must complete");
        Thread.Sleep(100);

        lock (emissions)
        {
            emissions.Should().Contain(20,
                "a leaf PropertyChanged notification fired during the subscribe gap must not be dropped");
        }
    }

    [Fact]
    public void DeepChain_RewalkRace_NewChildMutationDuringRewalkNotDropped()
    {
        // Setup: subscribe to parent.Child.Age, then swap parent.Child to a new instance.
        // The orchestrator must re-walk the chain. During the re-walk, a mutation on the NEW
        // child must not be lost in the gap between disposing the old chain notifiers and
        // subscribing the new ones.
        var parent = new ParentModel { Child = new ChildModel { Age = 10 } };
        var emissions = new List<int>();
        var observerAt20 = new ManualResetEventSlim(false);
        var observerCanContinue = new ManualResetEventSlim(false);
        var observer = Observer.Create<PropertyValue<ParentModel, int>>(pv =>
        {
            int receivedAt;
            lock (emissions)
            {
                emissions.Add(pv.Value);
                receivedAt = emissions.Count;
            }

            // After the parent swap is delivered (the value 20 from the new child's initial state),
            // hold the OnNext open to widen the re-walk window on a separate thread.
            if (pv.Value == 20 && receivedAt == 2)
            {
                observerAt20.Set();
                observerCanContinue.Wait();
            }
        });

        using var sub = parent.WhenPropertyChanged(p => p.Child!.Age, notifyOnInitialValue: true).Subscribe(observer);

        var newChild = new ChildModel { Age = 20 };
        var swapTask = Task.Run(() => parent.Child = newChild);

        observerAt20.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue("the worker thread must reach the post-swap emission");

        // While we are parked inside observer.OnNext for the re-walk value, mutate the NEW child.
        // If the orchestrator drops events during chain re-walk, this mutation is lost.
        newChild.Age = 30;

        observerCanContinue.Set();
        swapTask.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();
        Thread.Sleep(100);

        lock (emissions)
        {
            emissions.Should().Contain(30,
                "a mutation on the new child during the chain re-walk must not be dropped");
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
}
