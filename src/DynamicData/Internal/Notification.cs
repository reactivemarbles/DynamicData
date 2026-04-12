// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Internal;

/// <summary>
/// A lightweight notification struct for delivery queues. Discriminates
/// OnNext, OnError, and OnCompleted without heap allocation.
/// </summary>
internal readonly struct Notification<T>
{
    /// <summary>The value for OnNext notifications.</summary>
    public readonly T? Value;

    /// <summary>The exception for OnError notifications.</summary>
    public readonly Exception? Error;

    /// <summary>True if this is an OnNext notification.</summary>
    public readonly bool HasValue;

    private Notification(T? value, Exception? error, bool hasValue)
    {
        Value = value;
        Error = error;
        HasValue = hasValue;
    }

    /// <summary>Creates an OnNext notification.</summary>
    public static Notification<T> Next(T value) => new(value, null, true);

    /// <summary>Creates an OnError notification (terminal).</summary>
    public static Notification<T> OnError(Exception error) => new(default, error, false);

    /// <summary>Creates an OnCompleted notification (terminal).</summary>
    public static Notification<T> Completed => new(default, null, false);

    /// <summary>Gets whether this is a terminal notification.</summary>
    public bool IsTerminal => !HasValue;

    /// <summary>Delivers this notification to the specified observer.</summary>
    public void Accept(IObserver<T> observer)
    {
        if (HasValue)
        {
            observer.OnNext(Value!);
        }
        else if (Error is not null)
        {
            observer.OnError(Error);
        }
        else
        {
            observer.OnCompleted();
        }
    }
}
