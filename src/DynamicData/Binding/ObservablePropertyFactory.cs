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
    private readonly Func<TObject, bool, IObservable<PropertyValue<TObject, TProperty>>> _factory;

    public ObservablePropertyFactory(Func<TObject, TProperty> valueAccessor, ObservablePropertyPart[] chain)
    {
        // chain is leaf-first (output of SplitIntoSteps). Reverse once to root-to-leaf order.
        var rootToLeaf = chain.AsEnumerable().Reverse().ToArray();
        _factory = (source, notifyInitial) => Observable.Create<PropertyValue<TObject, TProperty>>(
            observer => new DeepChainSubscription(observer, source, rootToLeaf, valueAccessor, notifyInitial));
    }

    public ObservablePropertyFactory(Expression<Func<TObject, TProperty>> expression)
    {
        // Shallow form: single property, no chain. Used when depth == 1. Skips SharedDeliveryQueue
        // and Observable.FromEventPattern in favour of a direct PropertyChanged += handler for
        // the high-frequency single-property hot path.
        var memberName = expression.GetProperty().Name;
        var accessor = expression.Compile();
        _factory = (source, notifyInitial) => Observable.Create<PropertyValue<TObject, TProperty>>(
            observer => new SinglePropertySubscription(observer, source, memberName, accessor, notifyInitial));
    }

    public IObservable<PropertyValue<TObject, TProperty>> Create(TObject source, bool notifyInitial) => _factory(source, notifyInitial);

    // Emits PropertyValues to the downstream observer with a one-shot equality guard at the
    // subscribe-race boundary. notifyInitial controls only whether the caller will synthesize an
    // initial emission: when true, a racing PropertyChanged firing in the subscribe window may
    // produce the same value as the initial read; the one-shot dedup catches that duplicate.
    // When false, every PropertyChanged is a legitimate emission and the dedup is never armed.
    private sealed class Emitter : IObserver<PropertyValue<TObject, TProperty>>
    {
        private readonly IObserver<PropertyValue<TObject, TProperty>> _downstream;
        private int _initialClaimed;
        private int _dedupArmed;
        private PropertyValue<TObject, TProperty>? _seedValue;

        public Emitter(IObserver<PropertyValue<TObject, TProperty>> downstream, bool notifyInitial)
        {
            _downstream = downstream;
            _dedupArmed = notifyInitial ? 0 : 1;
        }

        public void OnNext(PropertyValue<TObject, TProperty> value)
        {
            if (Interlocked.CompareExchange(ref _initialClaimed, 1, 0) == 0)
            {
                Volatile.Write(ref _seedValue, value);
                _downstream.OnNext(value);
                return;
            }

            if (Interlocked.CompareExchange(ref _dedupArmed, 1, 0) == 0)
            {
                var seed = Volatile.Read(ref _seedValue);
                if (seed is not null && PropertyValuesEqual(seed, value))
                {
                    return;
                }
            }

            _downstream.OnNext(value);
        }

        public void OnError(Exception error) => _downstream.OnError(error);

        public void OnCompleted() => _downstream.OnCompleted();

        // One-shot dedup comparison: equal when both have a value AND values are equal, OR both
        // are unobtainable. Used exactly once per subscription, at the boundary between the
        // initial value and the first handler-fired emission.
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

    // Single-property subscription. Attaches a direct PropertyChanged handler and emits via the
    // shared Emitter. Used for x => x.Prop (depth == 1) where SharedDeliveryQueue and
    // Observable.FromEventPattern would be needless overhead on the hot path.
    private sealed class SinglePropertySubscription : IDisposable
    {
        private readonly TObject _source;
        private readonly string _memberName;
        private readonly Func<TObject, TProperty> _accessor;
        private readonly DeliveryQueue<PropertyValue<TObject, TProperty>> _queue;
        private readonly Emitter _emitter;

        public SinglePropertySubscription(
            IObserver<PropertyValue<TObject, TProperty>> observer,
            TObject source,
            string memberName,
            Func<TObject, TProperty> accessor,
            bool notifyInitial)
        {
            _source = source;
            _memberName = memberName;
            _accessor = accessor;
            _queue = new DeliveryQueue<PropertyValue<TObject, TProperty>>(observer);
            _emitter = new Emitter(_queue, notifyInitial);

            // Attach PropertyChanged handler FIRST so events during the initial read are not missed.
            _source.PropertyChanged += OnPropertyChanged;

            if (notifyInitial)
            {
                EmitCurrent();
            }
        }

        public void Dispose()
        {
            _source.PropertyChanged -= OnPropertyChanged;
            _queue.Dispose();
        }

        private void OnPropertyChanged(object? sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == _memberName)
            {
                EmitCurrent();
            }
        }

        // Reads source via the compiled accessor with try/catch; routes failures to _emitter.OnError,
        // otherwise forwards the PropertyValue through _emitter.OnNext. The original Rx pipeline
        // (Select(...)) gave us this for free; we have to do it explicitly now.
        private void EmitCurrent()
        {
            PropertyValue<TObject, TProperty> value;
            try
            {
                value = new PropertyValue<TObject, TProperty>(_source, _accessor(_source));
            }
            catch (Exception ex)
            {
                _emitter.OnError(ex);
                return;
            }

            _emitter.OnNext(value);
        }
    }

    // Deep-chain subscription. Encapsulates the SharedDeliveryQueue + sub-queues + per-level
    // SerialDisposable slots so fields are assigned in well-defined order and the signal sub-queue
    // never needs a forward null bootstrap.
    //
    // SharedDeliveryQueue funnels both chain-level signals and user emissions through a
    // single-drainer pattern. Two sub-queues:
    //   - userSub: receives PropertyValue emissions; drains to the user observer.
    //   - signalSub: receives level-change signals (int); drains by running ProcessSignal
    //     synchronously, which performs the re-walk (ResubscribeFrom) and enqueues the
    //     resulting value onto userSub.
    // Drain order is LIFO (highest sub-queue index first), so signal processing runs before
    // user delivery within each drain cycle, batching multiple signals before delivering the
    // resulting values.
    //
    // Three properties this gives us:
    //   1) Rx contract: user observer is never called concurrently (single drainer).
    //   2) Deadlock immunity: a blocking user observer parks ONLY the drainer thread;
    //      concurrent producers enqueue and return immediately.
    //   3) Concurrent-mutation safety: ResubscribeFrom runs on the drainer thread, so
    //      two threads racing to reassign the same intermediate property cannot leave a
    //      SerialDisposable slot subscribed to the loser; the drainer's loop processes
    //      every queued signal in order against the current chain state.
    private sealed class DeepChainSubscription : IDisposable
    {
        // Sentinel signal value enqueued during subscribe to perform the initial chain setup
        // from inside the SharedDeliveryQueue drainer.
        private const int InitialSetupSignal = -1;

        private readonly TObject _source;
        private readonly ObservablePropertyPart[] _rootToLeaf;
        private readonly Func<TObject, TProperty> _valueAccessor;
        private readonly bool _notifyInitial;
        private readonly SharedDeliveryQueue _sharedQueue;
        private readonly DeliverySubQueue<PropertyValue<TObject, TProperty>> _userSub;
        private readonly DeliverySubQueue<int> _signalSub;
        private readonly SerialDisposable[] _levelSlots;
        private readonly Emitter _emitter;

        public DeepChainSubscription(
            IObserver<PropertyValue<TObject, TProperty>> observer,
            TObject source,
            ObservablePropertyPart[] rootToLeaf,
            Func<TObject, TProperty> valueAccessor,
            bool notifyInitial)
        {
            _source = source;
            _rootToLeaf = rootToLeaf;
            _valueAccessor = valueAccessor;
            _notifyInitial = notifyInitial;

            _sharedQueue = new SharedDeliveryQueue();
            _userSub = _sharedQueue.CreateQueue(observer);
            _emitter = new Emitter(_userSub, notifyInitial);

            // ProcessSignal references _signalSub indirectly via ResubscribeFrom's notifier
            // subscriptions. Observer.Create stores the method group without invoking it, and
            // _signalSub is assigned by the surrounding expression, so by the time anything calls
            // ProcessSignal the field is set.
            _signalSub = _sharedQueue.CreateQueue(Observer.Create<int>(ProcessSignal));

            _levelSlots = new SerialDisposable[rootToLeaf.Length];
            for (var i = 0; i < rootToLeaf.Length; i++)
            {
                _levelSlots[i] = new SerialDisposable();
            }

            // Kick off initial chain setup via the drainer. The subscribe thread becomes the
            // drainer (no one else is draining yet on a fresh subscription) and runs
            // ProcessSignal(InitialSetupSignal) synchronously, which attaches the chain and
            // emits the initial value.
            _signalSub.OnNext(InitialSetupSignal);
        }

        public void Dispose()
        {
            foreach (var slot in _levelSlots)
            {
                slot.Dispose();
            }

            _signalSub.Dispose();
            _userSub.Dispose();
            _sharedQueue.Dispose();
        }

        private void ProcessSignal(int level)
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
                    if (_notifyInitial)
                    {
                        _emitter.OnNext(ReadCurrent());
                    }

                    return;
                }

                ResubscribeFrom(level + 1);
                _emitter.OnNext(ReadCurrent());
            }
            catch (Exception ex)
            {
                _emitter.OnError(ex);
            }
        }

        private void ResubscribeFrom(int startLevel)
        {
            var depth = _rootToLeaf.Length;
            if (startLevel >= depth)
            {
                return;
            }

            object? value = _source;
            for (var i = 0; i < startLevel; i++)
            {
                value = _rootToLeaf[i].Invoker(value);
                if (value is null)
                {
                    for (var j = startLevel; j < depth; j++)
                    {
                        _levelSlots[j].Disposable = Disposable.Empty;
                    }

                    return;
                }
            }

            for (var i = startLevel; i < depth; i++)
            {
                if (value is null)
                {
                    _levelSlots[i].Disposable = Disposable.Empty;
                    continue;
                }

                var levelIndex = i;
                var notifier = _rootToLeaf[i].Factory(value);
                _levelSlots[i].Disposable = notifier.Subscribe(_ => _signalSub.OnNext(levelIndex));

                value = _rootToLeaf[i].Invoker(value);
            }
        }

        // Root-to-leaf chain walk. Stops at null and returns an unobtainable PropertyValue.
        private PropertyValue<TObject, TProperty> ReadCurrent()
        {
            object? value = _source;
            foreach (var metadata in _rootToLeaf)
            {
                value = metadata.Invoker(value);
                if (value is null)
                {
                    return new PropertyValue<TObject, TProperty>(_source);
                }
            }

            return new PropertyValue<TObject, TProperty>(_source, _valueAccessor(_source));
        }
    }
}
