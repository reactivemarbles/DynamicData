using System;
using System.Collections.ObjectModel;
using FluentAssertions;
using Xunit;

namespace DynamicData.Tests.Playground
{
    public class Issue105
    {
        [Fact]
        public void GetAllUpdatesFromAnUnderlyingObservableCollection()
        {
            var drinks = new SourceCache<Drink, Guid>(d => d.Id);

            var collection = drinks.Connect()
                .TransformMany(d => d.Ingredients, i => i.Id)
                .AsObservableCache();

            var drink = new Drink();
            drinks.AddOrUpdate(drink);

            collection.Count.Should().Be(0);

            drink.Ingredients.Add(new Ingredient("Vodka"));
            collection.Count.Should().Be(1);

            drink.Ingredients.Add(new Ingredient("Lime"));
            collection.Count.Should().Be(2);
        }
    }

    internal class Drink
    {
        public Drink()
        {
            Ingredients = new ObservableCollection<Ingredient>();
            Id = Guid.NewGuid();
        }

        public ObservableCollection<Ingredient> Ingredients { get; }
        public Guid Id { get; }
    }

    internal class Ingredient
    {
        public Ingredient(string title)
        {
            Title = title;
            Id = Guid.NewGuid();
        }

        public string Title { get; }
        public Guid Id { get; }
    }
}
