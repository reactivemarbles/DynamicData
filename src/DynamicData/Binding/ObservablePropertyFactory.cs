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

    // Single-property subscription. Attaches a direct PropertyChanged handler and forwards every
    // event through a DeliveryQueue. Used for x => x.Prop (depth == 1) where SharedDeliveryQueue
    // and Observable.FromEventPattern would be needless overhead on the hot path.
    //
    // notifyInitial only controls whether the constructor synthesises an initial emission. There
    // is no equality dedup at the subscribe seam: a same-valued PropertyChanged firing in the
    // subscribe window is a legitimate event and must be delivered. The "never drop events"
    // contract takes precedence over avoiding a benign duplicate.
    private sealed class SinglePropertySubscription : IDisposable
    {
        private readonly TObject _source;
        private readonly string _memberName;
        private readonly Func<TObject, TProperty> _accessor;
        private readonly DeliveryQueue<PropertyValue<TObject, TProperty>> _queue;

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

        // Wraps both the accessor read AND the queue OnNext (which may invoke the downstream
        // observer synchronously via the DeliveryQueue drain) so that exceptions from either are
        // routed through OnError rather than escaping raw into the PropertyChanged setter that
        // fired the event. These emissions originate from an INotifyPropertyChanged callback, so a
        // raw escaping exception would bypass the subscriber's error handler and surface out of the
        // property setter. Routing through OnError gives a subscriber that supplied an onError
        // handler the chance to observe and absorb the failure. We deliberately do NOT swallow a
        // throw from OnError itself: if the downstream error is unhandled (or the error handler
        // rethrows) it propagates to the setter, matching Subject<T>.OnNext semantics.
        private void EmitCurrent()
        {
            try
            {
                _queue.OnNext(new PropertyValue<TObject, TProperty>(_source, _accessor(_source)));
            }
            catch (Exception ex)
            {
                _queue.OnError(ex);
            }
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
    //
    // notifyInitial only controls whether ProcessSignal emits the current chain value during
    // the InitialSetupSignal pass. There is no equality dedup at the subscribe seam: every
    // chain event is delivered.
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

        // Pre-allocated per-level notifier callbacks. Indexed by level. ResubscribeFrom reuses
        // these instead of allocating a fresh closure per re-walk.
        private readonly Action<Unit>[] _levelCallbacks;

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

            // ProcessSignal references _signalSub indirectly via ResubscribeFrom's notifier
            // subscriptions. Observer.Create stores the method group without invoking it, and
            // _signalSub is assigned by the surrounding expression, so by the time anything calls
            // ProcessSignal the field is set.
            _signalSub = _sharedQueue.CreateQueue(Observer.Create<int>(ProcessSignal));

            var depth = rootToLeaf.Length;
            _levelSlots = new SerialDisposable[depth];
            _levelCallbacks = new Action<Unit>[depth];
            for (var i = 0; i < depth; i++)
            {
                _levelSlots[i] = new SerialDisposable();
                var level = i;
                _levelCallbacks[i] = _ => _signalSub.OnNext(level);
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
            // Drainer thread. Wraps the entire signal processing so that exceptions from any
            // user-provided invoker, notifier factory, value accessor, or downstream observer are
            // routed through OnError rather than escaping raw into the SharedDeliveryQueue drainer
            // (and ultimately the INotifyPropertyChanged setter that raised the event). Routing
            // through OnError gives a subscriber that supplied an onError handler the chance to
            // observe and absorb the failure. We deliberately do NOT swallow a throw from OnError
            // itself: an unhandled downstream error (or a rethrowing error handler) propagates,
            // matching Subject<T>.OnNext semantics.
            //
            // The two cases (initial setup vs level-fire) collapse to:
            //   startLevel = (initial) ? 0 : level + 1
            //   emit       = (level-fire) || _notifyInitial
            try
            {
                var isInitial = level == InitialSetupSignal;
                ResubscribeFrom(isInitial ? 0 : level + 1);
                if (!isInitial || _notifyInitial)
                {
                    _userSub.OnNext(ReadCurrent());
                }
            }
            catch (Exception ex)
            {
                _userSub.OnError(ex);
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

                var notifier = _rootToLeaf[i].Factory(value);
                _levelSlots[i].Disposable = notifier.Subscribe(_levelCallbacks[i]);

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
