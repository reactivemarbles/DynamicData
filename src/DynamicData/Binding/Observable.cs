// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;

namespace DynamicData.Binding;

internal static class Observable<T>
{
    public static IObservable<T?> Default { get; } = Observable.Return<T?>(default);

    public static IObservable<T> Empty { get; } = Observable.Empty<T>();

    public static IObservable<T> Never { get; } = Observable.Never<T>();
}
