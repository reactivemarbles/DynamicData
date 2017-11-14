using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;

namespace DynamicData.Tests.Playground
{
    public static class ObservableEx
    {

        public static IObservable<IChangeSet<T>> BufferInitial<T>(this IObservable<IChangeSet<T>> source, TimeSpan initalBuffer, IScheduler scheduler = null)
        {
            return source.DeferUntilLoaded().Publish(shared =>
            {
                var initial = shared.Buffer(initalBuffer, scheduler ?? Scheduler.Default)
                    .FlattenBufferResult()
                    .Take(1);

                return initial.Concat(shared);
            });
        }
    }
}