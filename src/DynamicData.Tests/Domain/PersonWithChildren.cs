using System.Collections.Generic;
using System.Linq;

namespace DynamicData.Tests.Domain
{
    public class PersonWithChildren : IKey<string>
    {
        private readonly string _key;
        private readonly string _name;

        public PersonWithChildren(string name, int age)
            : this(name, age, Enumerable.Empty<Person>())
        {
        }

        public PersonWithChildren(string name, int age, IEnumerable<Person> relations)
        {
            _name = name;
            Age = age;
            KeyValue = Name;
            Relations = relations;
            _key = name;
        }

        public string Name => _name;

        public int Age { get; set; }

        public string KeyValue { get; }

        public IEnumerable<Person> Relations { get; }


        public override string ToString()
        {
            return $"{Name}. {Age}";
        }

        #region Implementation of IKey<out string>

        /// <summary>
        ///     The key
        /// </summary>
        public string Key { get { return _key; } }

        #endregion
    }
}