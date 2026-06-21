// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// Polyfill implementation adapted from SimonCropp/Polyfill (https://github.com/SimonCropp/Polyfill).
#if !NET8_0_OR_GREATER
namespace System.Threading;

/// <summary>Polyfill extensions for <see cref="CancellationTokenSource"/> on frameworks without the .NET 8 async cancellation API.</summary>
internal static class CancellationTokenSourcePolyfillExtensions
{
    /// <summary>Polyfill operations for a cancellation token source.</summary>
    /// <param name="source">The cancellation token source.</param>
    extension(CancellationTokenSource source)
    {
        /// <summary>Communicates a request for cancellation, completing synchronously (no asynchronous callback draining on this framework).</summary>
        /// <returns>A completed task representing the cancellation request.</returns>
        public Task CancelAsync()
        {
            source.Cancel();
            return Task.CompletedTask;
        }
    }
}
#endif
