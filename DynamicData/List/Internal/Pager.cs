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
        private readonly List<T> _all = new List<T>();
        private readonly ChangeAwareList<T> _paged = new ChangeAwareList<T>();

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
            var locker = new object();
            var request = _requests
                .Synchronize(locker)
                .Select(Page);

            var datachanged = _source
                .Synchronize(locker)
                .Select(Page);

            return request.Merge(datachanged)
                          .Where(changes => changes != null && changes.Count != 0);
        }

        private IChangeSet<T> Page(IPageRequest request)
        {
            if (request == null || request.Page < 0 || request.Size < 1)
                return null;

            if (request.Size == _parameters.Size && request.Page == _parameters.Page)
                return null;

            _parameters = request;
            return Page();
        }

        private IChangeSet<T> Page(IChangeSet<T> changeset = null)
        {
            if (changeset != null) _all.Clone(changeset);

            var previous = _paged;

            int pages = CalculatePages();
            int page = _parameters.Page > pages ? pages : _parameters.Page;
            int skip = _parameters.Size * (page - 1);

            var current = _all.Skip(skip)
                              .Take(_parameters.Size)
                              .ToList();

            var adds = current.Except(previous);
            var removes = previous.Except(current);

            _paged.RemoveMany(removes);

            adds.ForEach(t =>
            {
                var index = current.IndexOf(t);
                _paged.Insert(index, t);
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
                _paged.Move(previousIndex, currentIndex);
            }

            //find replaces [Is this ever the case that it can be reached]
            for (int i = 0; i < current.Count; i++)
            {
                var currentItem = current[i];
                var previousItem = previous[i];

                if (ReferenceEquals(currentItem, previousItem))
                    continue;

                var index = _paged.IndexOf(currentItem);
                _paged.Move(i, index);
            }
            return _paged.CaptureChanges();
        }

        private int CalculatePages()
        {
            if (_parameters.Size >= _all.Count)
            {
                return 1;
            }

            int pages = _all.Count / _parameters.Size;
            int overlap = _all.Count % _parameters.Size;

            if (overlap == 0)
            {
                return pages;
            }
            return pages + 1;
        }
    }
}
