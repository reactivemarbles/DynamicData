namespace DynamicData.Tests.Domain
{
    public class ParentChild
    {
        public Person Child { get; }
        public Person Parent { get; }

        public ParentChild(Person child, Person parent)
        {
            Child = child;
            Parent = parent;
        }
    }
}