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
        // so chain navigation is index-monotonic.
        var rootToLeaf = System.Linq.Enumerable.Reverse((IEnumerable<ObservablePropertyPart>)chain).ToArray();

        _factory = (source, notifyInitial) => Observable.Create<PropertyValue<TObject, TProperty>>(observer =>
        {
            // Chain orchestration via recursive Switch:
            //   ObserveLevel(level i) emits the current value of level i on subscribe AND on every
            //   PropertyChanged at that level. Select transforms each emission into an observable
            //   of the deeper chain; Switch subscribes to the new inner and disposes the old. When
            //   level X fires, the (X+1..depth) subscriptions are atomically re-attached against
            //   the new sub-tree.
            //
            // Race-safety:
            //   - ObserveLevel attaches the PropertyChanged handler BEFORE reading the initial
            //     value, so events fired during the read are not missed.
            //   - At the outermost layer below, CAS on initialClaimed ensures only one of
            //     {initial-read emission, first handler-fired emission} reaches the observer.
            //   - A one-shot Interlocked-CAS-guarded equality check on the first post-initial
            //     handler emission catches the rare setter-update-then-notify duplicate.
            //
            // Concurrent mutation of the SAME observed property from multiple threads is the
            // caller's responsibility (standard Rx contract). Switch's lock-acquisition order is
            // not guaranteed to match setter-completion order, so well-behaved INPC usage must
            // serialize mutations on observed properties.

            var initialClaimed = 0;
            var dedupArmed = 0;
            PropertyValue<TObject, TProperty>? seedValue = null;

            void Emit(PropertyValue<TObject, TProperty> value)
            {
                if (Interlocked.CompareExchange(ref initialClaimed, 1, 0) == 0)
                {
                    Volatile.Write(ref seedValue, value);
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

            var stream = ObserveChain(source, source, rootToLeaf, 0, valueAccessor);
            if (!notifyInitial)
            {
                stream = stream.Skip(1);
            }

            return stream.Subscribe(Emit, observer.OnError, observer.OnCompleted);
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

    // Recursive Rx composition. ObserveLevel emits the current value of the level on subscribe
    // AND on every PropertyChanged. Select transforms each emission into an observable of the
    // remaining (deeper) chain; Switch subscribes to the new inner and disposes the old.
    private static IObservable<PropertyValue<TObject, TProperty>> ObserveChain(
        TObject root,
        object? current,
        ObservablePropertyPart[] rootToLeaf,
        int level,
        Func<TObject, TProperty> leafAccessor)
    {
        if (current is null)
        {
            return Observable.Return(new PropertyValue<TObject, TProperty>(root));
        }

        if (level >= rootToLeaf.Length)
        {
            // Read the leaf value via the original compiled accessor, which walks the chain from
            // root. This always reads the CURRENT chain state regardless of where Switch is in
            // its re-walk; downstream observer always sees the live value.
            return Observable.Return(new PropertyValue<TObject, TProperty>(root, leafAccessor(root)));
        }

        var part = rootToLeaf[level];

        return ObserveLevel(current, part)
            .Select(child => ObserveChain(root, child, rootToLeaf, level + 1, leafAccessor))
            .Switch();
    }

    // Race-safe single-level property observation: attaches the PropertyChanged handler BEFORE
    // reading the initial value so events fired during the read are not missed.
    private static IObservable<object?> ObserveLevel(object source, ObservablePropertyPart part) =>
        Observable.Create<object?>(observer =>
        {
            var sub = part.Factory(source).Subscribe(_ => observer.OnNext(part.Invoker(source)));
            observer.OnNext(part.Invoker(source));
            return sub;
        });

    // One-shot dedup comparison: equal when both have a value AND values are equal,
    // OR both are unobtainable. Used at the boundary between the initial emission and the first
    // post-initial handler emission to suppress the rare setter-update-then-notify duplicate.
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
