(*** hide ***)
#I "../src/FSharp.Configuration.Runtime/bin/Release/netstandard2.0"

(**
FSharp.Configuration
===========================

The FSharp.Configuration project contains type providers for the configuration of .NET projects.

* [AppSettings](AppSettingsProvider.html)
* [ResX](ResXProvider.html)
* [Yaml](YamlConfigProvider.html)
* [Ini](IniTypeProvider.html)

<div class="row">
  <div class="col-sm-2"></div>
  <div class="col-sm-8">
    <div class="card card-body p-3" id="nuget">
      <p class="mb-0">The library can be <a href="https://nuget.org/packages/FSharp.Configuration">installed from NuGet:</a></a></p>
      <pre>PM> Install-Package FSharp.Configuration</pre>
    </div>
  </div>
  <div class="col-sm-2"></div>
</div>


Example
-------

This example demonstrates the use of the AppSettings type provider:

*)
// reference the type provider dll
#r "FSharp.Configuration.Runtime.dll"
#r "System.Configuration.dll"

open FSharp.Configuration

// Let the type provider do it's work
type Settings = AppSettings<"app.config">

Settings.ConfigFileName
Settings.Test2
// [fsi:val it : string = "Some Test Value 5"]

(**

![alt text](img/AppSettingsProvider.png "Intellisense for the App.Settings")

Contributing and copyright
--------------------------

The project is hosted on [GitHub][gh] where you can [report issues][issues], fork 
the project and submit pull requests. If you're adding new public API, please also 
consider adding [samples][content] that can be turned into a documentation. You might
also want to read [library design notes][readme] to understand how it works.

The library is available under Public Domain license, which allows modification and 
redistribution for both commercial and non-commercial purposes. For more information see the 
[License file][license] in the GitHub repository. 

  [content]: https://github.com/fsprojects/FSharp.Configuration/tree/master/docs/content
  [gh]: https://github.com/fsprojects/FSharp.Configuration
  [issues]: https://github.com/fsprojects/FSharp.Configuration/issues
  [readme]: https://github.com/fsprojects/FSharp.Configuration/blob/master/README.md
  [license]: https://github.com/fsprojects/FSharp.Configuration/blob/master/LICENSE.txt
*)
