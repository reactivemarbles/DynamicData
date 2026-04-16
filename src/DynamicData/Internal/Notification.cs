// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Internal;

/// <summary>
/// A lightweight 16-byte notification struct for delivery queues. Discriminates
/// OnNext, OnError, and OnCompleted using two reference fields without heap allocation.
/// Value types are boxed into the object field for correct null discrimination.
/// </summary>
internal readonly struct Notification<T>
    where T : notnull
{
    private readonly object? _value;
    private readonly Exception? _error;

    private Notification(object? value, Exception? error)
    {
        _value = value;
        _error = error;
    }

    /// <summary>Creates an OnNext notification.</summary>
    public static Notification<T> CreateNext(T value) => new(value, null);

    /// <summary>Creates an OnError notification (terminal).</summary>
    public static Notification<T> CreateError(Exception error)
    {
        error.ThrowArgumentNullExceptionIfNull(nameof(error));
        return new(null, error);
    }

    /// <summary>Creates an OnCompleted notification (terminal).</summary>
    public static Notification<T> CreateCompleted() => new(null, null);

    /// <summary>Gets whether this is an OnError notification.</summary>
    public bool IsError => _error is not null;

    /// <summary>Gets whether this is a terminal notification.</summary>
    public bool IsTerminal => _value is null;

    /// <summary>Delivers this notification to the specified observer.</summary>
    public void Accept(IObserver<T> observer)
    {
        if (_value is not null)
        {
            observer.OnNext((T)_value);
        }
        else if (_error is not null)
        {
            observer.OnError(_error);
        }
        else
        {
            observer.OnCompleted();
        }
    }
}
