// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Kernel;

internal sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T>
{
    public static readonly IEqualityComparer<T> Instance = new ReferenceEqualityComparer<T>();

    public bool Equals(T? x, T? y) => ReferenceEquals(x, y);

    public int GetHashCode(T? obj) => obj is null ? 0 : obj.GetHashCode();
}
