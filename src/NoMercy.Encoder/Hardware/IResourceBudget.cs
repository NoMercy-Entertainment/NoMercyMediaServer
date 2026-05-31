// Shared interface lives in NoMercy.Resources. This file is kept so that
// code inside NoMercy.Encoder can continue to use IResourceBudget without
// changing import statements — the global alias pulls it into scope.
global using IResourceBudget = NoMercy.Resources.IResourceBudget;
