using System;

namespace DynamicData.Tests.Utilities;

internal static class RawAnonymousObservable
{
    public static RawAnonymousObservable<T> Create<T>(Func<IObserver<T>, IDisposable> onSubscribe)
        => new(onSubscribe);
}

// Allows bypassing of safeguards implemented within Observable.Create<T>(), for testing.
internal class RawAnonymousObservable<T>
    : IObservable<T>
{
    private readonly Func<IObserver<T>, IDisposable> _onSubscribe;

    public RawAnonymousObservable(Func<IObserver<T>, IDisposable> onSubscribe)
        => _onSubscribe = onSubscribe;

    public IDisposable Subscribe(IObserver<T> observer)
        => _onSubscribe.Invoke(observer);
}
