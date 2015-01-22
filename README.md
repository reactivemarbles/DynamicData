## Dynamic Data

Dynamic data is a portable class library which brings the power of reactive (RX) to collections.

### What is it?

A comprehensive library of reactive extensions, which are used to manage in-memory collections. As the source collection changes the operators receive a changeset which enables them to self-maintain.

### Why use it?

It makes the management of in-memory data easy and is no exageration to say it can save thousands of lines of code.

### Give me some links

- Install from Nuget  https://www.nuget.org/packages/DynamicData
- Sample wpf project https://github.com/RolandPheasant/TradingDemo
- Blog http://dynamicdataproject.wordpress.com
- Feel free to feedback on twitter: [@RolandPheasant](https://twitter.com/RolandPheasant)

### But I want to see some details

First create a source of data:

```csharp
//use  SourceList<T> to compare items on hash code otherwise use SourceCache<TObject,TKey>.
var mySource = new SourceList<Trade>();
```

This example connects to a stream of live trades, creates a proxy for each trade and orders the results by most recent first. As the source is modified the result of ‘myoperation’ will automatically reflect changes.

```csharp
//some code to maintain the source (not shown)
var myoperation = mySource.Connect() 
            .Filter(trade=>trade.Status == TradeStatus.Live) 
            .Transform(trade => new TradeProxy(trade))
            .Sort(SortExpressionComparer<TradeProxy>.Descending(t => t.Timestamp))
            .DisposeMany()
             //more operations...
            .Subscribe(changeSet=>//do something with the result)
```
Oh and I forgot to say, ```TradeProxy``` is disposable and DisposeMany() ensures items are disposed when no longer part of the stream.

This example produces a stream which is grouped by status. If an item's changes status it will be moved to the new group and when a group has no items the group will automatically be removed.
```csharp
var myoperation = mySource.Connect() 
            .Group(trade=>trade.Status) //This is different frm Rx GroupBy
			.Subscribe(changeSet=>//do something with the groups)
```

or you could do something like this which will wire and unwire items from the observable when they are added, updated or removed from the source.
```csharp
var myoperation = mySource.Connect() 
			.MergeMany(trade=> trade.ObservePropertyChanged(t=>t.Amount))
			.Subscribe(ObservableOfAmountChangedForAllItems=>//do something with IObservable<PropChangedArg>)
```

And what's more is this is the tip of the iceberg - there are about 40 operators all bourne from pragmatic experience.

### So what's the magic then?

Simple, any change to a collection can be represented using a change set where the change set is a collection of changed items as follows.

```csharp
	//NB Exact implementation is  simplified 
	public interface IChangeSet<TObject,  TKey> : IEnumerable<Change<TObject, TKey>>
    {
    }

	//and the change is something like this
	public struct Change<TObject, TKey>
	{
		public ChangeReason Reason {get;}
		public TKey Key {get;}
		public TObject Current {get;}
		public Optional<TObjec>t Previous {get;}
	}
```

This structure is observed like this ```IObservableIChangeSet<TObject,  TKey>``` and voila, we can start building operators around this idea.




