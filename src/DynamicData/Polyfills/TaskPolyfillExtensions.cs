// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// Polyfill implementation adapted from SimonCropp/Polyfill (https://github.com/SimonCropp/Polyfill).
#if !NET6_0_OR_GREATER
namespace System.Threading.Tasks;

/// <summary>Polyfill extensions for <see cref="Task"/> on frameworks without the .NET 6 <c>WaitAsync</c> overloads.</summary>
internal static class TaskPolyfillExtensions
{
    /// <summary>Polyfill awaiting operations for a task.</summary>
    /// <param name="task">The task to await.</param>
    extension(Task task)
    {
        /// <summary>Gets a task that completes with <paramref name="task"/>, or faults when the timeout elapses or the token is cancelled.</summary>
        /// <param name="timeout">The timeout after which the returned task faults, or <see cref="Timeout.InfiniteTimeSpan"/> for no timeout.</param>
        /// <param name="cancellationToken">A token that cancels the wait.</param>
        /// <returns>A task that mirrors <paramref name="task"/> subject to the timeout and cancellation.</returns>
        public async Task WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            await WaitForCompletionAsync(task, timeout, cancellationToken).ConfigureAwait(false);
            await task.ConfigureAwait(false);
        }
    }

    /// <summary>Polyfill awaiting operations for a task that produces a result.</summary>
    /// <param name="task">The task to await.</param>
    /// <typeparam name="T">The task result type.</typeparam>
    extension<T>(Task<T> task)
    {
        /// <summary>Gets a task that completes with the same result as <paramref name="task"/>, or faults when the timeout elapses or the token is cancelled.</summary>
        /// <param name="timeout">The timeout after which the returned task faults, or <see cref="Timeout.InfiniteTimeSpan"/> for no timeout.</param>
        /// <param name="cancellationToken">A token that cancels the wait.</param>
        /// <returns>A task that mirrors <paramref name="task"/> subject to the timeout and cancellation.</returns>
        public async Task<T> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            await WaitForCompletionAsync(task, timeout, cancellationToken).ConfigureAwait(false);
            return await task.ConfigureAwait(false);
        }
    }

    /// <summary>Waits for the task to complete, timeout, or cancellation.</summary>
    /// <param name="task">The task to observe.</param>
    /// <param name="timeout">The timeout after which the wait fails, or <see cref="Timeout.InfiniteTimeSpan"/> for no timeout.</param>
    /// <param name="cancellationToken">A token that cancels the wait.</param>
    /// <returns>A task that completes when the wait condition is resolved.</returns>
    private static async Task WaitForCompletionAsync(Task task, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (task.IsCompleted)
        {
            return;
        }

        TaskCompletionSource<bool> signal = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeout != Timeout.InfiniteTimeSpan)
        {
            linked.CancelAfter(timeout);
        }

        using (linked.Token.Register(static state => ((TaskCompletionSource<bool>)state!).TrySetResult(true), signal))
        {
            var completed = await Task.WhenAny(task, signal.Task).ConfigureAwait(false);
            if (completed != task)
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw new TimeoutException();
            }
        }
    }
}
#endif
