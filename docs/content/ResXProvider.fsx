(*** hide ***)
#I "../../bin"

(**
The ResX type provider
============================

This tutorial shows the use of the ResX type provider. 
It allows typed access to .resx files.

Create a resource file in Visual Studio like:

![alt text](img/Resource1.png "Resources")

Reference the type provider assembly and configure it to use your Resource1.resx file:

*)

// reference the type provider dll
#r "FSharp.Configuration.dll"
open FSharp.Configuration

// Let the type provider do it's work
type Resource1 = ResXProvider<file="Resource1.resx">

(**
Now you have typed access to .resx files:

![alt text](img/ResXProvider.png "Intellisense for .resx files")

Reading from the resx file
--------------------------

*)

Resource1.Greetings
// [fsi:val it : string = "Hello World!"]