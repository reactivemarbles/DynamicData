// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Kernel;
#else

namespace DynamicData.Kernel;
#endif

/// <summary>
/// Provides members for the ReferenceEqualityComparer class.
/// </summary>
/// <typeparam name="T">The type of the T value.</typeparam>
internal sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T>
{
    /// <summary>
    /// The Instance field.
    /// </summary>
    public static readonly IEqualityComparer<T> Instance = new ReferenceEqualityComparer<T>();

    /// <summary>
    /// Executes the Equals operation.
    /// </summary>
    /// <param name="x">The x value.</param>
    /// <param name="y">The y value.</param>
    /// <returns>The result of the operation.</returns>
    public bool Equals(T? x, T? y) => ReferenceEquals(x, y);

    /// <summary>
    /// Executes the GetHashCode operation.
    /// </summary>
    /// <param name="obj">The obj value.</param>
    /// <returns>The result of the operation.</returns>
    public int GetHashCode(T? obj) => obj is null ? 0 : obj.GetHashCode();
}
