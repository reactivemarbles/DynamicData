## Dynamic Data

Dynamic data is a portable class library which brings the power of reactive (rx) to collections.  

A collection which mutates can have adds, updates and removes (plus moves and re-evaluates but more about that another time). Dynamic data has been evolved to take Rx to another dimension by introducing an observable cache and an observable list where changes are notified via an observable change set .  Operators receive these notifications then apply some logic and subsequently provides it's own notifications. In this way operators can be chained together to apply powerful and often very complicated operations with some very simple fluent code.

The benefit of at least 50 operators which are borne from pragmatic experience is that the management of in-memory data becomes easy and it is no exaggeration to say it can save thousands of lines of code by abstracting complicated and often repetitive operations.

###Some links

- [![Join the chat at https://gitter.im/RolandPheasant/DynamicData](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/RolandPheasant/DynamicData?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)
- [![Downloads](https://img.shields.io/nuget/dt/DynamicData.svg)](http://www.nuget.org/packages/DynamicData/)	
- [![Build status]( https://ci.appveyor.com/api/projects/status/occtlji3iwinami5/branch/develop?svg=true)](https://ci.appveyor.com/project/RolandPheasant/DynamicData/branch/develop)
- Sample wpf project https://github.com/RolandPheasant/Dynamic.Trader
- Blog at  http://dynamic-data.org/
- You can contact me on twitter  [@RolandPheasant](https://twitter.com/RolandPheasant) or email at [roland@dynamic-data.org]

### Version 4 has been released

The core of dynamic data is an observable cache which for most circumstances is great but sometimes there is the need simply for an observable list. Version 4 delivers this. It has been a great effort and consumed loads of my time, mental capacity and resolve but it is finally crystallising into a stable state. 

If you download the latest release of dynamic data from [dynamic data on nuget](https://www.nuget.org/packages/DynamicData/) you can create and have fun with the observable list.

### The observable list

Create an observable list like this:
```
var myInts= new SourceList<int>();
```
There are direct edit methods, for example
```
myInts.AddRange(Enumerable.Range(0, 10000)); 
myInts.Add(99999); 
myInts.Remove(99999);
```
Each amend operation will produce a change notification. A much more efficient option is to batch edit which produces a single notification.
```
myInts.Edit(innerList =>
{
   innerList.Clear();
   innerList.AddRange(Enumerable.Range(0, 10000));
});
```
If ``myInts``` is to be exposed publicly it can be made read only
```
IObservableList<int> readonlyInts = myInts.AsObservableList();
```
which hides the edit methods.

The list changes can be observed by calling ```myInts.Connect()```. This creates an observable change set for which there are dozens of list specific operators. The changes are transmitted as an Rx observable so are fluent and composable.

### The observable cache

Create an observable cache like this:
```
var myCache= new SourceCache<TObject,TKey>(t => key);
```
There are direct edit methods, for example

```
myCache.Clear();
myCache.AddOrUpdate(myItems);
```
Each amend operation will produced a change notification. A much more efficient option is to batch edit which produces a single notification.

```
myCache.BatchUpdate(innerCache =>
			  {
			      innerCache.Clear();
			      innerCache.AddOrUpdate(myItems);
			  });
```
If ```myCache``` is to be exposed publicly it can be made read only

```
IObservableCache<TObject,TKey> readonlyCache= myCache.AsObservableCache();
```
which hides the edit methods.

The cache is observed by calling ```myInts.Connect()```. This creates an observable change set for which there are dozens of list specific operators. The changes are transmitted as an Rx observable so are fluent and composable.


### Create an observable change set from a standard Rx observable

Given either of the following observables
```csharp
IObservable<T> myObservable;
IObservable<IEnumerable<T>> myObservable;
```
an observable cache can be created like like 
```csharp
//1. This option will create an observable where item's are identified using the hash code.
var mydynamicdatasource = myObservable.ToObservableChangeSet();
//2. Or specify a key like this
var mydynamicdatasource = myObservable.ToObservableChangeSet(t=> t.key);
```
### Create a size or time based expiring cache

The problem with the above is the cache will grow forever so there are overloads to specify size limitation or expiry times. The following shows how to limit the size and create a time expiring cache.
```csharp
//Time limit cache where the expiry time for each item can be specified
var mydynamicdatasource = myObservable.ToObservableChangeSet(t=> t.key, expireAfter: item => TimeSpan.FromHours(1));
//limit the cache to a maximum size
var mydynamicdatasource = myObservable.ToObservableChangeSet(t=> t.key, limitSizeTo:10000);
```

### Create an observable change set from an observable collection
Another way is to directly from an observable collection, you can do this
```csharp
var myobservablecollection= new ObservableCollection<T>();
// Use the hashcode for the key
var mydynamicdatasource = myobservablecollection.ToObservableChangeSet();
// or specify a key like this
var mydynamicdatasource = myobservablecollection.ToObservableChangeSet(t => t.Key);
```
This method is only recommended for simple queries which act only on the UI thread as ```ObservableCollection``` is not thread safe.

### Now for some powerful examples

No you can create an observable cache or an observable list, here are a few quick fire examples to illustrated the diverse range of things you can do. In all of these examples the resulting sequences always exactly reflect the items is the cache i.e. adds, updates and removes are always propagated.

#### Bind to a complex stream

This example a stream of live trades, creates a proxy for each trade and order the result by most recent first. The result is bound to the observable collection. (```ObservableCollectionExtended<T>``` is provided by dynamic data and is more efficient than the standard ``ObservableCollection<T>``` )
```
//Dynamic data has it's own take on an observable collection (optimised for populating f
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
Oh and I forgot to say, ```TradeProxy``` is disposable and ```DisposeMany()``` ensures items are disposed when no longer part of the stream.

#### Create a derived list or cache

Although this example is very simple, it is one of the most powerful aspects of dynamic data.  Any dynamic data stream can be materialised into a derived collection.  

If you have 
```
var myList = new SourceList<People>()
```
You can do this
``` 
var oldPeople = myList.Filter(person=>person.Age>65).AsObservableList();
```
and you have an observable list of pensioners.

The same applies to a cache.  The only difference is you call ```.AsObservableCache()``` to create a derived cache.

In practise I have found this function very useful in a trading system where old items massively outnumber current items.  By creating a derived collection and exposing that to consumers has saved a huge amount of processing power and memory downstream.

#### Filtering
Filter the underlying data using the filter operators
```
var myoperation = personChangeSet.Filter(person=>person.Age>50) 
```
or to dynamically change a filter 
```
IObservable<Func<Person,bool>> observablePredicate=...;
var myoperation = personChangeSet.Filter(observablePredicate) 
```
#### Sorting

Filter the underlying data using the filter operators
```
var myoperation = personChangeSet.Sort(SortExpressionComparer.Ascending(p=>p.Age) 
```
or to dynamically change a filter 
```
IObservable<IComparer<Person>> observableComparer=...;
var myoperation = personChangeSet.Filter(observableComparer) 
```
#### Grouping

This operator pre-caches the specified groups according to the group selector.
```
var myoperation = personChangeSet.GroupOn(person=>person.Status)
```

#### Transformation

Map to a another object
```
var myoperation = personChangeSet.Transform(person=>new PersonProxy(person)) 
```
Ceate a fully formed reactive tree
```
var myoperation = personChangeSet.TransformToTree(person=>person.BossId) 
```
Flatten  a child enumerable
```
var myoperation = personChangeSet.TransformMany(person=>person.Children) 
```
#### Aggregation

if we have a a list of people we can aggregate as follows
```
var count= 	personChangeSet.Count();
var max= 	personChangeSet.Max(p=>p.Age);
var min= 	personChangeSet.Min(p=>p.Age);
var stdDev= personChangeSet.StdDev(p=>p.Age);
var avg= 	personChangeSet.Avg(p=>p.Age);
```
In the near future I will create even more aggregations.

#### Join operators

There are And, Or, Xor and Except logical operators
```csharp
var peopleA= new SourceCache<Person,string>(p=>p.Name);
var peopleB= new SourceCache<Person,string>(p=>p.Name);

var observableA = peopleA.Connect();
var observableB = peopleB.Connect();

var inBoth = observableA.And(observableB);
var inEither= observableA.Or(observableB);
var inOnlyOne= observableA.Xor(observableB);
var inAandNotinB = observableA.Except(observableB);
```
Currently the join operators are only implemented for cache observables

#### Disposal handler

To ensure an object is disposed when it is removed from a stream
```
var myoperation = somedynamicdatasource.Connect().DisposeMany()
```
which will also dispose all objects when the stream is disposed. This is typically used when a transform function creates an object which is disposable.

#### Distinct Values

```DistinctValues()``` will produce an observable of distinct changes in the underlying collection.
```
var people = personSource.DistinctValues(trade => trade.Age)
```
In this case a distinct ages.


#### Virtualisation

Visualise data to restrict by index and segment size
```
IObservable<IVirtualRequest> request; //request stream
var virtualisedStream = somedynamicdatasource.Virtualise(request)
```
Visualise data to restrict by index and page size
```
IObservable<IPageRequest> request; //request stream
var pagedStream = somedynamicdatasource.Page(request)
```
In either of the above, the result is re-evaluated when the request stream changes

Top is an overload of ```Virtualise()``` and will return items matching the first 'n'  items.
```
var topStream = somedynamicdatasource.Top(10)
```
#### Observing binding changes

If the collection has objects which implement ```INotifyPropertyChanged``` the the following operators are available
```
var ageChanged = peopleDataSource.WhenValueChanged(p => p.Age)
```
which returns an observable of the age when the value of Age has changes, .
```
var ageChanged = peopleDataSource.WhenPropertyChanged(p => p.Age)
```
which returns an observable of the person and age when the value of Age has changes, .
```
var personChanged = peopleDataSource.WhenAnyPropertyChanged()
```
which returns an observable of the person when any property has changed,.

#### Observing item changes

Binding is a very small part of dynamic data. The above notify property changed overloads are just an example when binding. If you have a domain object which has children observables you can use ```MergeMany()``` which subscribes to and unsubscribes from items according to collection changes.

```csharp
var myoperation = somedynamicdatasource.Connect() 
			.MergeMany(trade=> trade.SomeObservable());
```
This wires and unwires ```SomeObservable``` as the collection changes.


### Why was the first Nuget release version 3
Even before rx existed I had implemented a similar concept using old fashioned events but the code was very ugly and my implementation full of race conditions so it never existed outside of my own private sphere. My second attempt was a similar implementation to the first but using rx when it first came out. This also failed as my understanding of rx was flawed and limited and my design forced consumers to implement interfaces.  Then finally I got my design head on and in 2011-ish I started writing what has become dynamic data. No inheritance, no interfaces, just the ability to plug in and use it as you please.  All along I meant to open source it but having so utterly failed on my first 2 attempts I decided to wait until the exact design had settled down. The wait lasted longer than I expected and end up taking over 2 years but the benefit is it has been trialled for 2 years on a very busy high volume low latency trading system which has seriously complicated data management. And what's more that system has gathered a load of attention for how slick and cool and reliable it is both from the user and IT point of view. So I present this library with the confidence of it being tried, tested, optimised and mature. I hope it can make your life easier like it has done for me.

### Want to know more?
I could go on endlessly but this is not the place for full documentation.  I promise this will come but for now I suggest downloading my WPF sample app (links above)  as I intend it to be a 'living document' and I promise it will be continually maintained. 

Also if you following me on Twitter you will find out when new samples or blog posts have been updated.

Additionally if you have read up to here and not pressed star then why not? Ha. A star may make me be more responsive to any requests or queries.

