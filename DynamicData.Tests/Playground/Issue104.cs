using System;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData.Binding;
using FluentAssertions;
using Microsoft.Reactive.Testing;
using Xunit;

namespace DynamicData.Tests.Playground
{
    public class Issue104
    {
        [Fact]
        public void MakeSelectMagicWorkWithObservable()
        {
            var initialItem = new IntHolder() { Value = 1, Description = "Initial Description" };

            var sourceList = new SourceList<IntHolder>();
            sourceList.Add(initialItem);

            var descriptionStream = sourceList
                .Connect()
                .AutoRefresh(intHolder => intHolder.Description)
                .Transform(intHolder => intHolder.Description, true)
                .Do(x => { }) // <--- Add break point here to check the overload fixes it
                .Bind(out ReadOnlyObservableCollection<string> resultCollection);

            using (descriptionStream.Subscribe())
            {
                var newDescription = "New Description";
                initialItem.Description = newDescription;

                newDescription.Should().Be(resultCollection[0]);
                //Assert.AreEqual(newDescription, resultCollection[0]);
            }
        }

        public class IntHolder : AbstractNotifyPropertyChanged
        {
            public int _value;
            public int Value
            {
                get => _value;
                set => this.SetAndRaise(ref _value, value);
            }

            public string _description_;
            public string Description
            {
                get => _description_;
                set => this.SetAndRaise(ref _description_, value);
            }
        }

    }
}
