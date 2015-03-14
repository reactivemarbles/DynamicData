namespace DynamicData.Kernel
{
	interface ISupportsCapcity
	{
		int Capacity { get; set; }
		int Count { get;  }
	}

	interface IChangeSet
	{
		int Capacity { get; set; }
		int Count { get; }
	}
}