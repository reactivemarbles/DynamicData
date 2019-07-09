using System.Collections.Generic;
using System.Linq;

namespace DynamicData.Tests.Domain
{
    public class PersonWithRelations : IKey<string>
    {
        private readonly string _key;
        private readonly string _name;

        public PersonWithRelations(string name, int age)
            : this(name, age, Enumerable.Empty<PersonWithRelations>())
        {
        }

        public PersonWithRelations(string name, int age, IEnumerable<PersonWithRelations> relations)
        {
            _name = name;
            Age = age;
            KeyValue = Name;
            Relations = relations;
            _key = name;
        }

        public string Name => _name;

        public int Age { get; }

        public string KeyValue { get; }

        public IEnumerable<PersonWithRelations> Relations { get; }

        public IEnumerable<Pet> Pet { get; set; }

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
