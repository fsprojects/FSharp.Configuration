(*** hide ***)
#I "../../bin"

(**
The AppSettings type provider
============================

This tutorial shows the use of the AppSettings type provider. 
It allows to access app.config files in a strongly typed way.

Using App.Settings from F# scripts
----------------------------------

Create a config file called `app.config` like this:

    [lang=xml]
    <?xml version="1.0" encoding="utf-8" ?>
    <configuration>
      <appSettings>
        <add key="test2" value="Some Test Value 5"/>
        <add key="TestInt" value="102"/>
        <add key="TestBool" value="True"/>
        <add key="TestDouble" value="10.01"/>
      </appSettings>
    </configuration>

Reference the type provider assembly and configure it to use your app.settings file:

*)

#r "FSharp.Configuration.dll"
#r "System.Configuration.dll"
open FSharp.Configuration

type Settings = AppSettings<"app.config">

(**

Now you have typed access to your app.config files:

![alt text](img/AppSettingsProvider.png "Intellisense for the App.Settings")

Reading and writing from the config
-----------------------------------

*)

// read a value from the config
Settings.Test2
// [fsi:val it : string = "Some Test Value 5"]

// verify the file name
Settings.ConfigFileName
// [fsi:val it : string = "C:\Code\FSharp.Configuration\docs\content\app.config"]
