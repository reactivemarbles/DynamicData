using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Linq;

namespace DynamicData.Binding
{

    internal static class Observable<T>
    {
        public static readonly IObservable<T> Empty = Observable.Empty<T>();
        public static readonly IObservable<T> Never = Observable.Never<T>();
        public static readonly IObservable<T> Default = Observable.Return(default(T));
    }

    internal class ObservablePropertyFactory<TObject, TProperty> 
        where TObject: INotifyPropertyChanged
    {
        private readonly Func<TObject, bool, IObservable<PropertyValue<TObject, TProperty>>> _factory;

        public ObservablePropertyFactory(Func<TObject, TProperty> valueAccessor, ObservablePropertyPart[] chain)
        {
            _factory = (t, notifyInitial) =>
            {
                //1) notify when values have changed 
                //2) resubscribe when changed because it may be a child object which has changed
                var valueHasChanged = GetNotifiers(t,chain).Merge().Take(1).Repeat();
                if (notifyInitial)
                {
                    valueHasChanged = Observable.Defer(() => Observable.Return(Unit.Default))
                        .Concat(valueHasChanged);
                }
                return valueHasChanged.Select(_ => GetPropertyValue(t,chain, valueAccessor));
            };
        }

        public ObservablePropertyFactory(Expression<Func<TObject, TProperty>> expression)
        {
            //this overload is used for shallow observations i.e. depth = 1, so no need for resubscriptons
            var member = expression.GetProperty();
            var accessor = expression.Compile();

            _factory = (t, notifyInitial) =>
            {
                PropertyValue<TObject, TProperty> Factory() => new PropertyValue<TObject, TProperty>(t, accessor(t));

                var propertyChanged = Observable.FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>
                    (
                        handler => t.PropertyChanged += handler,
                        handler => t.PropertyChanged -= handler
                    )
                    .Where(args => args.EventArgs.PropertyName == member.Name)
                    .Select(x => Factory());

                if (!notifyInitial)
                    return propertyChanged;

                var initial = Observable.Defer(() => Observable.Return(Factory()));
                return initial.Concat(propertyChanged);
            };
        }

        public IObservable<PropertyValue<TObject, TProperty>> Create(TObject source, bool notifyInitial)
        {
            return _factory(source, notifyInitial);
        }

        //create notifier for all parts of the property path 
        private static IEnumerable<IObservable<Unit>> GetNotifiers(TObject source, ObservablePropertyPart[] chain)
        {
            object value = source;
            foreach (var metadata in chain.Reverse())
            {
                var obs = metadata.Factory(value).Publish().RefCount();
                value = metadata.Accessor(value);
                yield return obs;

                if (value == null) yield break;
            }
        }

        //walk the tree and break at a null, or return the value [should reduce this to a null an expression]
        private static PropertyValue<TObject, TProperty> GetPropertyValue(TObject source, ObservablePropertyPart[] chain, Func<TObject, TProperty> valueAccessor)
        {
            object value = source;
            foreach (var metadata in chain.Reverse())
            {
                value = metadata.Accessor(value);
                if (value == null) return new PropertyValue<TObject, TProperty>(source);
            }
            return new PropertyValue<TObject, TProperty>(source, valueAccessor(source));
        }

    }
}