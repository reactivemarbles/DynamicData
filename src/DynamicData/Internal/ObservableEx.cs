﻿// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive;

namespace DynamicData.Internal;

internal static class ObservableEx
{
    public static IDisposable SubscribeSafe<T>(this IObservable<T> observable, Action<T> onNext, Action<Exception> onError, Action onComplete) =>
        observable.SubscribeSafe(Observer.Create(onNext, onError, onComplete));
}
