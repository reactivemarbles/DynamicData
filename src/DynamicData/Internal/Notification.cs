// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Internal;

/// <summary>
/// A lightweight notification struct for delivery queues. Discriminates
/// OnNext, OnError, and OnCompleted without heap allocation.
/// </summary>
internal readonly struct Notification<T>
    where T : notnull
{
    /// <summary>The value for OnNext notifications.</summary>
    public readonly Optional<T> Value;

    /// <summary>The exception for OnError notifications.</summary>
    public readonly Exception? Error;

    private Notification(Optional<T> value, Exception? error)
    {
        Value = value;
        Error = error;
    }

    /// <summary>Creates an OnNext notification.</summary>
    public static Notification<T> Next(T value) => new(value, null);

    /// <summary>Creates an OnError notification (terminal).</summary>
    public static Notification<T> OnError(Exception error)
    {
        error.ThrowArgumentNullExceptionIfNull(nameof(error));
        return new(Optional.None<T>(), error);
    }

    /// <summary>Creates an OnCompleted notification (terminal).</summary>
    public static readonly Notification<T> Completed = new(Optional.None<T>(), null);

    /// <summary>Gets whether this is a terminal notification.</summary>
    public bool IsTerminal => !Value.HasValue;

    /// <summary>Delivers this notification to the specified observer.</summary>
    public void Accept(IObserver<T> observer)
    {
        if (Value.HasValue)
        {
            observer.OnNext(Value.Value);
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
