## Dynamic Data
Dynamic data is a portable class library which brings the power of reactive (rx) to collections.  

A collection which mutates can have adds, updates and removes (plus moves and re-evaluates). Out of the box rx does nothing to manage any changes in a collection which is why Dynamic Data exists.  In Dynamic Data collection changes are notified via an observable change set which is the heart of the system.  An operator receives these notifications and then applies some logic and subsequently provides it's own notifications. In this way operators can be chained together to apply powerful and often very complicated operations with some very simple fluent code.

The benefit of at least 40 operators which are borne from pragmatic experience is that the management of in-memory data becomes easy and it is no exaggeration to say it can save thousands of lines of code by abstracting complicated and often repetitive operations.

### Why is the first Nuget release version 3
Even before rx existed I had implemented a similar concept using old fashioned events but the code was very ugly and my implementation full of race conditions so it never existed outside of my own private sphere. My second attempt was a similar implementation to the first but using rx. This also failed as my understanding of rx was flawed and limited and my design forced consumers to implement interfaces. Then finally I got my design head on and in 2011-ish I started writing what has become dynamic data.  All along I meant to open source it but having so utterly failed on my first 2 attempts I decided to wait. The wait lasted longer than I expected and end up taking over 2 years but the beauty is the it has been trialled for 2 years on a very busy hight volume low latency trading system. And what's more that system has gathered a load of attention for how slick and cool and reliable it is both from the user and IT point of view. So I have released it all with my tail held hight and I hope It can make your life easier like it has done for me.

### I've seen it before so give me some links
- Install from Nuget  https://www.nuget.org/packages/DynamicData
- Sample wpf project https://github.com/RolandPheasant/TradingDemo
- Blog http://dynamicdataproject.wordpress.com
- Feel free to feedback on twitter: [@RolandPheasant](https://twitter.com/RolandPheasant)

### Getting Started

As stated in the blurb, dynamic data is based on the concept of an observable change set.  The easiest way to create one is directly from an observable.
```csharp
IObservable<T> myObservable;
IObservable<IEnumerable<T>> myObservable;
//1. This option will create a collection where item's are identified using the hash code.
var mydynamicdatasource = myObservable.ToObservableChangeSet();
//2. Or specify a key like this
var mydynamicdatasource = myObservable.ToObservableChangeSet(t=> key);
```
The problem with the above is the collection will grow forever so there are overloads to specify size limitation or expiry times (not shown). 

To have much more control over the root collection then we need a local data store which has the requisite crud methods. Like the above the cache can be created with or without specifying a key
```csharp
var mycache  = new SourceCache<TObject,TKey>(t => key);
//or to use a hash key for identity
var mycache  = new SourceCache<TObject>();
```
One final out of the box means of creating an observable change set is if you are doing UI work and have an observable collection, you can do this
```csharp
var myobservablecollection= new ObservableCollection<T>();
//1. This option will create a collection where item's are identified using the hash code.
var mydynamicdatasource = myobservablecollection.ToObservableChangeSet();
//2. Or specify a key like this
var mydynamicdatasource = myobservablecollection.ToObservableChangeSet(t=> key);
```
### Now lets the games begin

Phew, got the boring stuff out of the way with so a few quick fire examples based on the assumption that we already have an observable change set. In all of these examples the resulting sequences always exactly reflect the items is the cache i.e. adds, updates and removes are always propagated.

**Example 1:** connect to a stream of live trades, creates a proxy for each trade and orders the results by most recent first. As the source is modified the result of ‘myoperation’ will automatically reflect changes.

```csharp
var myoperation = mySource
            .Filter(trade=>trade.Status == TradeStatus.Live) 
            .Transform(trade => new TradeProxy(trade))
            .Sort(SortExpressionComparer<TradeProxy>.Descending(t => t.Timestamp))
            .DisposeMany()
             //more operations...
            .Subscribe(changeSet=>//do something with the result)
```
Oh and I forgot to say, ```TradeProxy``` is disposable and DisposeMany() ensures items are disposed when no longer part of the stream.

**Example 2:** produces a stream which is grouped by status. If an item's changes status it will be moved to the new group and when a group has no items the group will automatically be removed.
```csharp
var myoperation = mySource
            .Group(trade=>trade.Status) //This is NOT Rx's GroupBy 
			.Subscribe(changeSet=>//do something with the groups)
```
**Example 3:** Suppose I am editing some trades and I have an observable on each trades which validates but I want to know when all items are valid then this will do the job.
```csharp
IObservable<True> allValid = mySource
                .TrueForAll(o => o.IsValidObservable, (trade, isvalid) => isvalid)
```
This bad boy flattens the observables for each item and self maintains as items are amended in the cache, and returns the combined state in one line of code. I love it.

**Example 4:**  will wire and un-wire items from the observable when they are added, updated or removed from the source.
```csharp
var myoperation = mySource.Connect() 
			.MergeMany(trade=> trade.ObservePropertyChanged(t=>t.Amount))
			.Subscribe(ObservableOfAmountChangedForAllItems=>//do something with IObservable<PropChangedArg>)
```
### Want to know more?
I could go on endlessly but this is not the place for full documentation.  I promise this will come but for now I suggest downloading my WPF sample app (links above)  as I intend it to be a 'living document' and I promise it will be continually maintained. 

Also if you following me on Twitter you will find out when new samples or blog posts have been updated

### Before you sign off, tell me a little more about the changeset?

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




