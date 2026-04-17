// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Internal;

/// <summary>
/// A lightweight 16-byte notification struct for delivery queues. Discriminates
/// OnNext, OnError, and OnCompleted using null checks on two fields without heap allocation.
/// </summary>
internal readonly struct Notification<T>
{
    private readonly T? _value;
    private readonly Exception? _error;

    private Notification(T? value, Exception? error)
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
        return new(default, error);
    }

    /// <summary>Creates an OnCompleted notification (terminal).</summary>
    public static Notification<T> CreateCompleted() => new(default, null);

    /// <summary>Gets whether this is an OnError notification.</summary>
    public bool IsError => _error is not null;

    /// <summary>Gets whether this is a terminal notification.</summary>
    public bool IsTerminal => _value is null;

    /// <summary>Delivers this notification to the specified observer.</summary>
    public void Accept(IObserver<T> observer)
    {
        if (_value is not null)
        {
            observer.OnNext(_value);
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
