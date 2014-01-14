(*** hide ***)
#I "../../bin"

(**
The ResX type provider
============================

This tutorial shows the use of the ResX type provider. 
It allows to access .resx files.

*)

// reference the type provider dll
#r "System.Configuration.dll"
open FSharp.Configuration

// Let the type provider do it's work
type ResX = ResXProvider<file="Resource1.resx">

ResX.Resource1.HelloWorld
// [fsi:val it : string = "Hello World"]