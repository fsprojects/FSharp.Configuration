(*** hide ***)
#I "../../bin"

(**
The YamlConfig type provider
============================

This tutorial shows the use of the YamlConfig type provider. 

It's generated, hence the types can be used from any .NET language, not only from F# code.

It can produce mutable properties for Yaml scalars (leafs), which means the object tree can be loaded, modified and saved into the original file or a stream as Yaml text. Adding new properties is not supported, however lists can be replaced with new ones atomically. This is intentional, see below.

The main purpose for this is to be used as part of a statically typed application configuration system which would have a single master source of configuration structure - a Yaml file. Then any F#/C# project in a solution will able to use the generated read-only object graph.

When you push a system into production, you can modify the configs with scripts written in F# in safe, statically typed way with full intellisense.

Using Yaml type provider from F# scripts
----------------------------------------

Create a `Config.yaml` file like this:

    [lang=yaml]
    Mail:
        Smtp:
            Host: smtp.sample.com
            Port: 25
            User: user1
            Password: pass1
        Pop3:
            Host: pop3.sample.com
            Port: 110
            User: user2
            Password: pass2
            CheckPeriod: 00:01:00
        ErrorNotificationRecipients:
            - user1@sample.com
            - user2@sample.com
        ErrorMessageId: 9d165087-9b74-4313-ab90-89be897d3d93
    DB:
        ConnectionString: Data Source=server1;Initial Catalog=Database1;Integrated Security=SSPI;
        NumberOfDeadlockRepeats: 5
        DefaultTimeout: 00:05:00

Reference the type provider assembly and configure it to use your yaml file:
*)

#r "FSharp.Configuration.dll"
open FSharp.Configuration

// Let the type provider do it's work
type TestConfig = YamlConfig<"Config.yaml">
let config = TestConfig()

(**

![alt text](img/YamlConfigProvider.png "Intellisense for YamlConfig")

Reading and writing from the config
-----------------------------------

*)

// read a value from the config
config.DB.ConnectionString

// [fsi:val it : string = ]
// [fsi:  "Data Source=server1;Initial Catalog=Database1;Integrated Security=SSPI;"]

// change a value in the config
config.DB.ConnectionString <- "Data Source=server2;"
config.DB.ConnectionString
// [fsi:val it : string = "Data Source=server2;"]

// write the settings back to a yaml file
config.Save(__SOURCE_DIRECTORY__ + @"\ChangedConfig.yaml")

(**
Using configuration from C#
---------------------------
Let's create a F# project named `Config`, add reference to `FSharp.Configuration.dll`, then add the following `Config.yaml` file:

    [lang=yaml]
    Mail:
      Smtp:
        Host: smtp.sample.com
        Port: 25
        User: user1
        Password: pass1
      Pop3:
        Host: pop3.sample.com
        Port: 110
        User: user2
        Password: pass2
        CheckPeriod: 00:01:00
      ErrorNotificationRecipients:
        - user1@sample.com
        - user2@sample.com
      ErrorMessageId: 9d165087-9b74-4313-ab90-89be897d3d93
    DB:
      ConnectionString: Data Source=server1;Initial Catalog=Database1;Integrated Security=SSPI;
      NumberOfDeadlockRepeats: 5
      DefaultTimeout: 00:05:00

Declare a YamlConfig type and point it to the file above:
*)
open FSharp.Configuration

type Config = YamlConfig<"Config.yaml">
(**
Compile it. Now we have assembly `Config.dll` containing generated types with the default values "baked" into them (actually the values are set in the type constructors).

Let's test it in a C# project. Create a Console Application, add reference to `FSharp.Configuration.dll` and our F# `Config` project. 

First, we'll try to create an instance of our generated `Config` type and check that all the values are there:

    [lang:csharp]
    var config = new Config.Config();
    Console.WriteLine(string.Format("Default configuration:\n{0}", config));

It should outputs this:

    [lang:yaml]
    Default settings:
    Mail:
      Smtp:
        Host: smtp.sample.com
        Port: 25
        User: user1
        Password: pass1
      Pop3:
        Host: pop3.sample.com
        Port: 110
        User: user2
        Password: pass2
        CheckPeriod: 00:01:00
      ErrorNotificationRecipients:
      - user1@sample.com
      - user2@sample.com
      ErrorMessageId: 9d165087-9b74-4313-ab90-89be897d3d93
    DB:
      ConnectionString: Data Source=server1;Initial Catalog=Database1;Integrated Security=SSPI;
      NumberOfDeadlockRepeats: 5
      DefaultTimeout: 00:05:00

And, of course, we now able to access all the config data in a nice typed way like this:
*)
let pop3host = config.Mail.Pop3.Host
// [fsi:val pop3host : string = "pop3.sample.com"]

let dbTimeout = config.DB.DefaultTimeout
// [fsi:val dbTimeout : System.TimeSpan = 00:05:00]

(**
It's not very interesting so far, as the main purpose of any configuration is to be loaded from a config file at runtime. 
So, add the following `RuntimeConfig.yaml` into the C# console project:

    [lang:yaml]
    Mail:
      Smtp:
        Host: smtp2.sample.com
        Port: 26
        User: user11
        Password: pass11
      Pop3:
        Host: pop32.sample.com
        Port: 111
        User: user2
        Password: pass2
        CheckPeriod: 00:02:00
      ErrorNotificationRecipients:
        - user11@sample.com
        - user22@sample.com
        - new_user@sample.com
      ErrorMessageId: 9d165087-9b74-4313-ab90-89be897d3d93
    DB:
      ConnectionString: Data Source=server2;Initial Catalog=Database1;Integrated Security=SSPI;
      NumberOfDeadlockRepeats: 5
      DefaultTimeout: 00:10:00

We changed almost every setting here. Update our default config with this file:

    [lang:csharp]
    // ...as before
    config.Load(@"RuntimeConfig.yaml");
    Console.WriteLine(string.Format("Loaded config:\n{0}", config));
    Console.ReadLine();

The output should be:

    [lang:yaml]
    Loaded settings:
    Mail:
      Smtp:
        Host: smtp2.sample.com
        Port: 26
        User: user11
        Password: pass11
      Pop3:
        Host: pop32.sample.com
        Port: 111
        User: user2
        Password: pass2
        CheckPeriod: 00:02:00
      ErrorNotificationRecipients:
      - user11@sample.com
      - user22@sample.com
      - new_user@sample.com
      ErrorMessageId: 9d165087-9b74-4313-ab90-89be897d3d93
    DB:
      ConnectionString: Data Source=server2;Initial Catalog=Database1;Integrated Security=SSPI;
      NumberOfDeadlockRepeats: 5
      DefaultTimeout: 00:10:00

Great! Values have been updated properly, the new user has been added into `ErrorNotificationRecipients` list.

The Changed event
-----------------
Every type in the hierarchy contains `Changed: EventHandler` event. It's raised when an instance is updated (`Load`ed), not when the writable properties are assigned.

Let's show the event in action:
*)

// ...reference assemblies and open namespaces as before...
let c = Config()
let log name _ = printfn "%s changed!" name
// add handlers for the root and all down the Mail hierarchy 
c.Changed.Add (log "ROOT")
c.Mail.Changed.Add (log "Mail")
c.Mail.Smtp.Changed.Add (log "Mail.Smtp")
c.Mail.Pop3.Changed.Add (log "Mail.Pop3")
// as a marker, add a handler for DB
c.DB.Changed.Add (log "DB")
c.LoadText """
Mail:
  Smtp:
    Host: smtp.sample.com
    Port: 25
    User:       => first changed value <=
    Password:   => second changed value on the same level (in the same Map) <=
    Ssl: true   
  Pop3:
    Host: pop3.sample.com
    Port: 110
    User: user2
    Password: pass2
    CheckPeriod: 00:01:00
  ErrorNotificationRecipients:
    - user1@sample.com
    - user2@sample.com
  ErrorMessageId: 9d165087-9b74-4313-ab90-89be897d3d93
DB:
  ConnectionString: Data Source=server1;Initial Catalog=Database1;Integrated Security=SSPI;
  NumberOfDeadlockRepeats: 5
  DefaultTimeout: 00:05:00
""" |> ignore

(**
The output is as follows:

    [lang:text]
    ROOT changed!
    Mail changed!
    Mail.Smtp changed!

So, we can see that all the events have been raised from the root's one down to the most close to the changed value one. And note that there're no duplicates - even though two value was changed in Mail.Smpt map, its Changed event has been raised only once.
*)
