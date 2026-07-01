// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

using DynamicData.Reactive.Binding;
#else

using DynamicData.Binding;
#endif
#if REACTIVE_SHIM

namespace DynamicData.Reactive.List.Internal;
#else

namespace DynamicData.List.Internal;
#endif

/// <summary>
/// Provides members for the TransformMany class.
/// </summary>
/// <typeparam name="TSource">The type of the TSource value.</typeparam>
/// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
/// <param name="source">The source value.</param>
/// <param name="manySelector">The manySelector value.</param>
/// <param name="equalityComparer">The equalityComparer value.</param>
/// <param name="childChanges">The childChanges value.</param>
internal sealed class TransformMany<TSource, TDestination>(IObservable<IChangeSet<TSource>> source, Func<TSource, IEnumerable<TDestination>> manySelector, IEqualityComparer<TDestination>? equalityComparer = null, Func<TSource, IObservable<IChangeSet<TDestination>>>? childChanges = null)
    where TSource : notnull
    where TDestination : notnull
{
    /// <summary>
    /// The _equalityComparer field.
    /// </summary>
    private readonly IEqualityComparer<TDestination> _equalityComparer = equalityComparer ?? EqualityComparer<TDestination>.Default;

    /// <summary>
    /// The _source field.
    /// </summary>
    private readonly IObservable<IChangeSet<TSource>> _source = source ?? throw new ArgumentNullException(nameof(source));

    /// <summary>
    /// Initializes a new instance of the <see cref="TransformMany{TSource, TDestination}"/> class.
    /// </summary>
    /// <param name="source">The source value.</param>
    /// <param name="manySelector">The manySelector value.</param>
    /// <param name="equalityComparer">The equalityComparer value.</param>
    public TransformMany(IObservable<IChangeSet<TSource>> source, Func<TSource, ReadOnlyObservableCollection<TDestination>> manySelector, IEqualityComparer<TDestination>? equalityComparer = null)
        : this(
            source,
            manySelector,
            equalityComparer,
            t => Observable.Defer(
                () =>
                {
                    var subsequentChanges = manySelector(t).ToObservableChangeSet();

                    if (manySelector(t).Count > 0)
                    {
                        return subsequentChanges;
                    }

                    return Observable.Return(ChangeSet<TDestination>.Empty).Concat(subsequentChanges);
                }))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TransformMany{TSource, TDestination}"/> class.
    /// </summary>
    /// <param name="source">The source value.</param>
    /// <param name="manySelector">The manySelector value.</param>
    /// <param name="equalityComparer">The equalityComparer value.</param>
    public TransformMany(IObservable<IChangeSet<TSource>> source, Func<TSource, ObservableCollection<TDestination>> manySelector, IEqualityComparer<TDestination>? equalityComparer = null)
        : this(
            source,
            manySelector,
            equalityComparer,
            t => Observable.Defer(
                () =>
                {
                    var subsequentChanges = manySelector(t).ToObservableChangeSet();

                    if (manySelector(t).Count > 0)
                    {
                        return subsequentChanges;
                    }

                    return Observable.Return(ChangeSet<TDestination>.Empty).Concat(subsequentChanges);
                }))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TransformMany{TSource, TDestination}"/> class.
    /// </summary>
    /// <param name="source">The source value.</param>
    /// <param name="manySelector">The manySelector value.</param>
    /// <param name="equalityComparer">The equalityComparer value.</param>
    public TransformMany(IObservable<IChangeSet<TSource>> source, Func<TSource, IObservableList<TDestination>> manySelector, IEqualityComparer<TDestination>? equalityComparer = null)
        : this(
            source,
            s => new ManySelectorFunc(s, x => manySelector(x).Items),
            equalityComparer,
            t => Observable.Defer(
                () =>
                {
                    var subsequentChanges = manySelector(t).Connect();

                    if (manySelector(t).Count > 0)
                    {
                        return subsequentChanges;
                    }

                    return Observable.Return(ChangeSet<TDestination>.Empty).Concat(subsequentChanges);
                }))
    {
    }

    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<TDestination>> Run()
    {
        if (childChanges is not null)
        {
            return CreateWithChangeSet();
        }

        return Observable.Create<IChangeSet<TDestination>>(
            observer =>
            {
                // NB: ChangeAwareList is used internally by dd to capture changes to a list and ensure they can be replayed by subsequent operators
                var result = new ChangeAwareList<TDestination>();

                return _source.Transform(item => new ManyContainer(manySelector(item).ToArray()), true).Select(
                    changes =>
                    {
                        var destinationChanges = new DestinationEnumerator(changes, _equalityComparer);
                        result.Clone(destinationChanges, _equalityComparer);
                        return result.CaptureChanges();
                    }).NotEmpty().SubscribeSafe(observer);
            });
    }

    /// <summary>
    /// Executes the CreateWithChangeSet operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    private IObservable<IChangeSet<TDestination>> CreateWithChangeSet()
    {
        if (childChanges is null)
        {
            throw new InvalidOperationException("_childChanges must not be null.");
        }

        return Observable.Create<IChangeSet<TDestination>>(
            observer =>
            {
                var result = new ChangeAwareList<TDestination>();

                var transformed = _source.Transform(
                    t =>
                    {
                        var locker = InternalEx.NewLock();
                        var collection = manySelector(t);
                        var changes = childChanges(t).Synchronize(locker).Skip(1);
                        return new ManyContainer(collection, changes);
                    }).Publish();

                var outerLock = new Lock();
                var initial = transformed.Synchronize(outerLock).Select(changes => new ChangeSet<TDestination>(new DestinationEnumerator(changes, _equalityComparer)));

                var subsequent = transformed.MergeMany(x => x.Changes).Synchronize(outerLock);

                var init = initial.Select(
                    changes =>
                    {
                        result.Clone(changes, _equalityComparer);
                        return result.CaptureChanges();
                    });

                var subsequentSelection = subsequent.RemoveIndex().Select(
                    changes =>
                    {
                        result.Clone(changes, _equalityComparer);
                        return result.CaptureChanges();
                    });

                var allChanges = init.Merge(subsequentSelection);

                return new CompositeDisposable(allChanges.SubscribeSafe(observer), transformed.Connect());
            });
    }
    // make this an instance

/// <summary>
/// Provides members for the DestinationEnumerator class.
/// </summary>
/// <param name="changes">The changes value.</param>
/// <param name="equalityComparer">The equalityComparer value.</param>
private sealed class DestinationEnumerator(IChangeSet<ManyContainer> changes, IEqualityComparer<TDestination> equalityComparer) : IEnumerable<Change<TDestination>>
    {
        /// <summary>
        /// Executes the GetEnumerator operation.
        /// </summary>
        /// <returns>The result of the operation.</returns>
        public IEnumerator<Change<TDestination>> GetEnumerator()
        {
            foreach (var change in changes)
            {
                switch (change.Reason)
                {
                    case ListChangeReason.Add:
                    case ListChangeReason.Remove:
                        foreach (var destination in change.Item.Current.Destination)
                        {
                            yield return new Change<TDestination>(change.Reason, destination);
                        }

                        break;

                    case ListChangeReason.AddRange:
                    case ListChangeReason.Clear:
                        {
                            var items = change.Range.SelectMany(m => m.Destination);
                            yield return new Change<TDestination>(change.Reason, items);
                        }

                        break;

                    case ListChangeReason.Replace:
                    case ListChangeReason.Refresh:
                        {
                            // this is difficult as we need to discover adds and removes (and perhaps replaced)
                            var currentItems = change.Item.Current.Destination.AsArray();
                            var previousItems = change.Item.Previous.Value.Destination.AsArray();

                            var adds = currentItems.Except(previousItems, equalityComparer);

                            // I am not sure whether it is possible to translate the original change into a replace
                            foreach (var destination in previousItems.Except(currentItems, equalityComparer))
                            {
                                yield return new Change<TDestination>(ListChangeReason.Remove, destination);
                            }

                            foreach (var destination in adds)
                            {
                                yield return new Change<TDestination>(ListChangeReason.Add, destination);
                            }
                        }

                        break;

                    case ListChangeReason.RemoveRange:
                        {
                            foreach (var destination in change.Range.SelectMany(m => m.Destination))
                            {
                                yield return new Change<TDestination>(ListChangeReason.Remove, destination);
                            }
                        }

                        break;

                    case ListChangeReason.Moved:
                        // do nothing as the original index has no bearing on the destination index
                        break;

                    default:
                        throw new IndexOutOfRangeException("Unknown list reason " + change);
                }
            }
        }

        /// <summary>
        /// Executes the GetEnumerator operation.
        /// </summary>
        /// <returns>The result of the operation.</returns>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

/// <summary>
/// Provides members for the ManyContainer class.
/// </summary>
/// <param name="destination">The destination value.</param>
/// <param name="changes">The changes value.</param>
private sealed class ManyContainer(IEnumerable<TDestination> destination, IObservable<IChangeSet<TDestination>>? changes = null)
    {
        /// <summary>
        /// Gets the Changes value.
        /// </summary>
        public IObservable<IChangeSet<TDestination>> Changes { get; } = changes ?? Observable.Empty<IChangeSet<TDestination>>();

        /// <summary>
        /// Gets the Destination value.
        /// </summary>
        public IEnumerable<TDestination> Destination { get; } = destination;
    }

/// <summary>
/// Provides members for the ManySelectorFunc class.
/// </summary>
/// <param name="source">The source value.</param>
/// <param name="selector">The selector value.</param>
private sealed class ManySelectorFunc(TSource source, Func<TSource, IEnumerable<TDestination>> selector) : IEnumerable<TDestination>
    {
        /// <summary>
        /// The _selector field.
        /// </summary>
        private readonly Func<TSource, IEnumerable<TDestination>> _selector = selector ?? throw new ArgumentNullException(nameof(selector));

        /// <summary>
        /// Executes the GetEnumerator operation.
        /// </summary>
        /// <returns>The result of the operation.</returns>
        public IEnumerator<TDestination> GetEnumerator() => _selector(source).GetEnumerator();

        /// <summary>
        /// Executes the GetEnumerator operation.
        /// </summary>
        /// <returns>The result of the operation.</returns>
        IEnumerator IEnumerable.GetEnumerator() => _selector(source).GetEnumerator();
    }
}
