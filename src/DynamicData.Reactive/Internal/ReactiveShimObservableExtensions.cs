// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Reactive.Internal;

/// <summary>
/// Temporary shim to provide SubscribeSafe extension methods for IObservable{T} when using the REACTIVE_SHIM compilation symbol. This is a workaround for the absence of SubscribeSafe in the Reactive Extensions (Primitives) library as of V5.5.0.
/// </summary>
internal static class ReactiveShimObservableExtensions
{
    public static IDisposable SubscribeSafe<T>(this IObservable<T> source, Action<T> onNext, Action<Exception> onError) =>
        source.Subscribe(onNext, onError);

    public static IDisposable SubscribeSafe<T>(this IObservable<T> source, Action<T> onNext, Action<Exception> onError, Action onCompleted) =>
        source.Subscribe(onNext, onError, onCompleted);

    public static IDisposable SubscribeSafe<T>(this IObservable<T> source, Action<Exception> onError, Action onCompleted) =>
        source.Subscribe(static _ => { }, onError, onCompleted);
}
