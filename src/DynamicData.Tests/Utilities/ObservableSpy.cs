using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Xunit.Abstractions;

namespace DynamicData.Tests.Utilities;

internal static class ObservableSpy
{
    private static readonly string ChangeSetEntrySpacing = Environment.NewLine + "\t";

    private static class NativeMethods
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "OutputDebugStringW")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "SYSLIB1054:Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time", Justification = "Affects operation")]
        public static extern void OutputDebugString(string lpOutputString);
    }

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
    public static IObservable<T> Spy<T>(this IObservable<T> source, string? infoText = null, Action<string>? logger = null, Func<T, string>? formatter = null, bool showSubs = true, bool showTimestamps = true)
    {
        static string NoTimestamp() => string.Empty;
        static string HighResTimestamp() => DateTimeOffset.UtcNow.ToString("HH:mm:ss.fffffff") + " ";

        var activeSubscriptionCounter = 0;
        var subscriptionCounter = 0;

        formatter ??= (t => t?.ToString() ?? "{Null}");
        logger = CreateLogger(logger ?? Console.WriteLine, showTimestamps ? HighResTimestamp : NoTimestamp, infoText ?? $"IObservable<{typeof(T).Name}>");

        var subLogger = showSubs ? logger : null;

        logger("Creating Observable");
        return Observable.Create<T>(observer =>
        {
            var subId = Interlocked.Increment(ref subscriptionCounter);
            var valueCounter = 0;
            bool? completedSuccessfully = null;

            subLogger?.Invoke($"Creating Subscription #{subId}");
            try
            {
                var subscription = source
                    .Do(x => logger($"OnNext() [SubId:{subId}] (#{Interlocked.Increment(ref valueCounter)}): {formatter(x)}"),
                        ex => { logger($"OnError() [SubId:{subId}] ({valueCounter} Values) [Exception: {ex}]"); completedSuccessfully = false; },
                        () => { logger($"OnCompleted() [SubId:{subId}] ({valueCounter} Values)"); completedSuccessfully = true; })
                    .Subscribe(t =>
                    {
                        try
                        {
                            observer.OnNext(t);
                        }
                        catch (Exception ex)
                        {
                            logger($"Downstream Exception [SubId:{subId}] ({ex})");
                            throw;
                        }
                    }, observer.OnError, observer.OnCompleted);

                return Disposable.Create(() =>
                {
                    if (subLogger != null)
                    {
                        switch (completedSuccessfully)
                        {
                            case true: subLogger.Invoke($"Disposing SubId #{subId} due to OnComplete"); break;
                            case false: subLogger.Invoke($"Disposing SubId #{subId} due to OnError"); break;
                            case null: subLogger.Invoke($"Disposing SubId #{subId} due to Unsubscribe"); break;
                        }
                    }
                    subscription?.Dispose();
                    var count = Interlocked.Decrement(ref activeSubscriptionCounter);
                    subLogger?.Invoke($"Dispose Completed! ({count} Active Subscriptions)");
                });
            }
            finally
            {
                var count = Interlocked.Increment(ref activeSubscriptionCounter);
                subLogger?.Invoke($"Subscription Id #{subId} Created!  ({count} Active Subscriptions)");
            }
        });
    }

    public static IObservable<IChangeSet<T, TKey>> Spy<T, TKey>(this IObservable<IChangeSet<T, TKey>> source, string? opName = null, Action<string>? logger = null, Func<T, string>? formatter = null, bool showSubs = true, bool showTimestamps = true)
        where T : notnull
        where TKey : notnull
    {
        formatter ??= (t => t?.ToString() ?? "{Null}");
        return Spy(source, opName, logger, CreateCacheChangeSetFormatter<T, TKey>(formatter!), showSubs, showTimestamps);
    }

    public static IObservable<IChangeSet<T>> Spy<T>(this IObservable<IChangeSet<T>> source, string? opName = null, Action<string>? logger = null, Func<T, string>? formatter = null, bool showSubs = true, bool showTimestamps = true)
        where T : notnull
    {
        formatter ??= (t => t?.ToString() ?? "{Null}");
        return Spy(source, opName, logger, CreateListChangeSetFormatter(formatter!), showSubs, showTimestamps);
    }

    private static Func<IChangeSet<T, TKey>, string> CreateCacheChangeSetFormatter<T, TKey>(Func<T, string> formatter)
        where T : notnull
        where TKey : notnull =>
        cs => "[Cache Change Set]" + ChangeSetEntrySpacing + string.Join(ChangeSetEntrySpacing, cs.Select((change, n) => $"#{n} {FormatChange(formatter, change)}"));

    private static Func<IChangeSet<T>, string> CreateListChangeSetFormatter<T>(Func<T, string> formatter)
        where T : notnull =>
        cs => "[List Change Set]" + ChangeSetEntrySpacing + string.Join(ChangeSetEntrySpacing, cs.Select((change, n) => $"#{n} {FormatChange(formatter, change)}"));

    public static IObservable<T> TestSpy<T>(this IObservable<T> source, ITestOutputHelper testOutputHelper, string? opName = null, Func<T, string>? formatter = null, bool showSubs = true, bool showTimestamps = true) =>
        source.Spy(opName, TestLogger(testOutputHelper), formatter, showSubs, showTimestamps);

    public static IObservable<IChangeSet<T, TKey>> TestSpy<T, TKey>(this IObservable<IChangeSet<T, TKey>> source, ITestOutputHelper testOutputHelper, string? opName = null, Func<T, string>? formatter = null, bool showSubs = true, bool showTimestamps = true)
        where T : notnull
        where TKey : notnull =>
        source.Spy(opName, TestLogger(testOutputHelper), formatter, showSubs, showTimestamps);

    public static IObservable<IChangeSet<T>> TestSpy<T>(this IObservable<IChangeSet<T>> source, ITestOutputHelper testOutputHelper, string? opName = null, Func<T, string>? formatter = null, bool showSubs = true, bool showTimestamps = true)
        where T : notnull =>
        source.Spy(opName, TestLogger(testOutputHelper), formatter, showSubs, showTimestamps);

    public static IObservable<T> DebugSpy<T>(this IObservable<T> source, string? opName = null, Func<T, string>? formatter = null, bool showSubs = true, bool showTimestamps = true) =>
 #if DEBUG || DEBUG_SPY_ALWAYS
        source.Spy(opName, DebugLogger, formatter, showSubs, showTimestamps);
#else
        source;
#endif

    public static IObservable<IChangeSet<T, TKey>> DebugSpy<T, TKey>(this IObservable<IChangeSet<T, TKey>> source, string? opName = null, Func<T, string>? formatter = null, bool showSubs = true, bool showTimestamps = true)
        where T : notnull
        where TKey : notnull =>
#if DEBUG || DEBUG_SPY_ALWAYS
        source.Spy(opName, DebugLogger, formatter, showSubs, showTimestamps);
#else
        source;
#endif

    public static IObservable<IChangeSet<T>> DebugSpy<T>(this IObservable<IChangeSet<T>> source, string? opName = null, Func<T, string>? formatter = null, bool showSubs = true, bool showTimestamps = true)
        where T : notnull =>
#if DEBUG || DEBUG_SPY_ALWAYS
        source.Spy(opName, DebugLogger, formatter, showSubs, showTimestamps);
#else
        source;
#endif

    private static string FormatChange<T, TKey>(Func<T, string> formatter, Change<T, TKey> change)
        where T : notnull
        where TKey : notnull =>
        $"[{change.Reason}] " +
            change.Reason switch
            {
                ChangeReason.Update => $"{formatter(change.Current)} [Previous: {formatter(change.Previous.Value)}]",
                _ => formatter(change.Current),
            };

    private static string FormatChange<T>(Func<T, string> formatter, Change<T> change)
        where T : notnull =>
        $"[{change.Reason}] " +
            change.Reason switch
            {
                ListChangeReason.AddRange => FormatRangeChange(formatter, change.Range),
                ListChangeReason.RemoveRange => FormatRangeChange(formatter, change.Range),
                _ => FormatItemChange(formatter, change.Item),
            };

    private static string FormatRangeChange<T>(Func<T, string> formatter, RangeChange<T> range) where T : notnull =>
        $"({range.Count} Values) "
            + (range.Index == -1 ? string.Empty : $"[Index: {range.Index}] ")
            + string.Join(", ", range.Select(n => formatter(n)));

    private static string FormatItemChange<T>(Func<T, string> formatter, ItemChange<T> item) where T : notnull =>
        formatter(item.Current)
            + ((item.CurrentIndex, item.PreviousIndex) switch
            {
                (-1, -1) => string.Empty,
                (int i, int j) when i == j || (j == -1) => $" [Index: {i}]",
                (int i, int j) => $" [Index: {i}, Prev: {j}]",
            })
            + (item.Previous switch
            {
                { HasValue: true, Value: T val } => $" [Previous: {formatter(val)}]",
                _ => string.Empty,
            });

    private static Action<string> CreateLogger(Action<string> baseLogger, Func<string> timeStamper, string opName) =>
            msg => baseLogger($"{timeStamper()}[{Environment.CurrentManagedThreadId:X2}] |{opName}| {msg}");

#if DEBUG || DEBUG_SPY_ALWAYS
    private static Action<string> TestLogger(ITestOutputHelper testOutputHelper) => str =>
    {
        testOutputHelper.WriteLine(str);
        DebugLogger(str);
    };
#else
    private static Action<string> TestLogger(ITestOutputHelper testOutputHelper) => testOutputHelper.WriteLine;
#endif

#if DEBUG
    private static void DebugLogger(string str) => System.Diagnostics.Debug.WriteLine(str); 
#elif DEBUG_SPY_ALWAYS
    private static void DebugLogger(string str) => NativeMethods.OutputDebugString(str); 
#endif

}
