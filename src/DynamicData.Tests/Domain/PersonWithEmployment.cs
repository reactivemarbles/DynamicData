using System;

namespace DynamicData.Tests.Domain
{
    public class PersonWithEmployment : IDisposable
    {
        private readonly IGroup<PersonEmployment, PersonEmpKey, string> _source;

        public PersonWithEmployment(IGroup<PersonEmployment, PersonEmpKey, string> source)
        {
            _source = source;
            EmploymentData = source.Cache;
        }

        public int EmploymentCount => EmploymentData.Count;

        public IObservableCache<PersonEmployment, PersonEmpKey> EmploymentData { get; }

        public string Person => _source.Key;

        public void Dispose()
        {
            EmploymentData.Dispose();
        }

        public override string ToString()
        {
            return $"Person: {Person}. Count {EmploymentCount}";
        }
    }
}