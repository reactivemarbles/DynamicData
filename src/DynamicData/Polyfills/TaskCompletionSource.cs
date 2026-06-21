// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// Polyfill implementation adapted from SimonCropp/Polyfill (https://github.com/SimonCropp/Polyfill).
#if !NET5_0_OR_GREATER

namespace System.Threading.Tasks;

/// <summary>Polyfill for the non-generic <see cref="TaskCompletionSource"/> introduced in .NET 5, backed by a <see cref="TaskCompletionSource{TResult}"/>.</summary>
[SuppressMessage("Performance", "CA1812", Justification = "Broadcast polyfill; not instantiated in every consuming leaf.")]
internal sealed class TaskCompletionSource
{
    /// <summary>The underlying generic completion source that backs this non-generic facade.</summary>
    private readonly TaskCompletionSource<bool> _inner;

    /// <summary>Initializes a new instance of the <see cref="TaskCompletionSource"/> class.</summary>
    public TaskCompletionSource() => _inner = new();

    /// <summary>Transitions the underlying task to the <see cref="TaskStatus.RanToCompletion"/> state.</summary>
    public void SetResult() => _inner.SetResult(true);

    /// <summary>Attempts to transition the underlying task to the <see cref="TaskStatus.RanToCompletion"/> state.</summary>
    /// <returns><see langword="true"/> if the operation was successful; otherwise <see langword="false"/>.</returns>
    public bool TrySetResult() => _inner.TrySetResult(true);

    /// <summary>Transitions the underlying task to the <see cref="TaskStatus.Faulted"/> state with the specified exception.</summary>
    /// <param name="exception">The exception to bind to the task.</param>
    public void SetException(Exception exception) => _inner.SetException(exception);

    /// <summary>Attempts to transition the underlying task to the <see cref="TaskStatus.Faulted"/> state with the specified exception.</summary>
    /// <param name="exception">The exception to bind to the task.</param>
    /// <returns><see langword="true"/> if the operation was successful; otherwise <see langword="false"/>.</returns>
    public bool TrySetException(Exception exception) => _inner.TrySetException(exception);

    /// <summary>Transitions the underlying task to the <see cref="TaskStatus.Canceled"/> state.</summary>
    public void SetCanceled() => _inner.TrySetCanceled();

    /// <summary>Attempts to transition the underlying task to the <see cref="TaskStatus.Canceled"/> state.</summary>
    /// <returns><see langword="true"/> if the operation was successful; otherwise <see langword="false"/>.</returns>
    public bool TrySetCanceled() => _inner.TrySetCanceled();

    /// <summary>Attempts to transition the underlying task to the <see cref="TaskStatus.Canceled"/> state for the specified token.</summary>
    /// <param name="cancellationToken">The token associated with the cancellation.</param>
    /// <returns><see langword="true"/> if the operation was successful; otherwise <see langword="false"/>.</returns>
    public bool TrySetCanceled(CancellationToken cancellationToken) => _inner.TrySetCanceled(cancellationToken);
}
#endif
