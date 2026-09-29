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
        _factory = (source, notifyInitial) => Observable.Create<PropertyValue<TObject, TProperty>>(observer =>
        {
            var subscription = new DeepChainSubscription(observer, source, rootToLeaf, valueAccessor, notifyInitial);

            // The scope owns initialization; only successful activation hands a dependent lease to Rx.
            using var lifetime = new RefCountDisposable(subscription);
            subscription.Start();

            return lifetime.GetDisposable();
        });
    }

    public ObservablePropertyFactory(Expression<Func<TObject, TProperty>> expression)
    {
        // Shallow form: single property, no chain. Used when depth == 1. Skips SharedDeliveryQueue
        // and Observable.FromEventPattern in favour of a direct PropertyChanged += handler for
        // the high-frequency single-property hot path.
        var memberName = expression.GetProperty().Name;
        var accessor = expression.Compile();
        _factory = (source, notifyInitial) => Observable.Create<PropertyValue<TObject, TProperty>>(observer =>
        {
            var subscription = new SinglePropertySubscription(observer, source, memberName, accessor);

            // The scope releases the handler if activation throws before Rx can receive its lease.
            using var lifetime = new RefCountDisposable(subscription);
            subscription.Start(notifyInitial);

            return lifetime.GetDisposable();
        });
    }

    public IObservable<PropertyValue<TObject, TProperty>> Create(TObject source, bool notifyInitial) => _factory(source, notifyInitial);

    // Single-property subscription. Attaches a direct PropertyChanged handler and synchronizes events. Used for
    // x => x.Prop (depth == 1) where Observable.FromEventPattern would be needless overhead on the hot path.
    //
    // notifyInitial only controls whether Start synthesises an initial emission. There
    // is no equality dedup at the subscribe seam: a same-valued PropertyChanged firing in the
    // subscribe window is a legitimate event and must be delivered. The "never drop events"
    // contract takes precedence over avoiding a benign duplicate.
    private sealed class SinglePropertySubscription : IDisposable
    {
        private readonly TObject _source;
        private readonly string _memberName;
        private readonly Func<TObject, TProperty> _accessor;
        private readonly IObserver<PropertyValue<TObject, TProperty>> _observer;
        #if NET9_0_OR_GREATER
        private readonly Lock _notificationGate;
        #else
        private readonly object _notificationGate;
        #endif

        public SinglePropertySubscription(
            IObserver<PropertyValue<TObject, TProperty>> observer,
            TObject source,
            string memberName,
            Func<TObject, TProperty> accessor)
        {
            _source = source;
            _memberName = memberName;
            _accessor = accessor;
            _observer = observer;
            _notificationGate = new();
        }

        public void Dispose()
        {
            _source.PropertyChanged -= OnPropertyChanged;
        }

        public void Start(bool notifyInitial)
        {
            // Attach before reading so changes during initialization are captured.
            _source.PropertyChanged += OnPropertyChanged;

            if (notifyInitial)
            {
                EmitCurrent();
            }
        }

        private void OnPropertyChanged(object? sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == _memberName)
            {
                EmitCurrent();
            }
        }

        // Reads the current property value and forwards it through the queue. The accessor is
        // user code and may throw; that exception routes to OnError. The downstream OnNext call
        // is NOT wrapped: per the Rx contract, if the user observer throws, the exception
        // propagates back to whoever invoked the PropertyChanged setter, matching what a plain
        // Subject<T>.OnNext would do.
        private void EmitCurrent()
        {
            lock (_notificationGate)
            {
                PropertyValue<TObject, TProperty> value;
                try
                {
                    value = new PropertyValue<TObject, TProperty>(_source, _accessor(_source));
                }
                catch (Exception ex)
                {
                    _observer.OnError(ex);
                    return;
                }

                _observer.OnNext(value);
            }
        }
    }

    // Deep-chain subscription.
    //
    // notifyInitial only controls whether ProcessSignal emits the current chain value during
    // the InitialSetupSignal pass. There is no equality dedup at the subscribe seam: every
    // chain event is delivered.
    private sealed class DeepChainSubscription : IDisposable
    {
        // Sentinel signal value enqueued during subscribe to perform the initial chain setup.
        private const int InitialSetupSignal = -1;

        private readonly TObject _source;
        private readonly ObservablePropertyPart[] _rootToLeaf;
        private readonly Func<TObject, TProperty> _valueAccessor;
        private readonly bool _notifyInitial;
        private readonly IObserver<PropertyValue<TObject, TProperty>> _observer;
        #if NET9_0_OR_GREATER
        private readonly Lock _notificationGate;
        #else
        private readonly object _notificationGate;
        #endif
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
            _observer = observer;
            _notificationGate = new();

            var depth = rootToLeaf.Length;
            _levelSlots = new SerialDisposable[depth];
            _levelCallbacks = new Action<Unit>[depth];
            for (var i = 0; i < depth; i++)
            {
                _levelSlots[i] = new SerialDisposable();
                var level = i;
                _levelCallbacks[i] = _ => ProcessChange(level);
            }
        }

        public void Dispose()
        {
            foreach (var slot in _levelSlots)
            {
                slot.Dispose();
            }
        }

        // Initial setup uses the same delivery ordering and reentrancy as subsequent changes.
        public void Start() => ProcessChange(InitialSetupSignal);

        private void ProcessChange(int level)
        {
            lock (_notificationGate)
            {
                // The chain walk (Invoker / notifier Factory / ReadCurrent's accessor) is user code and may throw;
                // those exceptions route to OnError. The downstream OnNext call is NOT wrapped: per the Rx contract,
                // if the user observer throws, the exception propagates back through the drainer, matching what a plain
                // Subject<T> would do.
                //
                // The two cases (initial setup vs level-fire) collapse to:
                //   startLevel = (initial) ? 0 : level + 1
                //   emit       = (level-fire) || _notifyInitial
                var isInitial = level == InitialSetupSignal;
                var shouldEmit = !isInitial || _notifyInitial;
                PropertyValue<TObject, TProperty> value;
                try
                {
                    ResubscribeFrom(isInitial ? 0 : level + 1);
                    if (!shouldEmit)
                    {
                        return;
                    }

                    value = ReadCurrent();
                }
                catch (Exception ex)
                {
                    _observer.OnError(ex);
                    return;
                }

                _observer.OnNext(value);
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
