The release notes from now on will be contained within https://github.com/reactiveui/DynamicData/releases

### v 6.12.3

First release done by the ReactiveUI release process. Release notes are now auto-generated. With the change to the ReactiveUI project there has been a rebranding of the logo. Roland Pheasant is still the main maintainer and copyright owner of the code.

* housekeeping: dont sign the DynamicData packages. [#249](https://github.com/RolandPheasant/DynamicData/pull/234) @glennawatson 
* housekeeping: Correct PackageIconUrl @worldbeater 
* Azure build script, cake script, complying with code standards [#241](https://github.com/RolandPheasant/DynamicData/pull/241) @glennawatson 
* Moved the NuGet icon, removed AppVeyor. @glennawatson 
* Update readme with the new logo/build status. @glennawatson 
* Move files from base directory to /src directory [#242](https://github.com/RolandPheasant/DynamicData/pull/242) @glennawatson 

### v 6.11.0

Change aware cache crash - fixed [#234](https://github.com/RolandPheasant/DynamicData/pull/234)
Fixed bug in And operator [#231](https://github.com/RolandPheasant/DynamicData/pull/231)
Transform to tree issue when adding parent after child fix [#230](https://github.com/RolandPheasant/DynamicData/pull/230)


### v 6.10.0

Transform to tree fixes [#230](https://github.com/RolandPheasant/DynamicData/issues/230) and [#225](https://github.com/RolandPheasant/DynamicData/issues/225).
Use minimum version of Rx = 4.1.5. See [#229](https://github.com/RolandPheasant/DynamicData/pull/229)

### v 6.9.1

Fix for transform on refresh overload for source list[#220](https://github.com/RolandPheasant/DynamicData/issues/220).

### v 6.9.0

Introduction of `Preview` operator which applies to soure cache and source list [#218](https://github.com/RolandPheasant/DynamicData/issues/218). There is a detailed explanation of [in the preview wiki](https://github.com/RolandPheasant/DynamicData/wiki/Preview-observable)

Fix crash in parallel transform when value is null [#216](https://github.com/RolandPheasant/DynamicData/pull/216)

### v 6.8.0

Update to MsBuild.Sdk.Extras v1.6.68 [#207](https://github.com/RolandPheasant/DynamicData/issues/207)

Add overload for AddOrUpdate with IEqualityComparer parameter [#204](https://github.com/RolandPheasant/DynamicData/issues/204)

Update to latest version of SourceLink in order to compile on mac [#203](https://github.com/RolandPheasant/DynamicData/issues/203)

Add ToSortedCollection() operator for ObservableCache and ObservableList [#202](https://github.com/RolandPheasant/DynamicData/issues/202)

Fix expire after bug [#196](https://github.com/RolandPheasant/DynamicData/issues/196)

Add overload for TransformMany to support IObservableList [#193](https://github.com/RolandPheasant/DynamicData/issues/193)

### v 6.7.1

CountChanged not working on SourceCache [#188](https://github.com/RolandPheasant/DynamicData/issues/188)

### v 6.7.0

Added overloads of ```Bind()``` to support binding to ```BindingList``` [#182](https://github.com/RolandPheasant/DynamicData/issues/182)

Fix for ```TransformMany``` when a refresh event is received  [#173](https://github.com/RolandPheasant/DynamicData/pull/173)


### v 6.6.1

Fix for UWP load issue. See additonally comment on [#161](https://github.com/RolandPheasant/DynamicData/issues/161)

Dynamic filter with source list not filtering correctly when item is not initially matching filter [#164](https://github.com/RolandPheasant/DynamicData/issues/164)

### v 6.6.0

Upgraded DynamicData.ReactiveUI to use minimum version of ReactiveUI v9.0.1. 
This effectively marks DynamicData.ReactiveUI as obsolete as it provides apaptors for ReactiveList which is no longer supported by the ReactiveUI team.

Support for UWP [#161](https://github.com/RolandPheasant/DynamicData/issues/161)

### v 6.5.1

Fix locking error in SourceCache internals [#153](https://github.com/RolandPheasant/DynamicData/issues/153)

### v 6.5.0

Memory and performance improvements for the observable cache, which has been achieved by reducing the number of allocations when maintaining state and when creating change sets.

Updated Dynamic Data to use minimum version of Rx v4.0.0 [#124](https://github.com/RolandPheasant/DynamicData/issues/124)

Improved debugging experience thanks to embedded symbols [#147](https://github.com/RolandPheasant/DynamicData/issues/147) 

Performance improvement for Bind operator in observable list  [#143](https://github.com/RolandPheasant/DynamicData/pull/143)

Enable the creation of an observable change set from any enumerable [#132](https://github.com/RolandPheasant/DynamicData/issues/132)

Fix bug in distinct values in observable list [#139](https://github.com/RolandPheasant/DynamicData/issues/139)

Filter(reapplyFilter) throws an ArgumentNullException [#128](https://github.com/RolandPheasant/DynamicData/issues/128)

OnItemAdded and OnItemRemoved for SourceCache [#135](https://github.com/RolandPheasant/DynamicData/pull/135)

Also a previous PR [#126](https://github.com/RolandPheasant/DynamicData/pull/126) to optimise sorting has been reverted due a bug which caused an occasional crash in in Dynamic Trader. The intention is to fix the bug and restate the PR at a later date
