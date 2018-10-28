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
