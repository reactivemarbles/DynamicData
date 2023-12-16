// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections;
using System.Reactive.Linq;

namespace DynamicData.List.Internal;

internal sealed class Pager<T>(IObservable<IChangeSet<T>> source, IObservable<IPageRequest> requests)
    where T : notnull
{
    private readonly IObservable<IPageRequest> _requests = requests ?? throw new ArgumentNullException(nameof(requests));

    private readonly IObservable<IChangeSet<T>> _source = source ?? throw new ArgumentNullException(nameof(source));

    public IObservable<IPageChangeSet<T>> Run() => Observable.Create<IPageChangeSet<T>>(
            observer =>
            {
                var locker = new object();
                var all = new List<T>();
                var paged = new ChangeAwareList<T>();

                IPageRequest parameters = new PageRequest(0, 25);

                var requestStream = _requests.Synchronize(locker).Select(
                    request =>
                    {
                        parameters = request;
                        return CheckParametersAndPage(all, paged, request);
                    });

                var dataChanged = _source
                    .Synchronize(locker)
                    .Select(changes => Page(all, paged, parameters, changes));

                return requestStream
                    .Merge(dataChanged)
                    .Where(changes => changes is not null && changes.Count != 0)
                    .Select(x => x!)
                    .SubscribeSafe(observer);
            });

    private static int CalculatePages(ICollection all, IPageRequest? request)
    {
        if (request is null || request.Size >= all.Count || request.Size == 0)
        {
            return 1;
        }

        var pages = all.Count / request.Size;
        var overlap = all.Count % request.Size;

        if (overlap == 0)
        {
            return pages;
        }

        return pages + 1;
    }

    private static PageChangeSet<T>? CheckParametersAndPage(List<T> all, ChangeAwareList<T> paged, IPageRequest? request)
    {
        if (request is null || request.Page < 0 || request.Size < 1)
        {
            return null;
        }

        return Page(all, paged, request);
    }

    private static PageChangeSet<T> Page(List<T> all, ChangeAwareList<T> paged, IPageRequest request, IChangeSet<T>? changeSet = null)
    {
        if (changeSet is not null)
        {
            all.Clone(changeSet);
        }

        var previous = paged;

        var pages = CalculatePages(all, request);
        var page = request.Page > pages ? pages : request.Page;
        var skip = request.Size * (page - 1);

        var current = all.Distinct().Skip(skip)
            .Take(request.Size)
            .ToList();

        var adds = current.Except(previous);
        var removes = previous.Except(current);

        paged.RemoveMany(removes);

        foreach (var add in adds)
        {
            var index = current.IndexOf(add);
            paged.Insert(index, add);
        }

        var startIndex = skip;

        if (changeSet is not null && changeSet.Count != 0)
        {
            var changes = changeSet
                .Where(change => change.Reason == ListChangeReason.Moved
                                 && change.MovedWithinRange(startIndex, startIndex + request.Size)).Select(x => x.Item);

            foreach (var itemChange in changes)
            {
                // check whether an item has moved within the same page
                var currentIndex = itemChange.CurrentIndex - startIndex;
                var previousIndex = itemChange.PreviousIndex - startIndex;
                paged.Move(previousIndex, currentIndex);
            }
        }

        var changed = paged.CaptureChanges();

        return new PageChangeSet<T>(changed, new PageResponse(paged.Count, page, all.Count, pages));
    }
}
