// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.ComponentModel;
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
            //     subscriptions are atomically swapped to point at the new parent's new
            //     child (subscribe new BEFORE disposing old).
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

            void Emit(PropertyValue<TObject, TProperty> value)
            {
                if (Interlocked.CompareExchange(ref initialClaimed, 1, 0) == 0)
                {
                    observer.OnNext(value);
                    return;
                }

                // initial has been emitted (or another handler-fired value claimed the slot).
                // Apply the one-shot dedup guard to the very first post-initial handler emission
                // to catch the rare setter-update-then-notify race.
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

            void OnLevelChanged(int level)
            {
                // A notifier at `level` fired: every deeper level's subscription must be
                // re-targeted to the new value at that level. Re-attach from level + 1 onward.
                ResubscribeFrom(level + 1);
                Emit(GetPropertyValueRootToLeaf(t, rootToLeaf, valueAccessor));
            }

            void ResubscribeFrom(int startLevel)
            {
                // Walk root-to-leaf to the value at startLevel; then attach a notifier at each
                // subsequent level and atomically swap each slot. SerialDisposable.Disposable
                // assignment disposes the old subscription after the new one is in place, so
                // no events are dropped: at all times, every live chain level has an active notifier.
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

            // Subscribe to all chain levels BEFORE reading the initial value: this closes the
            // TOCTOU gap. Any PropertyChanged event that fires concurrently with the initial read
            // is captured by an attached handler and competes for the first-emission slot via CAS.
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
