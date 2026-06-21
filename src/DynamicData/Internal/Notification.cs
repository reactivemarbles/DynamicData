// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Internal;
#else

namespace DynamicData.Internal;
#endif

/// <summary>
/// A lightweight notification struct for delivery queues. Discriminates
/// OnNext, OnError, and OnCompleted using <c>Optional&lt;T&gt;.HasValue</c>
/// and the error field, avoiding null discrimination on <c>T?</c> which
/// is broken for value types in generic struct fields on .NET 9.
/// </summary>
/// <typeparam name="T">The type of the T value.</typeparam>
internal readonly struct Notification<T>
    where T : notnull
{
    /// <summary>
    /// The _value field.
    /// </summary>
    private readonly ReactiveUI.Primitives.Optional<T> _value;

    /// <summary>
    /// The _error field.
    /// </summary>
    private readonly Exception? _error;

    /// <summary>
    /// Initializes a new instance of the <see cref="Notification{T}"/> struct.
    /// </summary>
    /// <param name="value">The value value.</param>
    /// <param name="error">The error value.</param>
    private Notification(ReactiveUI.Primitives.Optional<T> value, Exception? error)
    {
        _value = value;
        _error = error;
    }

    /// <summary>Creates an OnNext notification.</summary>
    /// <param name="value">The value value.</param>
    /// <returns>The result of the operation.</returns>
    public static Notification<T> CreateNext(T value) => new(value, null);

    /// <summary>Creates an OnError notification (terminal).</summary>
    /// <param name="error">The error value.</param>
    /// <returns>The result of the operation.</returns>
    public static Notification<T> CreateError(Exception error)
    {
        ArgumentExceptionHelper.ThrowIfNull(error);
        return new(ReactiveUI.Primitives.Optional<T>.None, error);
    }

    /// <summary>Creates an OnCompleted notification (terminal).</summary>
    /// <returns>The result of the operation.</returns>
    public static Notification<T> CreateCompleted() => new(ReactiveUI.Primitives.Optional<T>.None, null);

    /// <summary>Gets whether this is an OnError notification.</summary>
    public bool IsError => _error is not null;

    /// <summary>Gets whether this is a terminal notification (OnError or OnCompleted).</summary>
    public bool IsTerminal => !_value.HasValue;

    /// <summary>Delivers this notification to the specified observer.</summary>
    /// <param name="observer">The observer value.</param>
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
