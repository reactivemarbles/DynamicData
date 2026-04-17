// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using DynamicData.Kernel;

namespace DynamicData.Internal;

/// <summary>
/// A lightweight notification struct for delivery queues. Discriminates
/// OnNext, OnError, and OnCompleted using <see cref="Optional{T}.HasValue"/>
/// and the error field, avoiding null discrimination on <c>T?</c> which
/// is broken for value types in generic struct fields on .NET 9.
/// </summary>
internal readonly struct Notification<T>
{
    private readonly Optional<T> _value;
    private readonly Exception? _error;

    private Notification(Optional<T> value, Exception? error)
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
        return new(Optional.None<T>(), error);
    }

    /// <summary>Creates an OnCompleted notification (terminal).</summary>
    public static Notification<T> CreateCompleted() => new(Optional.None<T>(), null);

    /// <summary>Gets whether this is an OnError notification.</summary>
    public bool IsError => _error is not null;

    /// <summary>Gets whether this is a terminal notification (OnError or OnCompleted).</summary>
    public bool IsTerminal => !_value.HasValue;

    /// <summary>Delivers this notification to the specified observer.</summary>
    public void Accept(IObserver<T> observer)
    {
        if (_value.HasValue)
        {
            observer.OnNext(_value.Value);
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
