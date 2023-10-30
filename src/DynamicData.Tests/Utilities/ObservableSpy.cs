using System;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;

namespace DynamicData.Tests.Utilities;

internal static class ObservableSpy
{
    private static readonly string ChangeSetEntrySpacing = Environment.NewLine + "\t";

    /// <summary>
    /// Spys on the given IObservable{T} by emitting logging information that is tagged with the current ThreadId for all related
    /// events including every invocation on the Observer, subscriptions, disposes, and exceptions.
    /// </summary>
    /// <typeparam name="T">The type of the Observable.</typeparam>
    /// <param name="source">The source IObservable to be Spied on.</param>
    /// <param name="logger">The logger instance to use for logging.</param>
    /// <param name="infoText">Optional text to include with each log message.</param>
    /// <param name="formatter">Optional Value transformer to control how the observed values are logged.</param>
    /// <param name="showSubs">Indicates whether or not subscription related messages should be emitted.</param>
    /// <param name="showTimestamps">Indicates whether or not timestamps should be prepended to messages.</param>
    /// <returns>An IObservable{T} with the Spy events included.</returns>
    /// <remarks>Adapted from https://stackoverflow.com/q/20220755/.</remarks>
    public static IObservable<T> Spy<T>(this IObservable<T> source, string? infoText = null, Action<string>? logger = null,
                                                                    Func<T, string?>? formatter = null, bool showSubs = true,
                                                                    bool showTimestamps = true)
    {
        static string NoTimestamp() => string.Empty;
        static string HighResTimestamp() => DateTimeOffset.UtcNow.ToString("HH:mm:ss.fffffff") + " ";

        formatter ??= (t => t?.ToString() ?? "{Null}");
        logger = CreateLogger(logger ?? Console.WriteLine, showTimestamps ? HighResTimestamp : NoTimestamp, infoText ?? $"IObservable<{typeof(T).Name}>");

        logger("Creating Observable");

        int subscriptionCounter = 0;
        return Observable.Create<T>(obs =>
        {
            var valueCounter = 0;
            bool? completedSuccessfully = null;

            if (showSubs)
            {
                logger("Creating Subscription");
            }
            try
            {
                var subscription = source
                    .Do(x => logger($"OnNext() (#{Interlocked.Increment(ref valueCounter)}): {formatter(x)}"),
                        ex => { logger($"OnError() ({valueCounter} Values) [Exception: {ex}]"); completedSuccessfully = false; },
                        () => { logger($"OnCompleted() ({valueCounter} Values)"); completedSuccessfully = true; })
                    .Subscribe(t =>
                    {
                        try
                        {
                            obs.OnNext(t);
                        }
                        catch (Exception ex)
                        {
                            logger($"Downstream exception ({ex})");
                            throw;
                        }
                    }, obs.OnError, obs.OnCompleted);

                return Disposable.Create(() =>
                {
                    if (showSubs)
                    {
                        switch (completedSuccessfully)
                        {
                            case true: logger("Disposing because Observable Sequence Completed Successfully"); break;
                            case false: logger("Disposing due to Failed Observable Sequence"); break;
                            case null: logger("Disposing due to Unsubscribe"); break;
                        }
                    }
                    subscription?.Dispose();
                    int count = Interlocked.Decrement(ref subscriptionCounter);
                    if (showSubs)
                    {
                        logger($"Dispose Completed! ({count} Active Subscriptions)");
                    }
                });
            }
            finally
            {
                int count = Interlocked.Increment(ref subscriptionCounter);
                if (showSubs)
                {
                    logger($"Subscription Created!  ({count} Active Subscriptions)");
                }
            }
        });
    }

    public static IObservable<IChangeSet<T, TKey>> Spy<T, TKey>(this IObservable<IChangeSet<T, TKey>> source,
                                                                    string? opName = null, Action<string>? logger = null,
                                                                    Func<T, string?>? formatter = null, bool showSubs = true,
                                                                      bool showTimestamps = true)
        where T : notnull
        where TKey : notnull
    {
        formatter = formatter ?? (t => t?.ToString() ?? "{Null}");
        return Spy(source, opName, logger, cs => "[Cache Change Set]" + ChangeSetEntrySpacing + string.Join(ChangeSetEntrySpacing,
            cs.Select((change, n) => $"#{n} [{change.Reason}] {change.Key}: {FormatChange(formatter!, change)}")), showSubs, showTimestamps);
    }

    public static IObservable<IChangeSet<T>> Spy<T>(this IObservable<IChangeSet<T>> source,
                                                                    string? opName = null, Action<string>? logger = null,
                                                                    Func<T, string?>? formatter = null, bool showSubs = true,
                                                                      bool showTimestamps = true)
                                                                      where T : notnull
    {
        formatter = formatter ?? (t => t?.ToString() ?? "{Null}");
        return Spy(source, opName, logger, cs => "[List Change Set]" + ChangeSetEntrySpacing + string.Join(ChangeSetEntrySpacing,
            cs.Select(change => $"[{change.Reason}] {FormatChange(formatter!, change)}")), showSubs, showTimestamps);
    }

    public static IObservable<T> DebugSpy<T>(this IObservable<T> source, string? opName = null,
                                                                  Func<T, string?>? formatter = null, bool showSubs = true,
                                                                  bool showTimestamps = true)
    {
#if DEBUG
        return source.Spy(opName, DebugLogger, formatter, showSubs, showTimestamps);
#else
        return source;
#endif
    }

    public static IObservable<IChangeSet<T, TKey>> DebugSpy<T, TKey>(this IObservable<IChangeSet<T, TKey>> source,
                                                                    string? opName = null,
                                                                    Func<T, string?>? formatter = null, bool showSubs = true,
                                                                      bool showTimestamps = true)
        where T : notnull
        where TKey : notnull
    {
#if DEBUG
        return source.Spy(opName, DebugLogger, formatter, showSubs, showTimestamps);
#else
        return source;
#endif
    }

    public static IObservable<IChangeSet<T>> DebugSpy<T>(this IObservable<IChangeSet<T>> source,
                                                                    string? opName = null,
                                                                    Func<T, string?>? formatter = null, bool showSubs = true,
                                                                      bool showTimestamps = true)
                                                                      where T : notnull
    {
#if DEBUG
        return source.Spy(opName, DebugLogger, formatter, showSubs, showTimestamps);
#else
        return source;
#endif
    }

    private static string FormatChange<T, TKey>(Func<T, string> formatter, Change<T, TKey> change)
        where T : notnull
        where TKey : notnull =>
        change.Reason switch
        {
            ChangeReason.Update => $"{formatter(change.Current)} [Previous: {formatter(change.Previous.Value)}]",
            _ => formatter(change.Current),
        };

    private static string FormatChange<T>(Func<T, string> formatter, Change<T> change)
        where T : notnull =>
        change.Reason switch
        {
            ListChangeReason.AddRange => string.Join(", ", change.Range.Select(n => formatter(n))),
            ListChangeReason.RemoveRange => string.Join(", ", change.Range.Select(n => formatter(n))),
            _ => formatter(change.Item.Current),
        };

    private static Action<string> CreateLogger(Action<string> baseLogger, Func<string> timeStamper, string opName) =>
            msg => baseLogger($"{timeStamper()}[{Thread.CurrentThread.ManagedThreadId:X2}] |{opName}| {msg}");

#if DEBUG
    static void DebugLogger(string str) => Debug.WriteLine(str);
#endif
}
