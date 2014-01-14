(*** hide ***)
#I "../../bin"

(**
The AppSettings type provider
============================

This tutorial shows the use of the AppSettings type provider. 
It allows to access app.config files.

*)

// reference the type provider dll
#r "FSharp.Configuration.dll"
#r "System.Configuration.dll"
open FSharp.Configuration

// Let the type provider do it's work
type Settings = AppSettings<"app.config">

Settings.Test2
// [fsi:val it : string = "Some Test Value 5"]