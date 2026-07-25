## Release 1.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|--------------------
NA0001  | Usage | Warning | Use named arguments on calls with more than three arguments, and on bare true/false/null values
CB001   | Naming | Warning | Callback parameter name too short
NMS001  | NoMercy.Storage | Warning | Use IStorage.CombinePath instead of System.IO.Path.Combine for storage paths
NMS002  | NoMercy.Storage | Warning | Avoid System.IO.Path decomposition methods on storage-relative paths
