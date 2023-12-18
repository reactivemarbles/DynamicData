// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Linq;

namespace DynamicData.Binding;

internal sealed class ObservablePropertyFactory<TObject, TProperty>
    where TObject : INotifyPropertyChanged
{
    private readonly Func<TObject, bool, IObservable<PropertyValue<TObject, TProperty>>> _factory;

    public ObservablePropertyFactory(Func<TObject, TProperty> valueAccessor, ObservablePropertyPart[] chain) =>
        _factory = (t, notifyInitial) =>
        {
            // 1) notify when values have changed
            // 2) resubscribe when changed because it may be a child object which has changed
            var valueHasChanged = GetNotifiers(t, chain).Merge().Take(1).Repeat();
            if (notifyInitial)
            {
                valueHasChanged = Observable.Defer(() => Observable.Return(Unit.Default)).Concat(valueHasChanged);
            }

            return valueHasChanged.Select(_ => GetPropertyValue(t, chain, valueAccessor));
        };

    public ObservablePropertyFactory(Expression<Func<TObject, TProperty>> expression)
    {
        // this overload is used for shallow observations i.e. depth = 1, so no need for re-subscriptions
        var member = expression.GetProperty();
        var accessor = expression.Compile();

        _factory = (t, notifyInitial) =>
        {
            PropertyValue<TObject, TProperty> Factory() => new(t, accessor(t));

            var propertyChanged = Observable.FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(handler => t.PropertyChanged += handler, handler => t.PropertyChanged -= handler).Where(args => args.EventArgs.PropertyName == member.Name).Select(_ => Factory());

            if (!notifyInitial)
            {
                return propertyChanged;
            }

            var initial = Observable.Defer(() => Observable.Return(Factory()));
            return initial.Concat(propertyChanged);
        };
    }

    public IObservable<PropertyValue<TObject, TProperty>> Create(TObject source, bool notifyInitial) => _factory(source, notifyInitial);

    // create notifier for all parts of the property path
    private static IEnumerable<IObservable<Unit>> GetNotifiers(TObject source, IEnumerable<ObservablePropertyPart> chain)
    {
        object value = source;
        foreach (var metadata in chain.Reverse())
        {
            var obs = metadata.Factory(value).Publish().RefCount();
            value = metadata.Accessor(value);
            yield return obs;

            if (value is null)
            {
                yield break;
            }
        }
    }

    // walk the tree and break at a null, or return the value [should reduce this to a null an expression]
    private static PropertyValue<TObject, TProperty> GetPropertyValue(TObject source, IEnumerable<ObservablePropertyPart> chain, Func<TObject, TProperty> valueAccessor)
    {
        object value = source;
        foreach (var metadata in chain.Reverse())
        {
            value = metadata.Accessor(value);
            if (value is null)
            {
                return new PropertyValue<TObject, TProperty>(source);
            }
        }

        return new PropertyValue<TObject, TProperty>(source, valueAccessor(source));
    }
}
