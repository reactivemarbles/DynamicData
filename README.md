## Dynamic Data
Dynamic data is a portable class library which brings the power of reactive (rx) to collections.  

A collection which mutates can have adds, updates and removes (plus moves and re-evaluates). Out of the box Rx does nothing to manage any changes in a collection. In Dynamic Data collection changes are notified via an observable change set which is the heart of the system.  An operator receives these notifications and then applies some logic and subsequently provides it's own notifications. In this way operators can be chained together to apply powerful and often very complicated operations with some very simple fluent code.

The benefit of at least 40 operators which are borne from pragmatic experience is that the management of in-memory data becomes easy and it is no exaggeration to say it can save thousands of lines of code by abstracting complicated and often repetitive operations.

### Why is the first Nuget release version 3
Even before rx existed I had implemented a similar concept using old fashioned events but the code was very ugly and my implementation full of race conditions so it never existed outside of my own private sphere. My second attempt was a similar implementation to the first but using rx. This also failed as my understanding of rx was flawed and limited and my design forced consumers to implement interfaces. Then finally I got my design head on and in 2011-ish I started writing what has become dynamic data.  All along I meant to open source it but having so utterly failed on my first 2 attempts I decided to wait 

### I've seen it before so give me some links
- Install from Nuget  https://www.nuget.org/packages/DynamicData
- Sample wpf project https://github.com/RolandPheasant/TradingDemo
- Blog http://dynamicdataproject.wordpress.com
- Feel free to feedback on twitter: [@RolandPheasant](https://twitter.com/RolandPheasant)

### Getting Started

First create a source of data:

```csharp
//use  SourceList<T> to compare items on hash code otherwise use SourceCache<TObject,TKey>.
var mySource = new SourceList<Trade>();

//some code to maintain the source (not shown)
```

This example connects to a stream of live trades, creates a proxy for each trade and orders the results by most recent first. As the source is modified the result of ‘myoperation’ will automatically reflect changes.

```csharp
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

This structure is observed like this ```IObservable<IChangeSet<TObject,  TKey>>``` and voila, we can start building operators around this idea.




