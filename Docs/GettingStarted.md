# Getting Started
 
##The core concept
It is perhaps easiest to think of dynamic data as reactive extensions (rx) for collections but more accurately dynamic data is a bunch of rx operators based on the concept of an observable change set.  The change set notifies listeners of any changes to an underlying source and has the following signature.
```csharp
IObservable<IChangeSet<TObject,TKey>> myFirstObservableChangeSet;
```
where ```IChangeSet<TObject,TKey>```  represents a set of adds, updates, removes and moves (for sort dependent operators).  A further concept introduced by dynamic data is an evaluate change. This is used to tell listeners when an object has mutable values and there has been an in-line change.  An example could be when an object has time dependent values or changeable meta data.

The only constraint of dynamic data is an object needs to have a key specified. This was a design choice right from the beginning as the internals of dynamic data need to identify any object and be able to look it up quickly and efficiently.

## Creating an observable change set
To open up the world of dynamic data to any object, we need to feed the data into some mechanism which produces the observable change set.  Unless you are creating a custom operator then there is no need to directly create one as there are several out of the box means of doing so.

The easiest way is to feed directly into dynamic data from an standard rx observable.
```csharp
IObservable<T> myObservable;
IObservable<IEnumerable<T>> myObservable;
// Use the hashcode for the key
var mydynamicdatasource = myObservable.ToObservableChangeSet();
// or specify a key like this
var mydynamicdatasource = myObservable.ToObservableChangeSet(t=> t.key);
```
The problem with the above is the collection will grow forever so there are overloads to specify size limitation or expiry times (not shown). 

To have much more control over the root collection then we need an in-memory data store which has the requisite add, update and remove methods. Like the above the cache can be created with or without specifying a key
```csharp
// Use the hash code for the key
var mycache  = new SourceCache<TObject>();
// or specify a key like this
var mycache  = new SourceCache<TObject,TKey>(t => t.Key);
```
The cache produces an observable change set via it's connect methods.
```csharp
var oberverableChangeSet = mycache.Connect();
```
Another way is to directly from an observable collection, you can do this
```csharp
var myobservablecollection= new ObservableCollection<T>();
// Use the hashcode for the key
var mydynamicdatasource = myobservablecollection.ToObservableChangeSet();
// or specify a key like this
var mydynamicdatasource = myobservablecollection.ToObservableChangeSet(t => t.Key);
```
This method is only recommended for simple queries which act only on the UI thread as ```ObservableCollection``` is not thread safe.

One other point worth making here is any steam can be covered to as cache.
```csharp
var mycache = somedynamicdatasource.AsObservableCache();
```
This cache has the same connection methods as a source cache but is read only.

## Examples

Now you know how to create the source observable, here are some few quick fire examples. But first, what is the expected behaviour or any standard conventions?  Simple answer to that one.

 1. All operators must comply with the Rx guidelines.
 2. When an observer subscribes the initial items of the underlying source always form the first batch of changes.
 3. Empty change sets should never be fired.

In all of these examples the resulting sequences always exactly reflect the items is the cache.  This is where the power of  add, update and removes comes into it's own as all the operations are maintained with no consumer based plumbing.

**Example 1:** filters a stream of live trades, creates a proxy for each trade and orders the result by most recent first. As the source is modified the observable collection will automatically reflect changes.

```csharp
//Dynamic data has it's own take on an observable collection (optimised for populating from dynamic data observables)
var list = new ObservableCollectionExtended<TradeProxy>();
var myoperation = somedynamicdatasource
					.Filter(trade=>trade.Status == TradeStatus.Live) 
					.Transform(trade => new TradeProxy(trade))
					.Sort(SortExpressionComparer<TradeProxy>.Descending(t => t.Timestamp))
					.ObserveOnDispatcher()
					.Bind(list) 
					.DisposeMany()
					.Subscribe()
```
Oh and I forgot to say, ```TradeProxy``` is disposable and DisposeMany() ensures items are disposed when no longer part of the stream.

**Example 2:**  for filters which can be dynamically changed, we can use a filter controller
```csharp
var filtercontroller = new FilterController<Trade>()
var myoperation = somedynamicdatasource.Filter(filtercontroller) 

//can invoke a filter change any time
filtercontroller.Change(trade=>//return some predicate);
```

**Example 3:** produces a stream which is grouped by status. If an item's changes status it will be moved to the new group and when a group has no items the group will automatically be removed.
```csharp
var myoperation = somedynamicdatasource
					.Group(trade=>trade.Status) //This is NOT Rx's GroupBy 
```
**Example 4:** Suppose I am editing some trades and I have an observable on each trades which validates but I want to know when all items are valid then this will do the job.
```csharp
IObservable<bool> allValid = somedynamicdatasource
	                .TrueForAll(trade => trade.IsValidObservable, (trade, isvalid) => isvalid)
```
This operator flattens the observables and returns the combined state in one line of code. I love it.

**Example 5:**  will wire and un-wire items from the observable when they are added, updated or removed from the source.
```csharp
var myoperation = somedynamicdatasource.Connect() 
				.MergeMany(trade=> trade.ObservePropertyChanged(t=>t.Amount))
				.Subscribe(ObservableOfAmountChangedForAllItems=>//do something with IObservable<PropChangedArg>)
```
**Example 6:** Produces a distinct change set of currency pairs
```csharp
var currencyPairs= somedynamicdatasource
				    .DistinctValues(trade => trade.CurrencyPair)
```

## Want to know more?
There is so much more which will be documented but for now I suggest 

 1. Download the [WPF trading example](https://github.com/RolandPheasant/Tradingdemo) as I intend it to be a 'living document' and it will be continually maintained. 
 2. Read the documents as they are created (keep an eye on this space)
