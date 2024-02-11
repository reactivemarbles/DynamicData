using System;
using System.Linq;
using Bogus;
using DynamicData.Tests.Utilities;

namespace DynamicData.Tests.Domain;

internal static class Fakers
{
    const int MinAnimals = 3;
#if DEBUG
    const int MaxAnimals = 7;
#else
    const int MaxAnimals = 23;
#endif

    private static readonly string[][] AnimalTypeNames =
    [
        // Mammal
        ["Dog", "Cat", "Ferret", "Hamster", "Gerbil", "Cavie", "Mouse", "Pot-Bellied Pig"],

        // Reptile
        ["Corn Snake", "Python", "Gecko", "Skink", "Monitor Lizard", "Chameleon", "Tortoise", "Box Turtle", "Iguana"],

        // Fish
        ["Betta", "Goldfish", "Angelfish", "Catfish", "Guppie", "Mollie", "Neon Tetra", "Platie", "Koi"],

        // Amphibian
        ["Frog", "Toad", "Salamander"],

        // Bird
        ["Parakeet", "Cockatoo", "Parrot", "Finch", "Conure", "Lovebird", "Cockatiel"],
    ];

    private static readonly string[] PersonGenders = ["F", "M", "O"];

    public static Faker<Animal> Animal { get; } =
        new Faker<Animal>()
            .CustomInstantiator(faker =>
            {
                var family = faker.PickRandom<AnimalFamily>();
                var type = faker.PickRandom(AnimalTypeNames[(int)family]);
                var name = $"{faker.Commerce.ProductAdjective()} {faker.Person.FirstName}";

                return new Animal(name, type, family);
            });

    public static Faker<AnimalOwner> AnimalOwner { get; } = new Faker<AnimalOwner>().CustomInstantiator(faker => new AnimalOwner(faker.Person.FullName));

    public static Faker<AnimalOwner> AnimalOwnerWithAnimals { get; } = AnimalOwner.Clone().WithInitialAnimals(Animal);

    public static Faker<Market> Market { get; } = new Faker<Market>().CustomInstantiator(faker => new Market($"{faker.Commerce.ProductName()} Id#{faker.Random.AlphaNumeric(5)}"));

    public static Faker<Person> Person { get; } = new Faker<Person>().CustomInstantiator(faker =>
        new Person(faker.Person.FullName, faker.Random.Int(1, 100), faker.PickRandom(PersonGenders), faker.Person.FirstName)
        {
            FavoriteColor = faker.Random.RandomColor(),
            PetType = faker.PickRandom<AnimalFamily>(),
        });

    public static Faker<AnimalOwner> WithInitialAnimals(this Faker<AnimalOwner> existing, Faker<Animal> animalFaker, int minCount, int maxCount) =>
        existing.FinishWith((faker, owner) => owner.Animals.AddRange(animalFaker.GenerateBetween(minCount, maxCount)));

    public static Faker<AnimalOwner> WithInitialAnimals(this Faker<AnimalOwner> existing, Faker<Animal> animalFaker, int maxCount) =>
        WithInitialAnimals(existing, animalFaker, 0, maxCount);

    public static Faker<AnimalOwner> WithInitialAnimals(this Faker<AnimalOwner> existing, Faker<Animal> animalFaker) =>
        WithInitialAnimals(existing, animalFaker, MinAnimals, MaxAnimals);

    public static AnimalOwner AddAnimals(this AnimalOwner owner, Faker<Animal> animalFaker, int minCount, int maxCount) =>
        owner.With(o => o.Animals.AddRange(animalFaker.GenerateBetween(minCount, maxCount)));

    public static AnimalOwner AddAnimals(this AnimalOwner owner, Faker<Animal> animalFaker, int count) =>
        owner.With(o => o.Animals.AddRange(animalFaker.Generate(count)));

    public static Color RandomColor(this Randomizer rand) =>
        rand.EnumValues(1, Color.NotSpecified)[0];

    public static Color RandomColor(this Randomizer rand, Color current) =>
        rand.EnumValues(1, Color.NotSpecified, current)[0];
}

internal static class FakerExtensions
{
    public static Faker<T> WithSeed<T>(this Faker<T> faker, Randomizer randomizer) where T : class => faker.UseSeed(randomizer.Int());
}
