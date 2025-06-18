using System;

namespace DynamicData.Tests.List;

public static partial class ToObservableChangeSetFixture
{
    public const int IntegrationTestItemCount
        #if RELEASE
        = 1_000;
        #else
        = 100;
        #endif

    public enum SchedulerType
    {
        Default,
        TaskPool,
        ThreadPool,
        NewThread
    }

    public enum SourceType
    {
        Immediate,
        Asynchronous
    }

    public record Item
    {
        public static TimeSpan? SelectLifetime(Item item)
            => item.Lifetime;

        public int Id { get; init; }

        public Exception? Error { get; init; }

        public TimeSpan? Lifetime { get; init; }
    }
}
