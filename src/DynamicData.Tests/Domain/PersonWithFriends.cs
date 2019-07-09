using System.Collections.Generic;
using System.Linq;
using DynamicData.Binding;

namespace DynamicData.Tests.Domain
{
    public class PersonWithFriends : AbstractNotifyPropertyChanged, IKey<string>
    {
        private int _age;
        private IEnumerable<PersonWithFriends> _friends;

        public PersonWithFriends(string name, int age)
            : this(name, age, Enumerable.Empty<PersonWithFriends>())
        {
        }

        public PersonWithFriends(string name, int age, IEnumerable<PersonWithFriends> friends)
        {
            Name = name;
            _age = age;
            _friends = friends;
            Key = name;
        }

        public string Name { get; }

        public int Age
        {
            get => _age;
            set => SetAndRaise(ref _age, value);
        }

        public IEnumerable<PersonWithFriends> Friends
        {
            get => _friends;
            set => SetAndRaise(ref _friends, value);
        }

        public override string ToString()
        {
            return $"{Name}. {Age}";
        }

        #region Implementation of IKey<out string>

        /// <summary>
        ///     The key
        /// </summary>
        public string Key { get; }

        #endregion
    }
}
