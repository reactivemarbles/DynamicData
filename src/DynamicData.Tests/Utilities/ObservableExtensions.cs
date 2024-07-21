using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

using FluentAssertions;

namespace DynamicData.Tests.Utilities;

internal static class ObservableExtensions
{
    /// <summary>
    /// Forces the given observable to fail after the specified number events if an exception is provided.
    /// </summary>
    /// <typeparam name="T">Observable type.</typeparam>
    /// <param name="source">Source Observable.</param>
    /// <param name="count">Number of events before failing.</param>
    /// <param name="e">Exception to fail with.</param>
    /// <returns>The new Observable.</returns>
    public static IObservable<T> ForceFail<T>(this IObservable<T> source, int count, Exception? e) =>
        e is not null
            ? source.Take(count).Concat(Observable.Throw<T>(e))
            : source;

    /// <summary>
    /// Creates an observable that parallelizes some given work by taking the source observable, creates multiple subscriptions, limiting each to a certain number of values, and 
    /// attaching some work to be done in parallel to each before merging them back together.
    /// </summary>
    /// <typeparam name="T">Input Observable type.</typeparam>
    /// <typeparam name="U">Output Observable type.</typeparam>
    /// <param name="source">Source Observable.</param>
    /// <param name="count">Total number of values to process.</param>
    /// <param name="parallel">Total number of subscriptions to create.</param>
    /// <param name="fnAttachParallelWork">Function to append work to be done before the merging.</param>
    /// <returns>An Observable that contains the values resulting from the work performed.</returns>
    public static IObservable<U> Parallelize<T, U>(this IObservable<T> source, int count, int parallel, Func<IObservable<T>, IObservable<U>> fnAttachParallelWork) =>
        Observable.Merge(Distribute(count, parallel).Select(n => fnAttachParallelWork(source.Take(n))));

    /// <summary>
    /// Creates an observable that parallelizes some given work by taking the source observable, creates multiple subscriptions, limiting each to a certain number of values, and 
    /// merging them back together.
    /// </summary>
    /// <typeparam name="T">Observable type.</typeparam>
    /// <param name="source">Source Observable.</param>
    /// <param name="count">Total number of values to process.</param>
    /// <param name="parallel">Total number of subscriptions to create.</param>
    /// <returns>An Observable that contains the values resulting from the merged sequences.</returns>
    public static IObservable<T> Parallelize<T>(this IObservable<T> source, int count, int parallel) =>
        Observable.Merge(Distribute(count, parallel).Select(n => source.Take(n)));

    public static IDisposable RecordCacheItems<TObject, TKey>(
            this IObservable<IChangeSet<TObject, TKey>> source,
            out CacheItemRecordingObserver<TObject, TKey> observer,
            IScheduler? scheduler = null)
        where TObject : notnull
        where TKey : notnull
    {
        observer = new CacheItemRecordingObserver<TObject, TKey>(scheduler ?? GlobalConfig.DefaultScheduler);

        return source.Subscribe(observer);
    }

    public static IDisposable RecordValues<T>(
        this IObservable<T> source,
        out ValueRecordingObserver<T> observer,
        IScheduler? scheduler = null)
    {
        observer = new ValueRecordingObserver<T>(scheduler ?? GlobalConfig.DefaultScheduler);

        return source.Subscribe(observer);
    }

    public static IObservable<IChangeSet<T>> ValidateChangeSets<T>(this IObservable<IChangeSet<T>> source)
            where T : notnull
        // Using Raw observable and observer classes to bypass normal RX safeguards
        // This allows the operator to be combined with other operators that might be testing for things that the safeguards normally prevent.
        => RawAnonymousObservable.Create<IChangeSet<T>>(observer =>
        {
            var sortedItems = new List<T>();

            var reasons = Enum.GetValues<ListChangeReason>();

            return source.SubscribeSafe(RawAnonymousObserver.Create<IChangeSet<T>>(
                onNext: changes =>
                {
                    try
                    {
                        foreach (var change in changes)
                        {
                            change.Range.Should().NotBeNull();

                            change.Reason.Should().BeOneOf(reasons);

                            switch (change.Reason.GetChangeType())
                            {
                                case ChangeType.Item:
                                    change.Item.Reason.Should().Be(change.Reason);

                                    change.Range.Should().BeEmpty("single-item changes should not specify range info");
                                    break;

                                case ChangeType.Range:
                                    change.Item.Reason.Should().Be(default, "range changes should not specify single-item info");
                                    change.Item.PreviousIndex.Should().Be(-1, "range changes should not specify single-item info");
                                    change.Item.Previous.HasValue.Should().BeFalse("range changes should not specify single-item info");
                                    change.Item.CurrentIndex.Should().Be(-1, "range changes should not specify single-item info");
                                    change.Item.Current.Should().Be(default, "range changes should not specify single-item info");
                                    break;
                            }

                            switch (change.Reason)
                            {
                                case ListChangeReason.Add:
                                    change.Item.PreviousIndex.Should().Be(-1, "only Moved changes should specify a previous index");
                                    change.Item.Previous.HasValue.Should().BeFalse("only Update changes should specify a previous item");

                                    change.Item.CurrentIndex.Should().BeInRange(-1, sortedItems.Count, "the insertion index should be omitted, a valid index of the collection, or the next available index of the collection");
                                    if (change.Item.CurrentIndex is -1)
                                        sortedItems.Add(change.Item.Current);
                                    else
                                        sortedItems.Insert(
                                            index:  change.Item.CurrentIndex,
                                            item:   change.Item.Current);

                                    break;

                                case ListChangeReason.AddRange:
                                    change.Range.Index.Should().BeInRange(-1, sortedItems.Count - 1, "the insertion index should be omitted, a valid index of the collection, or the next available index of the collection");
                                    if (change.Range.Index is -1)
                                        sortedItems.AddRange(change.Range);
                                    else
                                        sortedItems.InsertRange(
                                            index:      change.Range.Index,
                                            collection: change.Range);

                                    break;

                                case ListChangeReason.Clear:
                                    change.Range.Index.Should().Be(-1, "a Clear change has no target index");
                                    change.Range.Should().BeEquivalentTo(
                                        sortedItems,
                                        config => config.WithStrictOrdering(),
                                        "items in the range should match the corresponding items in the collection");

                                    sortedItems.Clear();

                                    break;

                                case ListChangeReason.Moved:
                                    sortedItems.Should().NotBeEmpty("an item cannot be moved within an empty collection");

                                    change.Item.PreviousIndex.Should().BeInRange(0, sortedItems.Count - 1, "the source index should be a valid index of the collection");
                                    change.Item.Previous.HasValue.Should().BeFalse("only Update changes should specify a previous item");
                                    change.Item.CurrentIndex.Should().BeInRange(0, sortedItems.Count - 1, "the target index should be a valid index of the collection");
                                    change.Item.Current.Should().Be(sortedItems[change.Item.PreviousIndex], "the item to be moved should match the corresponding item in the collection");

                                    sortedItems.RemoveAt(change.Item.PreviousIndex);
                                    sortedItems.Insert(
                                        index:  change.Item.CurrentIndex,
                                        item:   change.Item.Current);

                                    break;

                                case ListChangeReason.Refresh:
                                    sortedItems.Should().NotBeEmpty("an item cannot be refreshed within an empty collection");

                                    change.Item.PreviousIndex.Should().Be(-1, "only Moved changes should specify a previous index");
                                    change.Item.Previous.HasValue.Should().BeFalse("only Update changes should specify a previous item");
                                    change.Item.CurrentIndex.Should().BeInRange(0, sortedItems.Count - 1, "the target index should be a valid index of the collection");
                                    change.Item.Current.Should().Be(sortedItems[change.Item.CurrentIndex], "the item to be refreshed should match the corresponding item in the collection");

                                    break;

                                case ListChangeReason.Remove:
                                    sortedItems.Should().NotBeEmpty("an item cannot be removed from an empty collection");

                                    change.Item.PreviousIndex.Should().Be(-1, "only Moved changes should specify a previous index");
                                    change.Item.Previous.HasValue.Should().BeFalse("only Update changes should specify a previous item");
                                    change.Item.CurrentIndex.Should().BeInRange(0, sortedItems.Count - 1, "the index to be removed should be a valid index of the collection");
                                    change.Item.Current.Should().Be(sortedItems[change.Item.CurrentIndex], "the item to be removed should match the corresponding item in the collection");

                                    sortedItems.RemoveAt(change.Item.CurrentIndex);

                                    break;

                                case ListChangeReason.RemoveRange:
                                    change.Range.Index.Should().BeInRange(-1, sortedItems.Count - 1, "the removal index should be omitted, or a valid index of the collection");

                                    if (change.Range.Index is -1)
                                        change.Range.Should().BeEmpty("the removal index was omitted");
                                    else
                                    {
                                        change.Range.Count.Should().BeInRange(1, sortedItems.Count - change.Range.Index, "the range to be removed should contain more items than exist in the collection, at the given removal index");
                                        change.Range.Should().BeEquivalentTo(
                                            sortedItems
                                                .Skip(change.Range.Index)
                                                .Take(change.Range.Count),
                                            config => config.WithStrictOrdering(), "items to be removed should match the corresponding items in the collection");

                                        sortedItems.RemoveRange(
                                            index:  change.Range.Index,
                                            count:  change.Range.Count);
                                    }

                                    break;

                                case ListChangeReason.Replace:
                                    sortedItems.Should().NotBeEmpty("an item cannot be replaced within an empty collection");

                                    change.Item.PreviousIndex.Should().Be(-1, "only Moved changes should specify a previous index");
                                    change.Item.CurrentIndex.Should().BeInRange(0, sortedItems.Count - 1, "the index to be replaced should be a valid index of the collection");
                                    change.Item.Previous.HasValue.Should().BeTrue("a Replace change should specify a previous item");
                                    change.Item.Previous.Should().Be(sortedItems[change.Item.CurrentIndex], "the replaced item should match the corresponding item in the collection");

                                    sortedItems[change.Item.CurrentIndex] = change.Item.Current;

                                    break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        observer.OnError(ex);
                    }
                },
                onError: observer.OnError,
                onCompleted: observer.OnCompleted));
        });

    public static IObservable<IChangeSet<TObject, TKey>> ValidateChangeSets<TObject, TKey>(
                this IObservable<IChangeSet<TObject, TKey>> source,
                Func<TObject, TKey> keySelector)
            where TObject : notnull
            where TKey : notnull
        // Using Raw observable and observer classes to bypass normal RX safeguards
        // This allows the operator to be combined with other operators that might be testing for things that the safeguards normally prevent.
        => RawAnonymousObservable.Create<IChangeSet<TObject, TKey>>(observer =>
        {
            var itemsByKey = new Dictionary<TKey, TObject>();
            var sortedKeys = new List<TKey>();
            var isSorted = null as bool?;
            
            var reasons = Enum.GetValues<ChangeReason>();

            return source.SubscribeSafe(RawAnonymousObserver.Create<IChangeSet<TObject, TKey>>(
                onNext: changes =>
                {
                    try
                    {
                        foreach (var change in changes)
                        {
                            change.Reason.Should().BeOneOf(reasons);

                            change.Key.Should().Be(keySelector.Invoke(change.Current), "the specified key should match the specified item's key");

                            switch (isSorted)
                            {
                                // First change determines whether or not all future changesets need to have indexes
                                case null:
                                    isSorted = change.CurrentIndex is not -1;
                                    break;

                                case true:
                                    change.CurrentIndex.Should().BeGreaterThan(-1, "indexes should be specified for a stream that specified them initially");
                                    break;

                                case false:
                                    change.CurrentIndex.Should().Be(-1, "indexes should be omitted for a stream that omitted them initially");
                                    break;
                            }

                            switch (change.Reason)
                            {
                                case ChangeReason.Add:
                                    itemsByKey.Keys.Should().NotContain(change.Key, "the key to be added should not already exist in the collection");

                                    change.Previous.HasValue.Should().BeFalse("only Update changes should specify a previous item");
                                    change.PreviousIndex.Should().Be(-1, "only Moved or Update changes should specify a previous index");

                                    if (change.CurrentIndex is not -1)
                                    {
                                        change.CurrentIndex.Should().BeInRange(0, sortedKeys.Count, "the index to be added should be a valid index of the collection, or the next available index of the collection");

                                        sortedKeys.Insert(
                                            index:  change.CurrentIndex,
                                            item:   change.Key);
                                    }

                                    itemsByKey.Add(change.Key, change.Current);

                                    break;

                                case ChangeReason.Moved:
                                    itemsByKey.Keys.Should().Contain(change.Key, "the key to be moved should exist in the collection");

                                    change.Previous.HasValue.Should().BeFalse("only Update changes should specify a previous item");
                                    change.PreviousIndex.Should().BeInRange(0, sortedKeys.Count - 1, "the source index should be a valid index of the collection");
                                    
                                    change.Current.Should().Be(itemsByKey[change.Key], "the item to be moved should match the corresponding item in the collection");
                                    change.CurrentIndex.Should().BeInRange(0, sortedKeys.Count - 1, "the target index should be a valid index of the collection");

                                    sortedKeys.RemoveAt(change.PreviousIndex);
                                    sortedKeys.Insert(
                                        index:  change.CurrentIndex,
                                        item:   change.Key);

                                    break;

                                case ChangeReason.Refresh:
                                    itemsByKey.Keys.Should().Contain(change.Key, "the key to be refreshed should exist in the collection");

                                    change.Previous.HasValue.Should().BeFalse("only Update changes should specify a previous item");
                                    change.PreviousIndex.Should().Be(-1, "only Moved or Update changes should specify a previous index");

                                    change.Current.Should().Be(itemsByKey[change.Key], "the item to be refreshed should match the corresponding item in the collection");

                                    if (change.CurrentIndex is not -1)
                                    {
                                        change.CurrentIndex.Should().BeInRange(0, sortedKeys.Count - 1, "the index to be refreshed should be a valid index of the collection");
                                        change.Key.Should().Be(sortedKeys[change.CurrentIndex], "the key to be refreshed should match the corresponding key in the collection");
                                    }

                                    break;

                                case ChangeReason.Remove:
                                    itemsByKey.Keys.Should().Contain(change.Key, "the key to be removed should exist in the collection");

                                    change.Previous.HasValue.Should().BeFalse("only Update changes should specify a previous item");
                                    change.PreviousIndex.Should().Be(-1, "only Moved or Update changes should specify a previous index");

                                    change.Current.Should().Be(itemsByKey[change.Key], "the item to be removed should match the corresponding item in the collection");

                                    if (change.CurrentIndex is not -1)
                                    {
                                        change.CurrentIndex.Should().BeInRange(0, sortedKeys.Count - 1, "the index to be removed should be a valid index of the collection");
                                        change.Key.Should().Be(sortedKeys[change.CurrentIndex], "the key to be removed should match the corresponding key in the collection");

                                        sortedKeys.RemoveAt(change.CurrentIndex);
                                    }

                                    itemsByKey.Remove(change.Key);

                                    break;

                                case ChangeReason.Update:
                                    itemsByKey.Keys.Should().Contain(change.Key, "the key to be updated should exist in the collection");

                                    change.Previous.HasValue.Should().BeTrue("an Update change should specify a previous item");
                                    change.Previous.Value.Should().Be(itemsByKey[change.Key], "the item to be updated should match the corresponding item in the collection");

                                    if (change.CurrentIndex is -1)
                                    {
                                        change.PreviousIndex.Should().Be(-1, "a previous index should only be specified if a current index is specified");
                                    }
                                    else
                                    {
                                        change.PreviousIndex.Should().BeInRange(0, sortedKeys.Count - 1, "the source index should be a valid index of the collection");
                                        change.Key.Should().Be(sortedKeys[change.PreviousIndex], "the key to be updated should match the corresponding key in the collection");

                                        change.CurrentIndex.Should().BeInRange(0, sortedKeys.Count - 1, "the target index should be a valid index of the collection");

                                        sortedKeys.RemoveAt(change.PreviousIndex);
                                        sortedKeys.Insert(
                                            index:  change.CurrentIndex,
                                            item:   change.Key);
                                    }

                                    itemsByKey[change.Key] = change.Current;

                                    break;
                            }
                        }

                        observer.OnNext(changes);
                    }
                    catch (Exception ex)
                    {
                        observer.OnError(ex);
                    }
                },
                onError: observer.OnError,
                onCompleted: observer.OnCompleted));
        });

    public static IObservable<T> ValidateSynchronization<T>(this IObservable<T> source)
        // Using Raw observable and observer classes to bypass normal RX safeguards
        // This allows the operator to be combined with other operators that might be testing for things that the safeguards normally prevent.
        => RawAnonymousObservable.Create<T>(observer =>
        {
            var inFlightNotification = null as Notification<T>;
            var synchronizationGate = new object();

            // Not using .Do() so we can track the *entire* in-flight period of a notification, including all synchronous downstream processing.
            return source.SubscribeSafe(RawAnonymousObserver.Create<T>(
                onNext: value => ProcessIncomingNotification(Notification.CreateOnNext(value)),
                onError: error => ProcessIncomingNotification(Notification.CreateOnError<T>(error)),
                onCompleted: () => ProcessIncomingNotification(Notification.CreateOnCompleted<T>())));

            void ProcessIncomingNotification(Notification<T> incomingNotification)
            {
                try
                {
                    var priorNotification = Interlocked.Exchange(ref inFlightNotification, incomingNotification);
                    if (priorNotification is not null)
                        throw new UnsynchronizedNotificationException<T>()
                        {
                            IncomingNotification = incomingNotification,
                            PriorNotification = priorNotification
                        };

                    lock (synchronizationGate)
                    {
                        switch(incomingNotification.Kind)
                        {
                            case NotificationKind.OnNext:
                                observer.OnNext(incomingNotification.Value);
                                break;

                            case NotificationKind.OnError:
                                observer.OnError(incomingNotification.Exception!);
                                break;

                            case NotificationKind.OnCompleted:
                                observer.OnCompleted();
                                break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    lock (synchronizationGate)
                    {
                        observer.OnError(ex);
                    }
                }
                finally
                {
                    Interlocked.Exchange(ref inFlightNotification, null);
                }
            }
        });

    // Emits "parallel" number of values that add up to "count"
    private static IEnumerable<int> Distribute(int count, int parallel) =>
        (count, parallel, count / parallel) switch
        {
            // Not enough count for each parallel, so just return as many as needed
            (int c, int p, _) when c <= p => Enumerable.Repeat(1, c),

            // Divides equally, so return the ratio for the parallel quantity
            (int c, int p, int ratio) when (c % p) == 0 => Enumerable.Repeat(ratio, p),

            // Doesn't divide equally, so return the ratio for the parallel quantity, and the remainder for the last one
            (int c, int p, int ratio) => Enumerable.Repeat(ratio, p - 1).Append(c - (ratio * (p - 1))),
        };
}
