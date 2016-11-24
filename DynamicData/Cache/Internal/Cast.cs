using System;
using System.Linq;
using System.Reactive.Linq;
using DynamicData.Kernel;


namespace DynamicData.Cache.Internal
{
    internal class Cast<TSource, TKey, TDestination>
    {
        private readonly IObservable<IChangeSet<TSource, TKey>> _source;
        private readonly Func<TSource, TDestination> _converter;

        public Cast(IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TDestination> converter)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (converter == null) throw new ArgumentNullException(nameof(converter));
            _source = source;
            _converter = converter;
        }

        public IObservable<IChangeSet<TDestination, TKey>> Run()
        {
            return _source.Select(changes =>
            {
                var transformed = changes.Select(change => new Change<TDestination, TKey>(change.Reason,
                                                                          change.Key,
                                                                          _converter(change.Current),
                                                                          change.Previous.Convert(_converter),
                                                                          change.CurrentIndex,
                                                                          change.PreviousIndex));
                return new ChangeSet<TDestination, TKey>(transformed);
            });
        }
    }

}
