#Filtering
In my experience the filtering options are the the most widely used operators in dynamic data. The more data is filtered the more efficient the operation so I recommend reducing the amount of data in a stream wherever possible.

##Filtering operators
The simplest filter option is to pass a predicate into the connect method on an observable cache.

```csharp
 var myObservableChangeSet = myCache.Connect(x=>//return a predicate);
``` 
but any observable change set has can be filtered.

```csharp
 var myFilteredOperation = myObservableChangeSet.Filter(x=>//return a predicate);
``` 
Easy so far but what if you want to change the filter dynamically. This is possible as dynamic data provides a filter controller.

```csharp
var filterController = new FilterController<T>();
//filter can be changed any time by applying the following method
filterController.Change(t=>//return a predicate);
var myFilteredOperation = myObservableChangeSet.Filter(filterController)
``` 

The filter controller is used to inject a new filter predicate into an observable change set without having to regenerate the entire stream. It has the following signature.

```csharp
public class FilterController<T>
{
	void Change(Func<T, bool> filter); 
	void ChangeToIncludeAll();
	void ChangeToExcludeAll();
	void Reevaluate();
	void Reevaluate(Func<T, bool> itemSelector);
}
``` 
The change operators can be called any time to re-apply a filter and I would say obvious as to what they do. By what is ```Reevaluate()``` I hear you think?

Re-evaluate is a concept introduced by dynamic data to tell the filter to re-evaluate whether an item matches or no longer matches a filter. This is useful when filtering on mutable state on objects such as time or variable meta data.  I will illustrate this with the following conceptual example.

```csharp
var filterController = new FilterController<T>();
filterController.Change(t=>t.Timestamp < 10 mins);
var myFilteredOperation = myObservableChangeSet.Filter(filterController)
``` 
When the observable is first subscribed to, items with a time stamp in the last 10 minutes will correctly match the filter, as will any new items added to the stream.  However as time elapses the oldest items in the stream will be older than 10 minutes and no longer match the filter but the filter has no natural way of know that. This is fixed with a simple re-evaluate.

For example you could poll, and apply the re-evaluation on a timer
```csharp
var refresher = Observable.Timer(TimeSpan.FromMinutes(1))
					.Subscribe(_=>  filterController.Reevaluate());
``` 
This way the filter will always be correct.

I have also applied exactly the same principle for filtering on calculated value and on market data values.

