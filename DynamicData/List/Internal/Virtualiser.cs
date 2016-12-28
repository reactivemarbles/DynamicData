using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using DynamicData.Annotations;
using DynamicData.Kernel;

namespace DynamicData.List.Internal
{
    internal class Virtualiser<T>
    {
        private readonly IObservable<IChangeSet<T>> _source;
        private readonly IObservable<IVirtualRequest> _requests;


        public Virtualiser([NotNull] IObservable<IChangeSet<T>> source, [NotNull] IObservable<IVirtualRequest> requests)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (requests == null) throw new ArgumentNullException(nameof(requests));
            _source = source;
            _requests = requests;
        }

        public IObservable<IVirtualChangeSet<T>> Run()
        {
            return Observable.Create<IVirtualChangeSet<T>>(observer =>
            {
                var locker = new object();
                var all = new List<T>();
                var virtualised = new ChangeAwareList<T>();

                IVirtualRequest parameters = new VirtualRequest(0, 25);

                var requestStream = _requests
                    .Synchronize(locker)
                    .Select(request =>
                    {
                        parameters = request;
                        return CheckParamsAndVirtualise(all, virtualised, request);
                    });

                var datachanged = _source
                    .Synchronize(locker)
                    .Select(changes => Virtualise(all, virtualised, parameters, changes));

                //TODO: Remove this shared state stuff ie. _parameters
                return requestStream.Merge(datachanged)
                    .Where(changes => changes != null && changes.Count != 0)
                    .Select(changes => new VirtualChangeSet<T>(changes, new VirtualResponse(virtualised.Count, parameters.StartIndex, all.Count)))
                    .SubscribeSafe(observer);
            });
        }

        private IChangeSet<T> CheckParamsAndVirtualise(List<T> all, ChangeAwareList<T> virtualised, IVirtualRequest request)
        {
            if (request == null || request.StartIndex < 0 || request.Size < 1)
                return null;

            return Virtualise(all, virtualised, request);
        }

        private IChangeSet<T> Virtualise(List<T> all, ChangeAwareList<T> virtualised, IVirtualRequest request, IChangeSet<T> changeset = null)
        {
            if (changeset != null) all.Clone(changeset);

            var previous = virtualised;

            var current = all.Skip(request.StartIndex)
                              .Take(request.Size)
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
                                                  && change.MovedWithinRange(request.StartIndex, request.StartIndex + request.Size));

            foreach (var change in moves)
            {
                //check whether an item has moved within the same page
                var currentIndex = change.Item.CurrentIndex - request.StartIndex;
                var previousIndex = change.Item.PreviousIndex - request.StartIndex;
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
