using System.Collections.Generic;
using System.Linq;

namespace DynamicData.Tests.Domain
{
    public class PersonWithRelations : IKey<string>
    {
        private readonly string _key;
        private readonly string _keyValue;
        private readonly string _name;
        private readonly IEnumerable<PersonWithRelations> _relations;

        public PersonWithRelations(string name, int age)
            : this(name, age, Enumerable.Empty<PersonWithRelations>())
        {
        }

        public PersonWithRelations(string name, int age, IEnumerable<PersonWithRelations> relations)
        {
            _name = name;
            Age = age;
            _keyValue = Name;
            _relations = relations;
            _key = name;
        }


        public string Name
        {
            get { return _name; }
        }

        public int Age { get; set; }

        public string KeyValue
        {
            get { return _keyValue; }
        }


        public IEnumerable<PersonWithRelations> Relations
        {
            get { return _relations; }
        }

        public IEnumerable<Pet> Pet { get; set; }

        public override string ToString()
        {
            return string.Format("{0}. {1}", Name, Age);
        }

        #region Implementation of IKey<out string>

        /// <summary>
        ///     The key
        /// </summary>
        public string Key
        {
            get { return _key; }
        }

        #endregion
    }
}