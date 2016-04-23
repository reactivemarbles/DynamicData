using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using DynamicData.Annotations;
using DynamicData.Controllers;
using DynamicData.Kernel;
using DynamicData.Operators;

namespace DynamicData.Internal
{
    internal class Pager<T>
    {
        private readonly IObservable<IChangeSet<T>> _source;
        private readonly IObservable<IPageRequest> _requests;
        private IPageRequest _parameters = new PageRequest(0, 25);

        public Pager([NotNull] IObservable<IChangeSet<T>> source, [NotNull] IObservable<IPageRequest> requests)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (requests == null) throw new ArgumentNullException(nameof(requests));
            _source = source;
            _requests = requests;
        }

        public IObservable<IChangeSet<T>> Run()
        {
            return Observable.Create<IChangeSet<T>>(observer =>
            {
                var locker = new object();
                var all = new List<T>();
                var virtualised = new ChangeAwareList<T>();

                var requestStream = _requests
                    .Synchronize(locker)
                    .Select(request => Page(all, virtualised, request));

                var datachanged = _source
                    .Synchronize(locker)
                    .Select(changes => Page(all, virtualised, changes));

                return requestStream.Merge(datachanged)
                    .Where(changes => changes != null && changes.Count != 0)
                    .SubscribeSafe(observer);
            });
        }

        private IChangeSet<T> Page(List<T> all, ChangeAwareList<T> paged, IPageRequest request)
        {
            if (request == null || request.Page < 0 || request.Size < 1)
                return null;

            if (request.Size == _parameters.Size && request.Page == _parameters.Page)
                return null;

            _parameters = request;
            return Page(all, paged);
        }

        private IChangeSet<T> Page(List<T> all, ChangeAwareList<T> paged, IChangeSet<T> changeset = null)
        {
            if (changeset != null) all.Clone(changeset);

            var previous = paged;

            int pages = CalculatePages(all);
            int page = _parameters.Page > pages ? pages : _parameters.Page;
            int skip = _parameters.Size * (page - 1);

            var current = all.Skip(skip)
                              .Take(_parameters.Size)
                              .ToList();

            var adds = current.Except(previous);
            var removes = previous.Except(current);

            paged.RemoveMany(removes);

            adds.ForEach(t =>
            {
                var index = current.IndexOf(t);
                paged.Insert(index, t);
            });

            var startIndex = skip;

            var moves = changeset.EmptyIfNull()
                                 .Where(change => change.Reason == ListChangeReason.Moved
                                                  && change.MovedWithinRange(startIndex, startIndex + _parameters.Size));

            foreach (var change in moves)
            {
                //check whether an item has moved within the same page
                var currentIndex = change.Item.CurrentIndex - startIndex;
                var previousIndex = change.Item.PreviousIndex - startIndex;
                paged.Move(previousIndex, currentIndex);
            }

            //find replaces [Is this ever the case that it can be reached]
            for (int i = 0; i < current.Count; i++)
            {
                var currentItem = current[i];
                var previousItem = previous[i];

                if (ReferenceEquals(currentItem, previousItem))
                    continue;

                var index = paged.IndexOf(currentItem);
                paged.Move(i, index);
            }
            return paged.CaptureChanges();
        }

        private int CalculatePages(List<T> all)
        {
            if (_parameters.Size >= all.Count)
            {
                return 1;
            }

            int pages = all.Count / _parameters.Size;
            int overlap = all.Count % _parameters.Size;

            if (overlap == 0)
            {
                return pages;
            }
            return pages + 1;
        }
    }
}
