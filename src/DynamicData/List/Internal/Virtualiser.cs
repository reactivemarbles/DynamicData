// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;

using DynamicData.Internal;

namespace DynamicData.List.Internal;

internal sealed class Virtualiser<T>(IObservable<IChangeSet<T>> source, IObservable<IVirtualRequest> requests)
    where T : notnull
{
    private readonly IObservable<IVirtualRequest> _requests = requests ?? throw new ArgumentNullException(nameof(requests));

    private readonly IObservable<IChangeSet<T>> _source = source ?? throw new ArgumentNullException(nameof(source));

    public IObservable<IVirtualChangeSet<T>> Run() => Observable.Create<IVirtualChangeSet<T>>(
            observer =>
            {
                // SharedDeliveryQueue + SynchronizeSafe replaces Synchronize(locker) so the
                // gate lock is released before downstream OnNext. Closes the cross-cache
                // deadlock window.
                var queue = new SharedDeliveryQueue();
                var all = new List<T>();
                var virtualised = new ChangeAwareList<T>();

                IVirtualRequest parameters = new VirtualRequest(0, 25);

                var requestStream = _requests.SynchronizeSafe(queue).Select(
                    request =>
                    {
                        parameters = request;
                        return CheckParamsAndVirtualise(all, virtualised, request);
                    });

                var dataChanged = _source.SynchronizeSafe(queue).Select(changes => Virtualise(all, virtualised, parameters, changes));

                var publisher = requestStream.UnsynchronizedMerge(dataChanged).Where(changes => changes is not null && changes.Count != 0)
                    .Select(x => x!)
                    .Select(changes => new VirtualChangeSet<T>(changes, new VirtualResponse(virtualised.Count, parameters.StartIndex, all.Count))).SubscribeSafe(observer);

                return new CompositeDisposable(publisher, queue);
            });

    private static IChangeSet<T>? CheckParamsAndVirtualise(IList<T> all, ChangeAwareList<T> virtualised, IVirtualRequest? request)
    {
        if (request is null || request.StartIndex < 0 || request.Size < 1)
        {
            return null;
        }

        return Virtualise(all, virtualised, request);
    }

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
