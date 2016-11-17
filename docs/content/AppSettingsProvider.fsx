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
        <add key="TestDateTime" value="2014-05-18 11:14:28Z"/>
        <add key="TestTimeSpan" value="00:12:30"/>
        <add key="TestUri" value="http://fsharp.org" />
        <add key="TestGuid" value="{7B7EB384-FEBA-4409-B560-66FF63F1E8D0}"/>
      </appSettings>
      <connectionStrings>
        <add name="Test" connectionString="Server=.;Database=SomeDatabase;Integrated Security=true"/>
      </connectionStrings>
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

// read a connection string from the config
Settings.ConnectionStrings.Test
// [fsi:val it : string = "Server=.;Database=SomeDatabase;Integrated Security=true"]

(**

Using AppSettingsProvider in *.fsx-script
-----------------------------------------

The default executable is the current project .config. (Which is Fsi.exe.config in F# interactive.)
How ever, if you want to modify the configuration of some other application, you can do with SelectExecutableFile-method:
*)

let path = System.IO.Path.Combine [|__SOURCE_DIRECTORY__ ; "bin"; "myProject.exe" |]
Settings.SelectExecutableFile path
Settings.Test2
