(*** hide ***)
#I "../../bin"

(**
The ConnectionStrings type provider
============================

This tutorial shows the use of the ConnectionStrings type provider. 
It allows to access connection strings from the app.config in a strongly typed way.

Using App.Settings from F# scripts
----------------------------------

Create a config file called `app.config` like this:

    [lang=xml]
    <?xml version="1.0" encoding="utf-8" ?>
    <configuration>
      <connectionStrings>
        <add key="Test" value="Server=.;Database=SomeDatabase;Integrated Security=true"/>
      </connectionStrings>
    </configuration>

Reference the type provider assembly and configure it to use your connection strings file:

*)

#r "FSharp.Configuration.dll"
#r "System.Configuration.dll"
open FSharp.Configuration

type Settings = ConnectionStrings<"app.config">

(**

Now you have typed access to your app.config files:

![alt text](img/ConnectionStringsProvider.png "Intellisense for the Connection Strings")

Reading and writing from the config
-----------------------------------

*)

// read a value from the config
Settings.Test2
// [fsi:val it : string = "Server=.;Database=SomeDatabase;Integrated Security=true"]

// verify the file name
Settings.ConfigFileName
// [fsi:val it : string = "C:\Code\FSharp.Configuration\docs\content\app.config"]
