// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace DynamicData.Tests.Utilities;

/// <summary>
/// <see cref="IObserver{T}"/> that records every OnNext value and the terminal state.
/// </summary>
internal sealed class CollectingObserver<T> : IObserver<T>
{
    private readonly List<T> _values = [];

    public IReadOnlyList<T> Values => _values;

    public Exception? Error { get; private set; }

    public bool IsCompleted { get; private set; }

    public void OnNext(T value) => _values.Add(value);

    public void OnError(Exception error) => Error = error;

    public void OnCompleted() => IsCompleted = true;
}
