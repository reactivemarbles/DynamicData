using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using DynamicData.Annotations;
using DynamicData.Kernel;

namespace DynamicData.Internal
{
    internal class Virtualiser<T>
    {
        private readonly IObservable<IChangeSet<T>> _source;
        private readonly IObservable<IVirtualRequest> _requests;
        private IVirtualRequest _parameters = new VirtualRequest(0, 25);

        public Virtualiser([NotNull] IObservable<IChangeSet<T>> source, [NotNull] IObservable<IVirtualRequest> requests)
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
                    .Select(request => Virtualise(all, virtualised, request));

                var datachanged = _source
                    .Synchronize(locker)
                    .Select(changes => Virtualise(all, virtualised, changes));

                return requestStream.Merge(datachanged)
                    .Where(changes => changes != null && changes.Count != 0)
                    .SubscribeSafe(observer);
            });

        }

        private IChangeSet<T> Virtualise(List<T> all, ChangeAwareList<T> virtualised, IVirtualRequest request)
        {
            if (request == null || request.StartIndex < 0 || request.Size < 1)
                return null;

            _parameters = request;
            return Virtualise(all, virtualised);
        }

        private IChangeSet<T> Virtualise(List<T> all, ChangeAwareList<T> virtualised, IChangeSet<T> changeset = null)
        {
            if (changeset != null) all.Clone(changeset);

            var previous = virtualised;

            var current = all.Skip(_parameters.StartIndex)
                              .Take(_parameters.Size)
                              .ToList();

            var adds = current.Except(previous);
            var removes = previous.Except(current);

            virtualised.RemoveMany(removes);

            adds.ForEach(t =>
            {
                var index = current.IndexOf(t);
                virtualised.Insert(index, t);
            });

            var moves = changeset.EmptyIfNull()
                                 .Where(change => change.Reason == ListChangeReason.Moved
                                                  && change.MovedWithinRange(_parameters.StartIndex, _parameters.StartIndex + _parameters.Size));

            foreach (var change in moves)
            {
                //check whether an item has moved within the same page
                var currentIndex = change.Item.CurrentIndex - _parameters.StartIndex;
                var previousIndex = change.Item.PreviousIndex - _parameters.StartIndex;
                virtualised.Move(previousIndex, currentIndex);
            }

            //find replaces [Is this ever the case that it can be reached]
            for (var i = 0; i < current.Count; i++)
            {
                var currentItem = current[i];
                var previousItem = previous[i];

                if (ReferenceEquals(currentItem, previousItem))
                    continue;

                var index = virtualised.IndexOf(currentItem);
                virtualised.Move(i, index);
            }
            return virtualised.CaptureChanges();
        }
    }
}
