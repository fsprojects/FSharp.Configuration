(*** hide ***)
#I "../../bin"

(**
The Yaml type provider
============================

This tutorial shows the use of the YAml type provider. 
It allows to access .yaml files.

*)

// reference the type provider dll
#r "System.Configuration.dll"
open FSharp.Configuration

// Let the type provider do it's work
type ResX = ResXProvider<file="Resource1.resx">

ResX.Resource1.HelloWorld
// [fsi:val it : string = "Hello World"]