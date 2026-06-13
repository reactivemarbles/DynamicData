// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.Binding;

internal sealed class ObservablePropertyFactory<TObject, TProperty>
    where TObject : INotifyPropertyChanged
{
    private readonly Func<TObject, bool, IObservable<PropertyValue<TObject, TProperty>>> _factory;

    public ObservablePropertyFactory(Func<TObject, TProperty> valueAccessor, ObservablePropertyPart[] chain)
    {
        // chain is stored leaf-first (output of SplitIntoSteps). Reverse once to root-to-leaf order
        // so chain navigation is index-monotonic: rootToLeaf[0] is the first property accessed off
        // the source, rootToLeaf[Length - 1] is the leaf.
        var rootToLeaf = System.Linq.Enumerable.Reverse((IEnumerable<ObservablePropertyPart>)chain).ToArray();
        var depth = rootToLeaf.Length;

        _factory = (t, notifyInitial) => Observable.Create<PropertyValue<TObject, TProperty>>(observer =>
        {
            // Race-free chain orchestration:
            //   - Per-level SerialDisposable means every chain level always has an active
            //     subscription. When a parent-level notifier fires, the deeper levels'
            //     subscriptions are atomically swapped to point at the new parent's new child.
            //   - A single-drainer pattern serializes re-walks: any thread can signal "level X
            //     needs re-walking" via _minDirtyLevel; the winner of an Interlocked CAS on
            //     _drainerActive runs the actual work. Other threads return immediately. This
            //     ensures the FINAL slot state always reflects the LATEST chain state.
            //   - CAS-based first-emission-wins ensures the initial emission and the first
            //     handler-fired emission cannot both reach the observer.
            //   - One-shot equality guard on the first handler emission after the initial
            //     eliminates the rare setter-update-then-notify duplicate.

            var levelSlots = new SerialDisposable[depth];
            for (var i = 0; i < depth; i++)
            {
                levelSlots[i] = new SerialDisposable();
            }

            var initialClaimed = 0;
            var dedupArmed = 0;
            PropertyValue<TObject, TProperty>? seedValue = null;
            var drainerActive = 0;
            // _minDirtyLevel is the lowest level (inclusive) whose deeper-chain subscriptions
            // need re-attaching. int.MaxValue means "nothing to do".
            var minDirtyLevel = int.MaxValue;

            void Emit(PropertyValue<TObject, TProperty> value)
            {
                if (Interlocked.CompareExchange(ref initialClaimed, 1, 0) == 0)
                {
                    observer.OnNext(value);
                    return;
                }

                if (Interlocked.CompareExchange(ref dedupArmed, 1, 0) == 0)
                {
                    var seed = Volatile.Read(ref seedValue);
                    if (seed is not null && PropertyValuesEqual(seed, value))
                    {
                        return;
                    }
                }

                observer.OnNext(value);
            }

            void UpdateMinDirty(int newLevel)
            {
                // CAS loop to atomically set _minDirtyLevel to min(current, newLevel).
                while (true)
                {
                    var current = Volatile.Read(ref minDirtyLevel);
                    if (current <= newLevel)
                    {
                        return;
                    }

                    if (Interlocked.CompareExchange(ref minDirtyLevel, newLevel, current) == current)
                    {
                        return;
                    }
                }
            }

            void OnLevelChanged(int level)
            {
                // A notifier at `level` fired: deeper levels (level + 1 onward) must be re-attached.
                UpdateMinDirty(level + 1);
                Drain();
            }

            void Drain()
            {
                if (Interlocked.CompareExchange(ref drainerActive, 1, 0) != 0)
                {
                    return;
                }

                while (true)
                {
                    try
                    {
                        while (true)
                        {
                            var startLevel = Interlocked.Exchange(ref minDirtyLevel, int.MaxValue);
                            if (startLevel == int.MaxValue)
                            {
                                break;
                            }

                            ResubscribeFrom(startLevel);
                            Emit(GetPropertyValueRootToLeaf(t, rootToLeaf, valueAccessor));
                        }
                    }
                    finally
                    {
                        Volatile.Write(ref drainerActive, 0);
                    }

                    // Re-check: signals may have arrived between Exchange-to-MaxValue and the
                    // drainerActive release. If so, try to re-claim the drainer.
                    if (Volatile.Read(ref minDirtyLevel) == int.MaxValue)
                    {
                        return;
                    }

                    if (Interlocked.CompareExchange(ref drainerActive, 1, 0) != 0)
                    {
                        // Someone else claimed the drainer; they will handle the signaled work.
                        return;
                    }
                }
            }

            void ResubscribeFrom(int startLevel)
            {
                // Called ONLY from inside Drain (single-drainer invariant). Walks root-to-leaf to
                // the value at startLevel; then attaches a notifier at each subsequent level and
                // atomically swaps each slot via SerialDisposable.Disposable=. At all times,
                // every live chain level has an active notifier.
                if (startLevel >= depth)
                {
                    return;
                }

                object? value = t;
                for (var i = 0; i < startLevel; i++)
                {
                    value = rootToLeaf[i].Invoker(value);
                    if (value is null)
                    {
                        for (var j = startLevel; j < depth; j++)
                        {
                            levelSlots[j].Disposable = Disposable.Empty;
                        }

                        return;
                    }
                }

                for (var i = startLevel; i < depth; i++)
                {
                    if (value is null)
                    {
                        levelSlots[i].Disposable = Disposable.Empty;
                        continue;
                    }

                    var levelIndex = i;
                    var notifier = rootToLeaf[i].Factory(value);
                    levelSlots[i].Disposable = notifier.Subscribe(_ => OnLevelChanged(levelIndex));

                    value = rootToLeaf[i].Invoker(value);
                }
            }

            // Initial setup is serialized via the drainer-claim so any concurrent notifier-fired
            // re-walk cannot run interleaved with our ResubscribeFrom(0). Notifier handlers fired
            // during this window will signal _minDirtyLevel and return; the drain below picks them
            // up. On a fresh subscription, no notifiers exist before ResubscribeFrom attaches them,
            // so the claim is uncontended.
            var priorDrainer = Interlocked.CompareExchange(ref drainerActive, 1, 0);
            Debug.Assert(priorDrainer == 0, "drainer must be free on initial subscription");

            try
            {
                ResubscribeFrom(0);

                if (notifyInitial)
                {
                    var initial = GetPropertyValueRootToLeaf(t, rootToLeaf, valueAccessor);
                    if (Interlocked.CompareExchange(ref initialClaimed, 1, 0) == 0)
                    {
                        Volatile.Write(ref seedValue, initial);
                        observer.OnNext(initial);
                    }
                }
            }
            finally
            {
                Volatile.Write(ref drainerActive, 0);
            }

            // Concurrent mutations during setup may have signaled _minDirtyLevel. Drain them now.
            if (Volatile.Read(ref minDirtyLevel) != int.MaxValue)
            {
                Drain();
            }

            return new CompositeDisposable(levelSlots);
        });
    }

    public ObservablePropertyFactory(Expression<Func<TObject, TProperty>> expression)
    {
        // this overload is used for shallow observations i.e. depth = 1, so no need for re-subscriptions
        var member = expression.GetProperty();
        var accessor = expression.Compile();
        var memberName = member.Name;

        _factory = (t, notifyInitial) => Observable.Create<PropertyValue<TObject, TProperty>>(observer =>
        {
            PropertyValue<TObject, TProperty> Read() => new(t, accessor(t));

            var initialClaimed = 0;
            var dedupArmed = 0;
            PropertyValue<TObject, TProperty>? seedValue = null;

            void OnPropertyChanged(object? sender, PropertyChangedEventArgs args)
            {
                if (args.PropertyName != memberName)
                {
                    return;
                }

                var value = Read();

                if (Interlocked.CompareExchange(ref initialClaimed, 1, 0) == 0)
                {
                    // Handler beat the initial-read to the first-emission slot. Pass through.
                    observer.OnNext(value);
                    return;
                }

                // initial already emitted (or another handler emission claimed the slot).
                // First post-initial handler emission may be a duplicate due to the
                // setter-update-then-notify race; apply the one-shot dedup guard.
                if (Interlocked.CompareExchange(ref dedupArmed, 1, 0) == 0)
                {
                    var seed = Volatile.Read(ref seedValue);
                    if (seed is not null && PropertyValuesEqual(seed, value))
                    {
                        return;
                    }
                }

                observer.OnNext(value);
            }

            // Attach the PropertyChanged handler FIRST so no notifications are missed during the
            // initial read window.
            t.PropertyChanged += OnPropertyChanged;

            if (notifyInitial)
            {
                var initial = Read();
                if (Interlocked.CompareExchange(ref initialClaimed, 1, 0) == 0)
                {
                    Volatile.Write(ref seedValue, initial);
                    observer.OnNext(initial);
                }
            }

            return Disposable.Create(() => t.PropertyChanged -= OnPropertyChanged);
        });
    }

    public IObservable<PropertyValue<TObject, TProperty>> Create(TObject source, bool notifyInitial) => _factory(source, notifyInitial);

    // Root-to-leaf chain walk. Stops at null and returns an unobtainable PropertyValue.
    private static PropertyValue<TObject, TProperty> GetPropertyValueRootToLeaf(TObject source, ObservablePropertyPart[] rootToLeaf, Func<TObject, TProperty> valueAccessor)
    {
        object? value = source;
        foreach (var metadata in rootToLeaf)
        {
            value = metadata.Invoker(value);
            if (value is null)
            {
                return new PropertyValue<TObject, TProperty>(source);
            }
        }

        return new PropertyValue<TObject, TProperty>(source, valueAccessor(source));
    }

    // One-shot dedup comparison: equal when both have a value AND values are equal,
    // OR both are unobtainable. Avoids importing DistinctUntilChanged semantics into the
    // general operator (this is a single comparison at the initial-handler boundary).
    private static bool PropertyValuesEqual(PropertyValue<TObject, TProperty> a, PropertyValue<TObject, TProperty> b)
    {
        if (a.UnobtainableValue != b.UnobtainableValue)
        {
            return false;
        }

        if (a.UnobtainableValue)
        {
            return true;
        }

        return EqualityComparer<TProperty>.Default.Equals(a.Value!, b.Value!);
    }
}
