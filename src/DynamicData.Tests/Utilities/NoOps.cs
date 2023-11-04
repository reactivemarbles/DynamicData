using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace DynamicData.Tests.Utilities;

internal class NoOpComparer<T> : IComparer<T>
{
    public int Compare(T x, T y) => throw new NotImplementedException();
}

internal class NoOpEqualityComparer<T> : IEqualityComparer<T>
{
    public bool Equals(T x, T y) => throw new NotImplementedException();
    public int GetHashCode([DisallowNull] T obj) => throw new NotImplementedException();
}
