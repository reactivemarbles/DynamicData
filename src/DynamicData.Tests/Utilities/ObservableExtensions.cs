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

    public static IDisposable RecordListItems<T>(
            this IObservable<IChangeSet<T>> source,
            out ListItemRecordingObserver<T> observer,
            IScheduler? scheduler = null)
        where T : notnull
    {
        observer = new ListItemRecordingObserver<T>(scheduler ?? GlobalConfig.DefaultScheduler);

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

            var receivedChangeSets = new List<IChangeSet<T>>();

            return source.SubscribeSafe(RawAnonymousObserver.Create<IChangeSet<T>>(
                onNext: changes =>
                {
                    try
                    {
                        foreach (var change in changes)
                        {
                            RequireNotNull(change.Range);

                            RequireContains(reasons, change.Reason);

                            switch (change.Reason.GetChangeType())
                            {
                                case ChangeType.Item:
                                    RequireEqual(change.Item.Reason, change.Reason);
                                    RequireEmpty(change.Range);
                                    break;

                                case ChangeType.Range:
                                    RequireEqual(change.Item, default(ItemChange<T>));
                                    break;
                            }

                            switch (change.Reason)
                            {
                                case ListChangeReason.Add:
                                    RequireEqual(change.Item.PreviousIndex, -1);
                                    RequireFalse(change.Item.Previous.HasValue);

                                    RequireBetween(change.Item.CurrentIndex, -1, sortedItems.Count);
                                    if (change.Item.CurrentIndex is -1)
                                        sortedItems.Add(change.Item.Current);
                                    else
                                        sortedItems.Insert(
                                            index: change.Item.CurrentIndex,
                                            item: change.Item.Current);

                                    break;

                                case ListChangeReason.AddRange:
                                    RequireBetween(change.Range.Index, -1, sortedItems.Count);
                                    if (change.Range.Index is -1)
                                        sortedItems.AddRange(change.Range);
                                    else
                                        sortedItems.InsertRange(
                                            index: change.Range.Index,
                                            collection: change.Range);

                                    break;

                                case ListChangeReason.Clear:
                                    RequireEqual(change.Range.Index, -1);
                                    // The fact that ChangeAwareList can generate Clear changesets with items listed not in the order that they appear in the source seems like a defect to me. Maybe fix?
                                    RequireEquivalent(change.Range, sortedItems, preserveOrder: false);

                                    sortedItems.Clear();

                                    break;

                                case ListChangeReason.Moved:
                                    RequireNotEmpty(sortedItems);

                                    RequireBetween(change.Item.PreviousIndex, 0, sortedItems.Count - 1);
                                    RequireFalse(change.Item.Previous.HasValue);
                                    RequireBetween(change.Item.CurrentIndex, 0, sortedItems.Count - 1);
                                    RequireEqual(change.Item.Current, sortedItems[change.Item.PreviousIndex]);

                                    sortedItems.RemoveAt(change.Item.PreviousIndex);
                                    sortedItems.Insert(
                                        index: change.Item.CurrentIndex,
                                        item: change.Item.Current);

                                    break;

                                case ListChangeReason.Refresh:
                                    RequireNotEmpty(sortedItems);

                                    RequireEqual(change.Item.PreviousIndex, -1);
                                    // This should likely be fixed. The purpose of Refresh changes is to force re-evaluation of an item that specifically has not changed, the previous item will always be the current item, by definition.
                                    // change.Item.Previous.HasValue should be false because only Update changes should specify a previous item.
                                    RequireBetween(change.Item.CurrentIndex, 0, sortedItems.Count - 1);
                                    RequireEqual(change.Item.Current, sortedItems[change.Item.CurrentIndex]);

                                    break;

                                case ListChangeReason.Remove:
                                    RequireNotEmpty(sortedItems);

                                    RequireEqual(change.Item.PreviousIndex, -1);
                                    RequireFalse(change.Item.Previous.HasValue);
                                    RequireBetween(change.Item.CurrentIndex, 0, sortedItems.Count - 1);
                                    RequireEqual(change.Item.Current, sortedItems[change.Item.CurrentIndex]);

                                    sortedItems.RemoveAt(change.Item.CurrentIndex);

                                    break;

                                case ListChangeReason.RemoveRange:
                                    RequireBetween(change.Range.Index, -1, sortedItems.Count - 1);

                                    if (change.Range.Index is -1)
                                        RequireEmpty(change.Range);
                                    else
                                    {
                                        RequireBetween(change.Range.Count, 1, sortedItems.Count - change.Range.Index);
                                        RequireEquivalent(
                                            change.Range,
                                            sortedItems
                                                .Skip(change.Range.Index)
                                                .Take(change.Range.Count),
                                            preserveOrder: true);

                                        sortedItems.RemoveRange(
                                            index: change.Range.Index,
                                            count: change.Range.Count);
                                    }

                                    break;

                                case ListChangeReason.Replace:
                                    RequireNotEmpty(sortedItems);

                                    RequireBetween(change.Item.PreviousIndex, 0, sortedItems.Count - 1);
                                    RequireBetween(change.Item.CurrentIndex, 0, sortedItems.Count - 1);
                                    RequireTrue(change.Item.Previous.HasValue);
                                    RequireEqual(change.Item.Previous.Value, sortedItems[change.Item.CurrentIndex]);

                                    sortedItems[change.Item.CurrentIndex] = change.Item.Current;

                                    break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        observer.OnError(ex);
                    }

                    observer.OnNext(changes);

                    receivedChangeSets.Add(changes);
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
                            RequireContains(reasons, change.Reason);

                            RequireEqual(change.Key, keySelector.Invoke(change.Current));

                            switch (isSorted)
                            {
                                // First change determines whether or not all future changesets need to have indexes
                                case null:
                                    isSorted = change.CurrentIndex is not -1;
                                    break;

                                case true:
                                    RequireGreaterThan(change.CurrentIndex, -1);
                                    break;

                                case false:
                                    RequireEqual(change.CurrentIndex, -1);
                                    break;
                            }

                            switch (change.Reason)
                            {
                                case ChangeReason.Add:
                                    RequireDoesNotContain(itemsByKey.Keys, change.Key);

                                    RequireFalse(change.Previous.HasValue);
                                    RequireEqual(change.PreviousIndex, -1);

                                    if (change.CurrentIndex is not -1)
                                    {
                                        RequireBetween(change.CurrentIndex, 0, sortedKeys.Count);

                                        sortedKeys.Insert(
                                            index: change.CurrentIndex,
                                            item: change.Key);
                                    }

                                    itemsByKey.Add(change.Key, change.Current);

                                    break;

                                case ChangeReason.Moved:
                                    RequireContains(itemsByKey.Keys, change.Key);

                                    RequireFalse(change.Previous.HasValue);
                                    RequireBetween(change.PreviousIndex, 0, sortedKeys.Count - 1);

                                    RequireEqual(change.Current, itemsByKey[change.Key]);
                                    RequireBetween(change.CurrentIndex, 0, sortedKeys.Count - 1);

                                    sortedKeys.RemoveAt(change.PreviousIndex);
                                    sortedKeys.Insert(
                                        index: change.CurrentIndex,
                                        item: change.Key);

                                    break;

                                case ChangeReason.Refresh:
                                    RequireContains(itemsByKey.Keys, change.Key);

                                    RequireFalse(change.Previous.HasValue);
                                    RequireEqual(change.PreviousIndex, -1);

                                    RequireEqual(change.Current, itemsByKey[change.Key]);

                                    if (change.CurrentIndex is not -1)
                                    {
                                        RequireBetween(change.CurrentIndex, 0, sortedKeys.Count - 1);
                                        RequireEqual(change.Key, sortedKeys[change.CurrentIndex]);
                                    }

                                    break;

                                case ChangeReason.Remove:
                                    RequireContains(itemsByKey.Keys, change.Key);

                                    RequireFalse(change.Previous.HasValue);
                                    RequireEqual(change.PreviousIndex, -1);

                                    RequireEqual(change.Current, itemsByKey[change.Key]);

                                    if (change.CurrentIndex is not -1)
                                    {
                                        RequireBetween(change.CurrentIndex, 0, sortedKeys.Count - 1);
                                        RequireEqual(change.Key, sortedKeys[change.CurrentIndex]);

                                        sortedKeys.RemoveAt(change.CurrentIndex);
                                    }

                                    itemsByKey.Remove(change.Key);

                                    break;

                                case ChangeReason.Update:
                                    RequireContains(itemsByKey.Keys, change.Key);

                                    RequireTrue(change.Previous.HasValue);
                                    RequireEqual(change.Previous.Value, itemsByKey[change.Key]);

                                    if (change.CurrentIndex is -1)
                                    {
                                        RequireEqual(change.PreviousIndex, -1);
                                    }
                                    else
                                    {
                                        RequireBetween(change.PreviousIndex, 0, sortedKeys.Count - 1);
                                        RequireEqual(change.Key, sortedKeys[change.PreviousIndex]);

                                        RequireBetween(change.CurrentIndex, 0, sortedKeys.Count - 1);

                                        sortedKeys.RemoveAt(change.PreviousIndex);
                                        sortedKeys.Insert(
                                            index: change.CurrentIndex,
                                            item: change.Key);
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
                        switch (incomingNotification.Kind)
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

    private static void RequireNotNull<T>(T value)
    {
        if (value is null)
        {
            Assert.Fail("Expected value to be non-null.");
        }
    }

    private static void RequireTrue(bool value)
    {
        if (!value)
        {
            Assert.Fail("Expected value to be true.");
        }
    }

    private static void RequireFalse(bool value)
    {
        if (value)
        {
            Assert.Fail("Expected value to be false.");
        }
    }

    private static void RequireEqual<T>(T actual, T expected)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
        {
            Assert.Fail($"Expected {actual} to equal {expected}.");
        }
    }

    private static void RequireGreaterThan<T>(T actual, T lowerBound)
        where T : IComparable<T>
    {
        if (actual.CompareTo(lowerBound) <= 0)
        {
            Assert.Fail($"Expected {actual} to be greater than {lowerBound}.");
        }
    }

    private static void RequireBetween<T>(T actual, T lowerBound, T upperBound)
        where T : IComparable<T>
    {
        if (actual.CompareTo(lowerBound) < 0 || actual.CompareTo(upperBound) > 0)
        {
            Assert.Fail($"Expected {actual} to be between {lowerBound} and {upperBound}.");
        }
    }

    private static void RequireContains<T>(IEnumerable<T> items, T expected)
    {
        if (!items.Contains(expected))
        {
            Assert.Fail($"Expected sequence to contain {expected}.");
        }
    }

    private static void RequireDoesNotContain<T>(IEnumerable<T> items, T unexpected)
    {
        if (items.Contains(unexpected))
        {
            Assert.Fail($"Expected sequence not to contain {unexpected}.");
        }
    }

    private static void RequireEmpty<T>(IEnumerable<T> items)
    {
        if (items.Any())
        {
            Assert.Fail("Expected sequence to be empty.");
        }
    }

    private static void RequireNotEmpty<T>(IEnumerable<T> items)
    {
        if (!items.Any())
        {
            Assert.Fail("Expected sequence not to be empty.");
        }
    }

    private static void RequireEquivalent<T>(IEnumerable<T> actual, IEnumerable<T> expected, bool preserveOrder)
    {
        var actualList = actual.ToList();
        var expectedList = expected.ToList();

        if (preserveOrder)
        {
            if (!actualList.SequenceEqual(expectedList))
            {
                Assert.Fail("Expected sequences to be equivalent with matching order.");
            }

            return;
        }

        if (actualList.Count != expectedList.Count)
        {
            Assert.Fail("Expected sequences to have the same count.");
        }

        var matched = new bool[expectedList.Count];
        foreach (var actualItem in actualList)
        {
            var matchedIndex = -1;
            for (var i = 0; i < expectedList.Count; i++)
            {
                if (!matched[i] && EqualityComparer<T>.Default.Equals(actualItem, expectedList[i]))
                {
                    matchedIndex = i;
                    break;
                }
            }

            if (matchedIndex < 0)
            {
                Assert.Fail($"Expected sequence item {actualItem} to have a matching item.");
            }

            matched[matchedIndex] = true;
        }
    }
}
