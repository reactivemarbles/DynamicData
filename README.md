![Build](https://github.com/reactivemarbles/DynamicData/workflows/Build/badge.svg) [![Code Coverage](https://codecov.io/gh/reactivemarbles/DynamicData/branch/main/graph/badge.svg)](https://codecov.io/gh/reactivemarbles/DynamicData)
<a href="https://reactiveui.net/slack">
        <img src="https://img.shields.io/badge/chat-slack-blue.svg">
</a>
[![NuGet Stats](https://img.shields.io/nuget/v/DynamicData.svg)](https://www.nuget.org/packages/DynamicData) ![Downloads](https://img.shields.io/nuget/dt/DynamicData.svg)
<br />
<br />
<a href="https://github.com/reactiveui/DynamicData">
        <img width="170" height="170" src="https://github.com/reactiveui/styleguide/blob/master/logo_dynamic_data/logo.svg"/>
</a>

## Dynamic Data

Dynamic Data is a portable class library which brings the power of Reactive Extensions (Rx) to collections.  

Rx is extremely powerful but out of the box provides nothing to assist with managing collections.  In most applications there is a need to update the collections dynamically.  Typically a collection is loaded and after the initial load, asynchronous updates are received.  The original collection will need to reflect these changes. In simple scenarios the code is simple. However, typical applications are much more complicated and may apply a filter, transform the original dto and apply a sort. Even with these simple every day operations the complexity of the code is quickly magnified.  Dynamic data has been developed to remove the tedious code of dynamically maintaining collections. It has grown to become functionally very rich with at least 60 collection based operations which amongst other things enable filtering, sorting, grouping,  joining different sources,  transforms, binding, pagination, data virtualisation, expiration, disposal management plus more.  

The concept behind using dynamic data is you maintain a data source (either ```SourceCache<TObject, TKey>``` or  ```SourceList<TObject>```),  then chain together various combinations of operators to declaratively manipulate and shape the data without the need to directly manage any collection.   

As an example the following code will filter trades to select only live trades, creates a proxy for each live trade, and finally orders the results by most recent first. The resulting trade proxies are bound on the dispatcher thread to an observable collection.  Also since  the proxy is disposable ```DisposeMany()``` will ensure the proxy is disposed when no longer used.

```cs
ReadOnlyObservableCollection<TradeProxy> list;

var myTradeCache = new SourceCache<Trade, long>(trade => trade.Id);
var myOperation = myTradeCache.Connect() 
		.Filter(trade=>trade.Status == TradeStatus.Live) 
		.Transform(trade => new TradeProxy(trade))
		.Sort(SortExpressionComparer<TradeProxy>.Descending(t => t.Timestamp))
		.ObserveOnDispatcher()
		.Bind(out list) 
		.DisposeMany()
		.Subscribe()
```
The magic is that as  ```myTradeCache``` is maintained the target observable collection looks after itself.

This is a simple example to show how using Dynamic Data's collections and operators make in-memory data management extremely easy and can reduce the size and complexity of your code base by abstracting complicated and often repetitive operations.

### Sample Projects 

- Sample WPF project trading project [Dynamic Trader](https://github.com/RolandPheasant/Dynamic.Trader)
- Various unit tested examples of many different operators [Snippets](https://github.com/RolandPheasant/DynamicData.Snippets)
- [Tail Blazer](https://github.com/RolandPheasant/TailBlazer) for tailing files 

### Get in touch 

If you have any questions, want to get involved or would simply like to keep abreast of developments, you are welcome to join the slack community [Reactive UI Slack](https://reactiveui.net/slack). I am also available [@RolandPheasant](https://twitter.com/RolandPheasant) 
There is a blog at  https://dynamic-data.org/ but alas it is hopelessly out of date.

## Table of Contents

* [Dynamic Data](#dynamic-data)
  * [Sample Projects](#sample-projects)
  * [Get in touch](#get-in-touch)
* [Table of Contents](#table-of-contents)
* [Create Dynamic Data Collections](#create-dynamic-data-collections)
  * [The Observable List](#the-observable-list)
  * [The Observable Cache](#the-observable-cache)
* [Creating Observable Change Sets](#creating-observable-change-sets)
  * [Connect to a Cache or List](#connect-to-a-cache-or-list)
  * [Create an Observable Change Set from an Rx Observable](#create-an-observable-change-set-from-an-rx-observable)
  * [Create an Observable Change Set from an Rx Observable with an Expiring Cache](#create-an-observable-change-set-from-an-rx-observable-with-an-expiring-cache)
  * [Create an Observable Change Set from an Observable Collection](#create-an-observable-change-set-from-an-observable-collection)
  * [Create an Observable Change Set from an Binding List](#create-an-observable-change-set-from-an-binding-list)
  * [Using the ObservableChangeSet static class](#using-the-observablechangeset-static-class)
* [Consuming Observable Change Sets](#consuming-observable-change-sets)
* [Observable list vs observable cache](#observable-list-vs-observable-cache)
* [History of Dynamic Data](#history-of-dynamic-data)
* [Want to know more?](#want-to-know-more)

## Create Dynamic Data Collections

### The Observable List

Create an observable list like this:
```cs
var myInts = new SourceList<int>();
```
The observable list provides the direct edit methods you would expect. For example:
```cs
myInts.AddRange(Enumerable.Range(0, 10000)); 
myInts.Add(99999); 
myInts.Remove(99999);
```
The `AddRange`, `Add` and `Remove` methods above will each produce a distinct change notification.  In order to increase efficiency when making multiple amendments, the list provides a means of batch editing. This is achieved using the `.Edit` method which ensures only a single change notification is produced.
```cs
myInts.Edit(innerList =>
{
   innerList.Clear();
   innerList.AddRange(Enumerable.Range(0, 10000));
});
```
If ``myInts`` is to be exposed publicly it can be made read only using `.AsObservableList`
```cs
IObservableList<int> readonlyInts = myInts.AsObservableList();
```
which hides the edit methods.

The list's changes can be observed by calling `myInts.Connect()` like this:
```cs
IObservable<IChangeSet<int>> myIntsObservable = myInts.Connect();
```
This creates an observable change set for which there are dozens of operators. The changes are transmitted as an Rx observable, so they are fluent and composable.

### The Observable Cache

Create an observable cache like this:
```cs
var myCache = new SourceCache<TObject,TKey>(t => key);
```
There are direct edit methods, for example

```cs
myCache.Clear();
myCache.AddOrUpdate(myItems);
```
The `Clear` and `AddOrUpdate` methods above will each produce a distinct change notification.  In order to increase efficiency when making multiple amendments, the cache provides a means of batch editing. This is achieved using the `.Edit` method which ensures only a single change notification is produced.

```cs
myCache.Edit(innerCache =>
			  {
			      innerCache.Clear();
			      innerCache.AddOrUpdate(myItems);
			  });
```
If `myCache` is to be exposed publicly it can be made read only using `.AsObservableCache`

```cs
IObservableCache<TObject,TKey> readonlyCache = myCache.AsObservableCache();
```
which hides the edit methods.

The cache is observed by calling `myCache.Connect()` like this:
```cs
IObservable<IChangeSet<TObject,TKey>> myCacheObservable = myCache.Connect();
```
This creates an observable change set for which there are dozens of operators. The changes are transmitted as an Rx observable, so they are fluent and composable.

## Creating Observable Change Sets
As stated in the introduction of this document, Dynamic Data is based on the concept of creating and manipulating observable change sets. 

The primary method of creating observable change sets is to connect to instances of `ISourceCache<T,K>` and `ISourceList<T>`. There are alternative methods to produce observables change sets however, depending on the data source.

### Connect to a Cache or List
Calling `Connect()` on a `ISourceList<T>` or `ISourceCache<T,K>` will produce an observable change set. 
```cs
var myObservableChangeSet = myDynamicDataSource.Connect();
```

### Create an Observable Change Set from an Rx Observable
Given either of the following observables:
```cs
IObservable<T> myObservable;
IObservable<IEnumerable<T>> myObservable;
```
an observable change set can be created like by calling `.ToObservableChangeSet` like this:
```cs
var myObservableChangeSet = myObservable.ToObservableChangeSet(t=> t.key);
```

### Create an Observable Change Set from an Rx Observable with an Expiring Cache
The problem with the example above is that the internal backing cache of the observable change set will grow in size forever. 
To counter this behavior, there are overloads of `.ToObservableChangeSet` where a size limitation or expiry time can be specified for the internal cache.

To create a time expiring cache, call `.ToObservableChangeSet` and specify the expiry time using the expireAfter argument:
```cs
var myConnection = myObservable.ToObservableChangeSet(t=> t.key, expireAfter: item => TimeSpan.FromHours(1));
```

To create a size limited cache, call `.ToObservableChangeSet` and specify the size limit using the limitSizeTo argument:
```cs
var myConnection = myObservable.ToObservableChangeSet(t=> t.key, limitSizeTo:10000);
```
There is also an overload to specify expiration by both time and size.

### Create an Observable Change Set from an Observable Collection
```cs
var myObservableCollection = new ObservableCollection<T>();
```
To create a cache based observable change set, call `.ToObservableChangeSet` and specify a key selector for the backing cache
```cs
var myConnection = myObservableCollection.ToObservableChangeSet(t => t.Key);
```
or to create a list based observable change set call `.ToObservableChangeSet` with no arguments
```cs
var myConnection = myObservableCollection.ToObservableChangeSet();
```
This method is only recommended for simple queries which act only on the UI thread as `ObservableCollection` is not thread safe.

### Create an Observable Change Set from an Binding List
```cs
var myBindingList = new BindingList<T>();
```
To create a cache based observable change set, call `.ToObservableChangeSet` and specify a key selector for the backing cache
```cs
var myConnection = myBindingList.ToObservableChangeSet(t => t.Key);
```
or to create a list based observable change set call `.ToObservableChangeSet` with no arguments
```cs
var myConnection = myBindingList.ToObservableChangeSet();
```
This method is only recommended for simple queries which act only on the UI thread as `ObservableCollection` is not thread safe.

### Using the ObservableChangeSet static class

There is also  another way to create observable change sets, and that is to use the ```ObservableChangeSet``` static class.  This class is a facsimile of the Rx.Net ```Observable``` static class and provides an almost identical API. 

An observable list can be created as follows:

```cs
  var myObservableList = ObservableChangeSet.Create<int>(observableList =>
  {
	  //some code to load data and subscribe
      var loader= myService.LoadMyDataObservable().Subscribe(observableList.Add);
      var subscriber = myService.GetMySubscriptionsObservable().Subscribe(observableList.Add);
      //dispose of resources
      return new CompositeDisposable(loader,subscriber );
  });
```
and creating a cache is almost identical except a key has to be specified 
```cs
  var myObservableCache = ObservableChangeSet.Create<Trade, int>(observableCache =>
  {
	  //code omitted
  }, trade = > trade.Id);
```
There are several overloads ```ObservableChangeSet.Create``` which match the overloads which ```Observable.Create``` provides.

## Consuming Observable Change Sets
The examples below illustrate the kind of things you can achieve after creating an observable change set. 
Now you can create an observable cache or an observable list, here are a few quick fire examples to illustrate the diverse range of things you can do. In all of these examples the resulting sequences always exactly reflect the items is the cache i.e. adds, updates and removes are always propagated.

#### Create a Derived List or Cache
This example shows how you can create derived collections from an observable change set. It applies a filter to a collection, and then creates a new observable collection that only contains items from the original collection that pass the filter.
This pattern is incredibly useful when you want to make modifications to an existing collection and then expose the modified collection to consumers. 

Even though the code in this example is very simple, this is one of the most powerful aspects of Dynamic Data. 

Given a SourceList 
```cs
var myList = new SourceList<People>();
```
You can apply operators, in this case the `Filter()` operator, and then create a new observable list with `AsObservableList()`
```cs
var oldPeople = myList.Connect().Filter(person => person.Age > 65).AsObservableList();
```
The resulting observable list, oldPeople, will only contain people who are older than 65.

The same pattern can be used with SourceCache by using `.AsObservableCache()` to create derived caches.

As an alternative to `.Bind(out collection)` you can use `.BindToObservableList(out observableList)` for both `SourceList` & `SourceCache`. This is useful for getting derived read-only lists from sources that use `.AutoRefresh()`, since collections do not support refresh notifications.

#### Filtering
Filter the observable change set by using the `Filter` operator
```cs
var myPeople = new SourceList<People>();
var myPeopleObservable = myPeople.Connect();

var myFilteredObservable = myPeopleObservable.Filter(person => person.Age > 50); 
```
or to filter a change set dynamically 
```cs
IObservable<Func<Person,bool>> observablePredicate=...;
var myFilteredObservable = myPeopleObservable.Filter(observablePredicate); 
```

#### Sorting
Sort the observable change set by using the `Sort` operator
```cs
var myPeople = new SourceList<People>();
var myPeopleObservable = myPeople.Connect();
var mySortedObservable = myPeopleObservable.Sort(SortExpressionComparer.Ascending(p => p.Age)); 
```
or to dynamically change sorting
```cs
IObservable<IComparer<Person>> observableComparer=...;
var mySortedObservable = myPeopleObservable.Sort(observableComparer);
```
For more information on sorting see [wiki](https://github.com/RolandPheasant/DynamicData/wiki/Sorting)

#### Grouping
The `GroupOn` operator pre-caches the specified groups according to the group selector.
```cs
var myOperation = personChangeSet.GroupOn(person => person.Status)
```
The value of the inner group is represented by an observable list for each matched group. When values matching the inner grouping are modified, it is the inner group which produces the changes.
You can also use `GroupWithImmutableState` which will produce a grouping who's inner items are a fixed size array.

#### Transformation
The `Transform` operator allows you to map objects from the observable change set to another object
```cs
var myPeople = new SourceList<People>();
var myPeopleObservable = myPeople.Connect();
var myTransformedObservable = myPeopleObservable.Transform(person => new PersonProxy(person));
```

The `TransformToTree` operator allows you to create a fully formed reactive tree (only available for observable cache)
```cs
var myPeople = new SourceCache<Person, string>(p => p.Name);
var myTransformedObservable = myPeople.Connect().TransformToTree(person => person.BossId);
```


Flatten a child enumerable
```cs
var myOperation = personChangeSet.TransformMany(person => person.Children) 
```

#### Aggregation
The `Count`, `Max`, `Min`, `Avg`, and `StdDev` operators allow you to perform aggregate functions on observable change sets
```cs
var myPeople = new SourceList<People>();
var myPeopleObservable = myPeople.Connect();

var countObservable = 	 myPeopleObservable.Count();
var maxObservable = 	 myPeopleObservable.Max(p => p.Age);
var minObservable = 	 myPeopleObservable.Min(p => p.Age);
var stdDevObservable =   myPeopleObservable.StdDev(p => p.Age);
var avgObservable = 	 myPeopleObservable.Avg(p => p.Age);
```
More aggregating operators will be added soon.

#### Logical Operators
The `And`, `Or`, `Xor` and `Except` operators allow you to perform logical operations on observable change sets
```cs
var peopleA = new SourceCache<Person,string>(p => p.Name);
var peopleB = new SourceCache<Person,string>(p => p.Name);

var observableA = peopleA.Connect();
var observableB = peopleB.Connect();

var inBoth = observableA.And(observableB);
var inEither= observableA.Or(observableB);
var inOnlyOne= observableA.Xor(observableB);
var inAandNotinB = observableA.Except(observableB);
```

A recent and very powerful feature is dynamic logical operators. From version 4.6 onwards you can dynamically include and exclude collections from the resulting list. 
```cs
var list1 = new SourceList<int>();
var list2 = new SourceList<int>();
var list3  = new SourceList<int>();
	
var combined = new SourceList<ISourceList<int>>();

//child lists can be added or removed any time
combined.Add(list1);
combined.Add(list2);
combined.Add(list3);

//The operators look after themselves 
var inAll = combined.And();
var inAny = combined.Or();
var inOnlyOne= combined.Xor();
var inFirstAndNotAnyOther = combined.Except();
```
For more information on grouping see [wiki](https://github.com/RolandPheasant/DynamicData/wiki/Composite-Collections)
 

#### Disposal
The `DisposeMany` operator ensures that objects are disposed when removed from an observable stream
```cs
var myPeople = new SourceList<People>();
var myPeopleObservable = myPeople.Connect();
var myTransformedObservable = myPeopleObservable.Transform(person => new DisposablePersonProxy(person))
                                                .DisposeMany();
```
The `DisposeMany` operator is typically used when a transform function creates disposable objects.

#### Distinct Values
The `DistinctValues` operator will select distinct values from the underlying collection
```cs
var myPeople = new SourceList<People>();
var myPeopleObservable = myPeople.Connect();
var myDistinctObservable = myPeopleObservable.DistinctValues(person => person.Age);
```

#### Virtualisation

Visualise data to restrict by index and segment size
```cs
IObservable<IVirtualRequest> request; //request stream
var virtualisedStream = someDynamicDataSource.Virtualise(request)
```
Visualise data to restrict by index and page size
```cs
IObservable<IPageRequest> request; //request stream
var pagedStream = someDynamicDataSource.Page(request)
```
In either of the above, the result is re-evaluated when the request stream changes

Top is an overload of ```Virtualise()``` and will return items matching the first 'n'  items.
```cs
var topStream = someDynamicDataSource.Top(10)
```

#### Observing Properties of Objects in a Collection
If the collection is made up of objects that implement `INotifyPropertyChanged` then the following operators are available

The `WhenValueChanged` operator returns an observable of the value of the specified property when it has changed
```cs
var ageChanged = peopleDataSource.Connect().WhenValueChanged(p => p.Age)
```

The `WhenPropertyChanged` operator returns an observable made up of the value of the specified property as well as it's parent object when the specified property has changed
```cs
var ageChanged = peopleDataSource.Connect().WhenPropertyChanged(p => p.Age)
```

The `WhenAnyPropertyChanged` operator returns an observable of objects when any of their properties have changed
```cs
var personChanged = peopleDataSource.Connect().WhenAnyPropertyChanged()
```

#### Observing item changes

Binding is a very small part of Dynamic Data. The above notify property changed overloads are just an example when binding. If you have a domain object which has children observables you can use ```MergeMany()``` which subscribes to and unsubscribes from items according to collection changes.

```cs
var myoperation = somedynamicdatasource.Connect() 
			.MergeMany(trade => trade.SomeObservable());
```
This wires and unwires ```SomeObservable``` as the collection changes.

## Observable list vs observable cache
I get asked about the differences between these a lot and the answer is really simple. If you have a unique id, you should use an observable cache as it is dictionary based which will ensure no duplicates can be added and it notifies on adds, updates and removes, whereas list allows duplicates and only has no concept of an update.

There is another difference. The cache side of dynamic data is much more mature and has a wider range of operators. Having more operators is mainly because I found it easier to achieve good all round performance with the key based operators and do not want to add anything to Dynamic Data which inherently has poor performance.

## History of Dynamic Data
Even before Rx existed I had implemented a similar concept using old fashioned events but the code was very ugly and my implementation full of race conditions so it never existed outside of my own private sphere. My second attempt was a similar implementation to the first but using Rx when it first came out. This also failed as my understanding of Rx was flawed and limited and my design forced consumers to implement interfaces.  Then finally I got my design head on and in 2011-ish I started writing what has become dynamic data. No inheritance, no interfaces, just the ability to plug in and use it as you please.  All along I meant to open source it but having so utterly failed on my first 2 attempts I decided to wait until the exact design had settled down. The wait lasted longer than I expected and ended up taking over 2 years but the benefit is it has been trialled for 2 years on a very busy high volume low latency trading system which has seriously complicated data management. And what's more that system has gathered a load of attention for how slick and cool and reliable it is both from the user and IT point of view. So I present this library with the confidence of it being tried, tested, optimised and mature. I hope it can make your life easier like it has done for me.

## Want to know more?
I could go on endlessly but this is not the place for full documentation.  I promise this will come but for now I suggest downloading my WPF sample app (links at top of document)  as I intend it to be a 'living document' and I promise it will be continually maintained. 

Also, if you follow me on Twitter you will find out when new samples or blog posts have been updated.

Additionally, if you have read up to here and not pressed star then why not? Ha. A star may make me be more responsive to any requests or queries.
