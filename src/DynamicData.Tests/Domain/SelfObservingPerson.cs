using System;
using System.Reactive.Linq;

namespace DynamicData.Tests.Domain;

public class SelfObservingPerson : IDisposable
{
    private readonly IDisposable _cleanUp;

    public SelfObservingPerson(IObservable<Person> observable) => _cleanUp = observable.Finally(() => Completed = true).Subscribe(
            p =>
            {
                Person = p;
                UpdateCount++;
            });

    public bool Completed { get; private set; }

    public Person? Person { get; private set; }

    public int UpdateCount { get; private set; }

    /// <summary>
    ///put here the code to dispose all managed and unmanaged resources
    /// </summary>
    public void Dispose() => _cleanUp.Dispose();
}
