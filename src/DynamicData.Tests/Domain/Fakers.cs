using Bogus;

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

    public static Faker<Animal> Animal { get; } =
        new Faker<Animal>()
            .CustomInstantiator(faker =>
            {
                var family = faker.PickRandom<AnimalFamily>();
                var type = faker.PickRandom(AnimalTypeNames[(int)family]);
                var name = $"{faker.Commerce.ProductAdjective()} the {type}";

                return new Animal(name, type, family);
            });

    public static Faker<AnimalOwner> AnimalOwner { get; } =
        new Faker<AnimalOwner>()
            .CustomInstantiator(faker =>
            {
                var result = new AnimalOwner(faker.Person.FullName);

                result.Animals.AddRange(Animal.Generate(faker.Random.Number(MinAnimals, MaxAnimals)));

                return result;
            });
}
