using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.Binding
{
    [DebuggerDisplay("PropertyChainPart<{_expresson}>")]
    internal sealed class PropertyChainPart
    {
        private readonly MemberExpression _expresson;
        public Func<object, IObservable<Unit>> Factory { get; }
        public Func<object, object> Accessor { get; }

        public PropertyChainPart(MemberExpression expresson)
        {
            _expresson = expresson;
            Factory = expresson.CreatePropertyChangedFactory();
            Accessor = expresson.CreateValueAccessor();
        }
    }

    [DebuggerDisplay("PropertyChain<{typeof(TObject).Name},{typeof(TValue).Name}>")]
    internal sealed class PropertyChain<TObject, TValue>
        where TObject:INotifyPropertyChanged
    {
        private readonly TObject _source;
        private readonly IEnumerable<PropertyChainPart> _propertyMetadata;
        private readonly Func<TObject, TValue> _valueAccessor;
        private readonly bool _notifyInitial;

        public PropertyChain(TObject source, Expression<Func<TObject, TValue>> valueAccessor, IEnumerable<PropertyChainPart> propertyMetadata, bool notifyInitial)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (valueAccessor == null) throw new ArgumentNullException(nameof(valueAccessor));
            if (propertyMetadata == null) throw new ArgumentNullException(nameof(propertyMetadata));

            _source = source;
            _propertyMetadata = propertyMetadata;
            _valueAccessor = valueAccessor?.Compile() ?? throw new ArgumentNullException(nameof(valueAccessor));
            _notifyInitial = notifyInitial;
        }

        public IObservable<PropertyValue<TObject, TValue>> CreateObservable()
        {
            //PropertyValue<TObject, TValue> PropertyValueFactory() 
            //{
            //    return new PropertyValue<TObject, TValue>(_source, _valueAccessor(_source));
            //}

            //TODO: 1. Check for nulls and report nothing if anything in the chain is null (except last value)
            //TODO: 2. Resubscribe to all changes whenever anything but the last value is null
            //TODO: 3. Aggregate using linq to clean this shit up

            PropertyValue<TObject, TValue> ValueOrNull()
            {
                object value = _source;
                foreach (var metadata in _propertyMetadata.Reverse())
                {
                    value = metadata.Accessor(value);
                    if (value == null) return null;
                }
                return new PropertyValue<TObject, TValue>(_source, _valueAccessor(_source));
            }


            var propertyChanged =  Observable.Create<PropertyValue<TObject, TValue>>(observer =>
            {
                IEnumerable<IObservable<Unit>> GetValues()
                {
                    object value = _source;
                    foreach (var metadata in _propertyMetadata.Reverse())
                    {
                        Debug.WriteLine(value);
                        var obs = metadata.Factory(value).Publish().RefCount();
                        value = metadata.Accessor(value);
                        yield return obs;

                        if (value == null) yield break;
                    }
                }

                var values = GetValues().Count();
                Debug.WriteLine(values + " count");

                var shared = GetValues()
                
                        .Merge()
                                .Do(x =>
                            {
                                Debug.WriteLine("-----------------");
                            });

                IObservable<Unit> valueHasChanged = shared
                        .Take(1)
                        .Repeat();

                if (_notifyInitial)
                {
                    valueHasChanged = Observable.Defer(() => Observable.Return(Unit.Default))
                        .Concat(valueHasChanged);
                }
                      
                //if (_notifyInitial)
                //{
                //    var initial = Observable
                //        .Defer(() => Observable.Return(ValueOrNull()));

                //    factory = initial.Concat(factory)
                //        .TakeUntil(factory)
                //        .Repeat();
                //}
                //else
                //{
                //    factory = factory
                //        .TakeUntil(factory)
                //        .Repeat();
                //}

                var publisher = valueHasChanged
                    .Select(_ => ValueOrNull())
                    .SubscribeSafe(observer);

                return new CompositeDisposable(publisher);
            });


            return propertyChanged;
        }

        private class NotificationWithValue
        {
            public IObservable<Unit> Notification { get; }
            public object Value { get; }

            public NotificationWithValue(IObservable<Unit> notification, object value)
            {
                Notification = notification;
                Value = value;
            }
        }

        private IEnumerable<IObservable<Unit>> CreateObservables()
        {
            //use linq .aggregate?
            var result = new List<IObservable<Unit>>();
            object parent = _source;
            foreach (var metadata in _propertyMetadata.Reverse())
            {
                var obs = metadata.Factory(parent);
                parent = metadata.Accessor(parent);
                result.Add(obs);
            }
            return result;
        }
    }
}