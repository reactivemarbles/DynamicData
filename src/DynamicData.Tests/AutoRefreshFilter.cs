using System;
using System.ComponentModel;
using FluentAssertions;
using Xunit;

namespace DynamicData.Tests
{
    public class AutoRefreshFilter
    {
        [Fact]
        public void Test()
        {
            var a0 = new Item("A0");
            var i1 = new Item("I1");
            var i2 = new Item("I2");
            var i3 = new Item("I3");

            var obsList = new SourceList<Item>();
            obsList.AddRange(new[] {a0, i1, i2, i3});

            var obsListDerived = obsList.Connect()
                .AutoRefresh(x => x.Name)
                .Filter(x => x.Name.Contains("I"))
                .AsObservableList();

            obsListDerived.Count.Should().Be(3);
            obsListDerived.Items.Should().BeEquivalentTo(new [] {i1, i2, i3});

            i1.Name = "X2";
            obsListDerived.Count.Should().Be(2);
            obsListDerived.Items.Should().BeEquivalentTo(new[] {i2, i3});

            a0.Name = "I0";
            obsListDerived.Count.Should().Be(3);
            obsListDerived.Items.Should().BeEquivalentTo(new[] {a0, i2, i3});
        }
    }

    public class Item : INotifyPropertyChanged
    {
        public Guid Id { get; }

        private string _name;

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            }
        }

        public Item(string name)
        {
            Id = Guid.NewGuid();
            Name = name;
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
