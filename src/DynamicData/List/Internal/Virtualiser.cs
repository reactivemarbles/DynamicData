// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.List.Internal;
#else

namespace DynamicData.List.Internal;
#endif

/// <summary>
/// Provides members for the Virtualiser class.
/// </summary>
/// <typeparam name="T">The type of the T value.</typeparam>
/// <param name="source">The source value.</param>
/// <param name="requests">The requests value.</param>
internal sealed class Virtualiser<T>(IObservable<IChangeSet<T>> source, IObservable<IVirtualRequest> requests)
    where T : notnull
{
    /// <summary>
    /// The _requests field.
    /// </summary>
    private readonly IObservable<IVirtualRequest> _requests = requests ?? throw new ArgumentNullException(nameof(requests));

    /// <summary>
    /// The _source field.
    /// </summary>
    private readonly IObservable<IChangeSet<T>> _source = source ?? throw new ArgumentNullException(nameof(source));

    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<IVirtualChangeSet<T>> Run() => Observable.Create<IVirtualChangeSet<T>>(
            observer =>
            {
                var locker = InternalEx.NewLock();
                var all = new List<T>();
                var virtualised = new ChangeAwareList<T>();

                IVirtualRequest parameters = new VirtualRequest(0, 25);

                var requestStream = _requests.Synchronize(locker).Select(
                    request =>
                    {
                        parameters = request;
                        return CheckParamsAndVirtualise(all, virtualised, request);
                    });

                var dataChanged = _source.Synchronize(locker).Select(changes => Virtualise(all, virtualised, parameters, changes));

                // TODO: Remove this shared state stuff ie. _parameters
                return requestStream.Merge(dataChanged).Where(changes => changes is not null && changes.Count != 0)
                    .Select(x => x!)
                    .Select(changes => new VirtualChangeSet<T>(changes, new VirtualResponse(virtualised.Count, parameters.StartIndex, all.Count))).SubscribeSafe(observer);
            });

    /// <summary>
    /// Executes the CheckParamsAndVirtualise operation.
    /// </summary>
    /// <param name="all">The all value.</param>
    /// <param name="virtualised">The virtualised value.</param>
    /// <param name="request">The request value.</param>
    /// <returns>The result of the operation.</returns>
    private static IChangeSet<T>? CheckParamsAndVirtualise(IList<T> all, ChangeAwareList<T> virtualised, IVirtualRequest? request)
    {
        if (request is null || request.StartIndex < 0 || request.Size < 1)
        {
            return null;
        }

        return Virtualise(all, virtualised, request);
    }

    /// <summary>
    /// Executes the Virtualise operation.
    /// </summary>
    /// <param name="all">The all value.</param>
    /// <param name="virtualised">The virtualised value.</param>
    /// <param name="request">The request value.</param>
    /// <param name="changeSet">The changeSet value.</param>
    /// <returns>The result of the operation.</returns>
    private static IChangeSet<T> Virtualise(IList<T> all, ChangeAwareList<T> virtualised, IVirtualRequest request, IChangeSet<T>? changeSet = null)
    {
        if (changeSet is not null)
        {
            all.Clone(changeSet);
        }

        var previous = virtualised;

        var current = all.Distinct().Skip(request.StartIndex).Take(request.Size).ToList();

        var adds = current.Except(previous);
        var removes = previous.Except(current);

        virtualised.RemoveMany(removes);

        foreach (var add in adds)
        {
            var index = current.IndexOf(add);
            virtualised.Insert(index, add);
        }

        if (changeSet is not null && changeSet.Count != 0)
        {
            var changes = changeSet.EmptyIfNull().Where(change => change.Reason == ListChangeReason.Moved && change.MovedWithinRange(request.StartIndex, request.StartIndex + request.Size)).Select(x => x.Item);

            foreach (var itemChange in changes)
            {
                // check whether an item has moved within the same page
                var currentIndex = itemChange.CurrentIndex - request.StartIndex;
                var previousIndex = itemChange.PreviousIndex - request.StartIndex;
                virtualised.Move(previousIndex, currentIndex);
            }
        }

        return virtualised.CaptureChanges();
    }
}
