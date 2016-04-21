using System;
using DynamicData.Kernel;

namespace DynamicData.Tests.Domain
{
    public class PersonWithEmployment : IDisposable
    {
        private readonly IObservableCache<PersonEmployment, PersonEmpKey> _empData;
        private readonly IGroup<PersonEmployment, PersonEmpKey, string> _source;

        public PersonWithEmployment(IGroup<PersonEmployment, PersonEmpKey, string> source)
        {
            _source = source;
            _empData = source.Cache;
        }

        public string Person { get { return _source.Key; } }

        public IObservableCache<PersonEmployment, PersonEmpKey> EmpoymentData { get { return _empData; } }

        public int EmployementCount { get { return _empData.Count; } }

        public void Dispose()
        {
            _empData.Dispose();
        }

        public override string ToString()
        {
            return string.Format("Person: {0}. Count {1}", Person, EmployementCount);
        }
    }
}
