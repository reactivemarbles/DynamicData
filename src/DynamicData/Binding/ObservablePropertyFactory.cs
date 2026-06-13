// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;

using DynamicData.Internal;

namespace DynamicData.Binding;

internal sealed class ObservablePropertyFactory<TObject, TProperty>
    where TObject : INotifyPropertyChanged
{
    /// <summary>Sentinel signal value enqueued during subscribe to perform the initial chain setup
    /// from inside the SharedDeliveryQueue drainer.</summary>
    private const int InitialSetupSignal = -1;

    private readonly Func<TObject, bool, IObservable<PropertyValue<TObject, TProperty>>> _factory;

    public ObservablePropertyFactory(Func<TObject, TProperty> valueAccessor, ObservablePropertyPart[] chain)
    {
        // chain is leaf-first (output of SplitIntoSteps). Reverse once to root-to-leaf order.
        var rootToLeaf = System.Linq.Enumerable.Reverse((IEnumerable<ObservablePropertyPart>)chain).ToArray();
        var depth = rootToLeaf.Length;

        _factory = (source, notifyInitial) => Observable.Create<PropertyValue<TObject, TProperty>>(observer =>
        {
            // SharedDeliveryQueue funnels both chain-level signals and user emissions through a
            // single-drainer pattern. Two sub-queues:
            //   - userSub: receives PropertyValue emissions; drains to the user observer.
            //   - signalSub: receives level-change signals (int); drains by running the level
            //     processor synchronously, which performs the re-walk (ResubscribeFrom) and
            //     enqueues the resulting value onto userSub.
            // Drain order is LIFO (highest sub-queue index first), so signal processing runs
            // before user delivery within each drain cycle, batching multiple signals before
            // delivering the resulting values.
            //
            // Three properties this gives us:
            //   1) Rx contract: user observer is never called concurrently (single drainer).
            //   2) Deadlock immunity: a blocking user observer parks ONLY the drainer thread;
            //      concurrent producers enqueue and return immediately.
            //   3) Concurrent-mutation safety: ResubscribeFrom runs on the drainer thread, so
            //      two threads racing to reassign the same intermediate property cannot leave a
            //      SerialDisposable slot subscribed to the loser; the drainer's loop processes
            //      every queued signal in order against the current chain state.
            var sharedQueue = new SharedDeliveryQueue();
            var userSub = sharedQueue.CreateQueue(observer);
            DeliverySubQueue<int>? signalSub = null;

            var levelSlots = new SerialDisposable[depth];
            for (var i = 0; i < depth; i++)
            {
                levelSlots[i] = new SerialDisposable();
            }

            var initialClaimed = 0;
            // When notifyInitial is false, the dedup window does not apply: the user wants every
            // PropertyChanged event, including same-valued ones. Arm dedup as already-fired so the
            // dedup branch is never taken.
            var dedupArmed = notifyInitial ? 0 : 1;
            PropertyValue<TObject, TProperty>? seedValue = null;

            void Emit(PropertyValue<TObject, TProperty> value)
            {
                if (Interlocked.CompareExchange(ref initialClaimed, 1, 0) == 0)
                {
                    Volatile.Write(ref seedValue, value);
                    userSub.OnNext(value);
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

                userSub.OnNext(value);
            }

            void OnLevelChanged(int level) => signalSub!.OnNext(level);

            void ResubscribeFrom(int startLevel)
            {
                if (startLevel >= depth)
                {
                    return;
                }

                object? value = source;
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

            void ProcessSignal(int level)
            {
                // Drainer thread: any exception thrown by a user-provided invoker, notifier
                // factory, or value accessor must be routed to OnError rather than escaping the
                // drainer. The original Rx pipeline (Select(...)) gave us this for free; we have
                // to do it explicitly now.
                try
                {
                    if (level == InitialSetupSignal)
                    {
                        // Initial chain setup runs on the drainer so it serializes against any
                        // concurrent notifier fires that may arrive while we attach the notifiers.
                        ResubscribeFrom(0);
                        if (notifyInitial)
                        {
                            Emit(GetPropertyValueRootToLeaf(source, rootToLeaf, valueAccessor));
                        }

                        return;
                    }

                    ResubscribeFrom(level + 1);
                    Emit(GetPropertyValueRootToLeaf(source, rootToLeaf, valueAccessor));
                }
                catch (Exception ex)
                {
                    userSub.OnError(ex);
                }
            }

            signalSub = sharedQueue.CreateQueue(Observer.Create<int>(ProcessSignal));

            // Trigger the initial setup signal. The subscribe thread becomes the drainer (no one
            // else is draining yet on a fresh subscription) and runs ProcessSignal(InitialSetupSignal)
            // synchronously, which attaches the chain and emits the initial value.
            signalSub.OnNext(InitialSetupSignal);

            return new CompositeDisposable(new CompositeDisposable(levelSlots), signalSub, userSub, sharedQueue);
        });
    }

    public ObservablePropertyFactory(Expression<Func<TObject, TProperty>> expression)
    {
        // Shallow form: single property, no chain. Used when depth == 1.
        var member = expression.GetProperty();
        var accessor = expression.Compile();
        var memberName = member.Name;

        _factory = (t, notifyInitial) => Observable.Create<PropertyValue<TObject, TProperty>>(observer =>
        {
            // DeliveryQueue serializes emissions to the user observer so concurrent PropertyChanged
            // invocations cannot violate Rx contract. The shallow form has only one notifier, so
            // SharedDeliveryQueue is not needed; a single DeliveryQueue suffices.
            var queue = new DeliveryQueue<PropertyValue<TObject, TProperty>>(observer);

            var initialClaimed = 0;
            // When notifyInitial is false, the dedup window does not apply: the user wants every
            // PropertyChanged event, including same-valued ones. Arm dedup as already-fired so the
            // dedup branch is never taken.
            var dedupArmed = notifyInitial ? 0 : 1;
            PropertyValue<TObject, TProperty>? seedValue = null;

            void Emit(PropertyValue<TObject, TProperty> value)
            {
                if (Interlocked.CompareExchange(ref initialClaimed, 1, 0) == 0)
                {
                    Volatile.Write(ref seedValue, value);
                    queue.OnNext(value);
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

                queue.OnNext(value);
            }

            void OnPropertyChanged(object? sender, PropertyChangedEventArgs args)
            {
                if (args.PropertyName != memberName)
                {
                    return;
                }

                // accessor is user code via the compiled expression; an exception must be routed to
                // OnError rather than escaping into INotifyPropertyChanged's event invocation.
                PropertyValue<TObject, TProperty> value;
                try
                {
                    value = new PropertyValue<TObject, TProperty>(t, accessor(t));
                }
                catch (Exception ex)
                {
                    queue.OnError(ex);
                    return;
                }

                Emit(value);
            }

            // Attach PropertyChanged handler FIRST so events during the initial read are not missed.
            t.PropertyChanged += OnPropertyChanged;

            if (notifyInitial)
            {
                PropertyValue<TObject, TProperty> initialValue;
                try
                {
                    initialValue = new PropertyValue<TObject, TProperty>(t, accessor(t));
                }
                catch (Exception ex)
                {
                    queue.OnError(ex);
                    return new CompositeDisposable(
                        Disposable.Create(() => t.PropertyChanged -= OnPropertyChanged),
                        queue);
                }

                Emit(initialValue);
            }

            return new CompositeDisposable(
                Disposable.Create(() => t.PropertyChanged -= OnPropertyChanged),
                queue);
        });
    }

    public IObservable<PropertyValue<TObject, TProperty>> Create(TObject source, bool notifyInitial) => _factory(source, notifyInitial);

    // Root-to-leaf chain walk. Stops at null and returns an unobtainable PropertyValue.
    private static PropertyValue<TObject, TProperty> GetPropertyValueRootToLeaf(
        TObject source, ObservablePropertyPart[] rootToLeaf, Func<TObject, TProperty> valueAccessor)
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

    // One-shot dedup comparison: equal when both have a value AND values are equal, OR both are
    // unobtainable. Used exactly once per subscription, at the boundary between the initial value
    // and the first handler-fired emission.
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
