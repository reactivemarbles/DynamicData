// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// Polyfill implementation adapted from SimonCropp/Polyfill (https://github.com/SimonCropp/Polyfill).
#if !NETCOREAPP3_0_OR_GREATER && !NETSTANDARD2_1_OR_GREATER
namespace System.Threading;

/// <summary>Polyfill extensions for <see cref="CancellationToken"/> on frameworks without <c>UnsafeRegister</c>.</summary>
internal static class CancellationTokenPolyfillExtensions
{
    /// <summary>Polyfill registration operations for a cancellation token.</summary>
    /// <param name="token">The cancellation token.</param>
    extension(CancellationToken token)
    {
        /// <summary>Registers a delegate that is invoked when the token is cancelled, without capturing the execution context.</summary>
        /// <param name="callback">The delegate to invoke on cancellation.</param>
        /// <param name="state">The state passed to <paramref name="callback"/>.</param>
        /// <returns>A registration that can be disposed to remove the callback.</returns>
        public CancellationTokenRegistration UnsafeRegister(Action<object?> callback, object? state) =>
            token.Register(callback, state, useSynchronizationContext: false);

        /// <summary>Registers a delegate that is invoked with the triggering token when the token is cancelled, without capturing the execution context.</summary>
        /// <param name="callback">The delegate to invoke on cancellation, receiving the state and the triggering token.</param>
        /// <param name="state">The state passed to <paramref name="callback"/>.</param>
        /// <returns>A registration that can be disposed to remove the callback.</returns>
        public CancellationTokenRegistration UnsafeRegister(Action<object?, CancellationToken> callback, object? state) =>
            token.Register(
                static boxed =>
                {
                    var (inner, innerState, innerToken) = ((Action<object?, CancellationToken> Callback, object? State, CancellationToken Token))boxed!;
                    inner(innerState, innerToken);
                },
                (callback, state, token),
                useSynchronizationContext: false);
    }
}
#endif
