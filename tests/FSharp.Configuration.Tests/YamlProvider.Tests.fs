module FSharp.Configuration.Tests.YamlTests

open FSharp.Configuration
open NUnit.Framework
open FsUnit
open System
open System.IO

type Settings = YamlConfig<"Settings.yaml">

[<Test>] 
let ``Can return a string from the settings file``() = 
    let settings = Settings()  
    settings.DB.ConnectionString.GetType() |> should equal typeof<string>   
    settings.DB.ConnectionString |> should equal "Data Source=server1;Initial Catalog=Database1;Integrated Security=SSPI;"

[<Test>] 
let ``Can return an int from the settings file``() =   
    let settings = Settings()
    settings.DB.NumberOfDeadlockRepeats.GetType() |> should equal typeof<int>   
    settings.DB.NumberOfDeadlockRepeats |> should equal 5

[<Test>] 
let ``Can return a TimeSpan from the settings file``() =   
    let settings = Settings()
    settings.DB.DefaultTimeout.GetType() |> should equal typeof<System.TimeSpan>   
    settings.DB.DefaultTimeout |> should equal (System.TimeSpan.FromMinutes 5.)

[<Test>] 
let ``Can return a list from the settings file``() = 
    let settings = Settings()
    settings.Mail.ErrorNotificationRecipients.Count |> should equal 2
    settings.Mail.ErrorNotificationRecipients.[0] |> should equal "user1@sample.com"
    settings.Mail.ErrorNotificationRecipients.[1] |> should equal "user2@sample.com"

[<Test>] 
let ``Can write to a string in the settings file``() =
    let settings = Settings()
    settings.DB.ConnectionString <- "Data Source=server2"
    settings.DB.ConnectionString |> should equal "Data Source=server2"

[<Test>] 
let ``Can write to an int in the settings file``() =
    let settings = Settings()
    settings.DB.NumberOfDeadlockRepeats <- 6
    settings.DB.NumberOfDeadlockRepeats |> should equal 6

let private assertFilesAreEqual expected actual =
    let read file = (File.ReadAllText file).Replace("\r\n", "\n")
    read expected |> should equal (read actual)

[<Test>] 
let ``Can save a settings file to a specified location``() =
    let settings = Settings()
    settings.DB.NumberOfDeadlockRepeats <- 11
    settings.DB.DefaultTimeout <- System.TimeSpan.FromMinutes 6.
    settings.Save("SettingsModifed.yaml")
    assertFilesAreEqual "SettingsModifed.yaml" "Settings2.yaml"

[<Test>] 
let ``Can save settings to the file it was loaded from last time``() =
    let settings = Settings()
    let tempFile = Path.GetTempFileName()
    try
        File.Copy ("Settings.yaml", tempFile, overwrite=true)
        settings.Load tempFile
        settings.DB.NumberOfDeadlockRepeats <- 11
        settings.DB.DefaultTimeout <- System.TimeSpan.FromMinutes 6.
        settings.Save()
        assertFilesAreEqual tempFile "Settings2.yaml"
    finally File.Delete tempFile

[<Test>] 
let ``Throws exception during saving if it was not loaded from a file and location is not specified``() =
    let settings = Settings()
    (fun() -> settings.Save()) |> should throw typeof<InvalidOperationException>
    
[<Test>] 
let ``Can loads full settings``() =
    let settings = Settings()
    settings.LoadText """
Mail:
  Smtp:
    Host: smtp.sample.com*
    Port: 4430
    User: user1*
    Password: pass1*
  Pop3:
    Host: pop3.sample.com*
    Port: 3310
    User: user2*
    Password: pass2*
    CheckPeriod: 00:02:00
  ErrorNotificationRecipients:
    - user1@sample.com*
    - user2@sample.com*
    - user3@sample.com
DB:
  ConnectionString: Data Source=server1;Initial Catalog=Database1;Integrated Security=SSPI;*
  NumberOfDeadlockRepeats: 50
  DefaultTimeout: 00:06:00
"""
    settings.Mail.Smtp.Host |> should equal "smtp.sample.com*"
    settings.Mail.Smtp.Port |> should equal 4430
    settings.Mail.Smtp.User |> should equal "user1*"
    settings.Mail.Smtp.Password |> should equal "pass1*"

    settings.Mail.Pop3.Host |> should equal "pop3.sample.com*"
    settings.Mail.Pop3.Port |> should equal 3310
    settings.Mail.Pop3.User |> should equal "user2*"
    settings.Mail.Pop3.Password |> should equal "pass2*"
    settings.Mail.Pop3.CheckPeriod |> should equal (TimeSpan.FromMinutes 2.)

    Assert.That(settings.Mail.ErrorNotificationRecipients, 
                Is.EquivalentTo ["user1@sample.com*"; "user2@sample.com*"; "user3@sample.com"])

    settings.DB.ConnectionString |> should equal "Data Source=server1;Initial Catalog=Database1;Integrated Security=SSPI;*"
    settings.DB.NumberOfDeadlockRepeats |> should equal 50
    settings.DB.DefaultTimeout |> should equal (TimeSpan.FromMinutes 6.)

[<Test>]
let ``Can load partial settings``() =
    let settings = Settings()
    settings.LoadText """
Mail:
  Smtp:
    Port: 4430
  Pop3:
    CheckPeriod: 00:02:00
  ErrorNotificationRecipients:
    - user1@sample.com*
    - user2@sample.com*
    - user3@sample.com
"""
    settings.Mail.Smtp.Host |> should equal "smtp.sample.com"
    settings.Mail.Smtp.Port |> should equal 4430
    settings.Mail.Smtp.User |> should equal "user1"
    settings.Mail.Smtp.Password |> should equal "pass1"

    settings.Mail.Pop3.Host |> should equal "pop3.sample.com"
    settings.Mail.Pop3.Port |> should equal 331
    settings.Mail.Pop3.User |> should equal "user2"
    settings.Mail.Pop3.Password |> should equal "pass2"
    settings.Mail.Pop3.CheckPeriod |> should equal (TimeSpan.FromMinutes 2.)

    Assert.That(settings.Mail.ErrorNotificationRecipients, 
                Is.EquivalentTo ["user1@sample.com*"; "user2@sample.com*"; "user3@sample.com"])

    settings.DB.ConnectionString |> should equal "Data Source=server1;Initial Catalog=Database1;Integrated Security=SSPI;"
    settings.DB.NumberOfDeadlockRepeats |> should equal 5
    settings.DB.DefaultTimeout |> should equal (TimeSpan.FromMinutes 5.)

[<Test>]
let ``Can load settings containing unknown nodes``() =
    let settings = Settings()
    settings.LoadText """
Mail:
  Smtp:
    Port: 4430
    NestedUnknown: value1
TopLevelUnknown:
  Value: 1

"""
    settings.Mail.Smtp.Port |> should equal 4430
    
[<Test>]
let ``Can load empty lists``() =
    let settings = Settings()
    settings.LoadText """
Mail:
  ErrorNotificationRecipients: []
"""
    settings.Mail.ErrorNotificationRecipients |> should be Empty

type private Listener(event: IEvent<EventHandler,_>) =
    let events = ref 0
    do event.Add (fun _ -> incr events)
    member x.Events = !events

[<Test>]
let ``Raises Changed events``() =
    let settings = Settings()
    let rootListener = Listener(settings.Changed)
    let mailListener = Listener(settings.Mail.Changed)
    let pop3Listener = Listener(settings.Mail.Pop3.Changed)
    let smtpListener = Listener(settings.Mail.Smtp.Changed)
    let dbListener = Listener(settings.DB.Changed)

    settings.LoadText """
Mail:
  Pop3:
    CheckPeriod: 00:02:00
"""
    [rootListener.Events
     mailListener.Events
     pop3Listener.Events
     smtpListener.Events
     dbListener.Events] 
    |> should equal [1; 1; 1; 0; 0]
    
[<Test>]
let ``Does not raise duplicates of parent Changed events even though several children changed``() =
    let settings = Settings()
    let rootListener = Listener(settings.Changed)
    let mailListener = Listener(settings.Mail.Changed)
    let pop3Listener = Listener(settings.Mail.Pop3.Changed)
    let smtpListener = Listener(settings.Mail.Smtp.Changed)

    settings.LoadText """
Mail:
  Smtp:
    Port: 4430
  Pop3:
    CheckPeriod: 00:02:00
"""
    [rootListener.Events
     mailListener.Events
     pop3Listener.Events
     smtpListener.Events] 
    |> should equal [1; 1; 1; 1]


type Lists = YamlConfig<"Lists.yaml">

[<Test>]
let ``Can load sequence of maps (single item)``() =
    let settings = Lists()
    settings.LoadText """
items:
    - part_no:   Test
      descrip:   Some description
      price:     3.47
      quantity:  14
"""
    settings.items.Count |> should equal 1
    settings.items.[0].part_no |> should equal "Test"
    settings.items.[0].descrip |> should equal "Some description"
    settings.items.[0].quantity |> should equal 14 


[<Test>]
let ``Can load sequence of maps (multiple items)``() =
    let settings = Lists()
    settings.LoadText """
items:
    - part_no:   Test
      descrip:   Some description
      price:     3.47
      quantity:  14
    - part_no:   A4786
      descrip:   Water Bucket (Filled)
      price:     1.47
      quantity:  4

    - part_no:   E1628
      descrip:   High Heeled "Ruby" Slippers
      size:      8
      price:     100.27
      quantity:  1
"""
    settings.items.Count |> should equal 3
    settings.items.[0].part_no |> should equal "Test"
    settings.items.[0].descrip |> should equal "Some description"
    settings.items.[0].quantity |> should equal 14
    
    settings.items.[2].part_no |> should equal "E1628"
    settings.items.[2].descrip |> should equal "High Heeled \"Ruby\" Slippers"
    settings.items.[2].quantity |> should equal 1

[<Ignore>]
[<Test>]
let ``Check that list defaults are OK``() =
    let settings = Lists()
    settings.items.Count |> should equal 2
    settings.Archive.Count |> should equal 3