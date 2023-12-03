using System;

namespace DynamicData.Tests.Domain;

public class PersonWithEmployment(IGroup<PersonEmployment, PersonEmpKey, string> source) : IDisposable
{
    private readonly IGroup<PersonEmployment, PersonEmpKey, string> _source = source;

    public int EmploymentCount => EmploymentData.Count;

    public IObservableCache<PersonEmployment, PersonEmpKey> EmploymentData { get; } = source?.Cache!;

    public string Person => _source.Key;

    public void Dispose() => EmploymentData.Dispose();

    public override string ToString() => $"Person: {Person}. Count {EmploymentCount}";
}
