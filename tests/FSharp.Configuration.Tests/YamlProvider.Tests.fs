module FSharp.Configuration.Tests.YamlTests

open FSharp.Configuration
open System
open System.IO
open Xunit

type Settings = YamlConfig<"Settings.yaml">

[<Fact>] 
let ``Can return a string from the settings file``() = 
    let settings = Settings()  
    Assert.Equal<string>(settings.DB.ConnectionString, "Data Source=server1;Initial Catalog=Database1;Integrated Security=SSPI;")

[<Fact>] 
let ``Can return an int from the settings file``() =   
    let settings = Settings()
    Assert.Equal(settings.DB.NumberOfDeadlockRepeats, 5)

[<Fact>] 
let ``Can return an int64 from the settings file``() =   
    let settings = Settings()
    Assert.Equal(settings.DB.Id, 21474836470L)

[<Fact>] 
let ``Can return an double from the settings file``() =   
    let settings = Settings()
    Assert.Equal(settings.JustStuff.SomeDoubleValue, 0.5)

[<Fact>] 
let ``Can return a TimeSpan from the settings file``() =   
    let settings = Settings()
    Assert.Equal(settings.DB.DefaultTimeout, System.TimeSpan.FromMinutes 5.)

[<Fact>]
let ``Can return a TimeStamp from the settings file``() =
    let settings = Settings()
    Assert.Equal(settings.JustStuff.SomeTimeStamp, System.DateTimeOffset(2001, 01, 01, 12, 34, 56, System.TimeSpan.Zero))

[<Fact>] 
let ``Can return a list from the settings file``() = 
    let settings = Settings()
    Assert.Equal(settings.Mail.ErrorNotificationRecipients.Count, 2)
    Assert.Equal<string>(settings.Mail.ErrorNotificationRecipients.[0], "user1@sample.com")
    Assert.Equal<string>(settings.Mail.ErrorNotificationRecipients.[1], "user2@sample.com")

[<Fact>] 
let ``Can write to a string in the settings file``() =
    let settings = Settings()
    settings.DB.ConnectionString <- "Data Source=server2"
    Assert.Equal<string>(settings.DB.ConnectionString, "Data Source=server2")

[<Fact>] 
let ``Can write to an int in the settings file``() =
    let settings = Settings()
    settings.DB.NumberOfDeadlockRepeats <- 6
    Assert.Equal(settings.DB.NumberOfDeadlockRepeats, 6)

[<Fact>] 
let ``Can write to an double in the settings file``() =
    let settings = Settings()
    settings.JustStuff.SomeDoubleValue <- 0.5
    Assert.Equal(settings.JustStuff.SomeDoubleValue, 0.5)

let private assertFilesAreEqual expected actual =
    let read file = (File.ReadAllText file).Replace("\r\n", "\n")
    Assert.Equal<string>(read expected, read actual)

[<Fact>] 
let ``Can save a settings file to a specified location``() =
    let settings = Settings()
    settings.DB.NumberOfDeadlockRepeats <- 11
    settings.DB.DefaultTimeout <- System.TimeSpan.FromMinutes 6.
    settings.JustStuff.SomeDoubleValue <- 0.5
    settings.Save "SettingsModifed.yaml"
    assertFilesAreEqual "SettingsModifed.yaml" "Settings2.yaml"

[<Fact>] 
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

[<Fact>] 
let ``Throws exception during saving if it was not loaded from a file and location is not specified``() =
    let settings = Settings()
    Assert.Throws<InvalidOperationException> (fun() -> settings.Save())
    
[<Fact>] 
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
    Assert.Equal<string>(settings.Mail.Smtp.Host, "smtp.sample.com*")
    Assert.Equal(settings.Mail.Smtp.Port, 4430)
    Assert.Equal<string>(settings.Mail.Smtp.User, "user1*")
    Assert.Equal<string>(settings.Mail.Smtp.Password, "pass1*")

    Assert.Equal<string>(settings.Mail.Pop3.Host, "pop3.sample.com*")
    Assert.Equal(settings.Mail.Pop3.Port, 3310)
    Assert.Equal<string>(settings.Mail.Pop3.User, "user2*")
    Assert.Equal<string>(settings.Mail.Pop3.Password, "pass2*")
    Assert.Equal(settings.Mail.Pop3.CheckPeriod, TimeSpan.FromMinutes 2.)

    Assert.Equal<_ list>(List.ofSeq settings.Mail.ErrorNotificationRecipients, ["user1@sample.com*"; "user2@sample.com*"; "user3@sample.com"])
    
    Assert.Equal<string>(settings.DB.ConnectionString, "Data Source=server1;Initial Catalog=Database1;Integrated Security=SSPI;*")
    Assert.Equal(settings.DB.NumberOfDeadlockRepeats, 50)
    Assert.Equal(settings.DB.DefaultTimeout, TimeSpan.FromMinutes 6.)

[<Fact>]
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
    Assert.Equal<string>(settings.Mail.Smtp.Host, "smtp.sample.com")
    Assert.Equal(settings.Mail.Smtp.Port, 4430)
    Assert.Equal<string>(settings.Mail.Smtp.User, "user1")
    Assert.Equal<string>(settings.Mail.Smtp.Password, "pass1")
   
    Assert.Equal<string>(settings.Mail.Pop3.Host, "pop3.sample.com")
    Assert.Equal(settings.Mail.Pop3.Port, 331)
    Assert.Equal<string>(settings.Mail.Pop3.User, "user2")
    Assert.Equal<string>(settings.Mail.Pop3.Password, "pass2")
    Assert.Equal(settings.Mail.Pop3.CheckPeriod, TimeSpan.FromMinutes 2.)

    Assert.Equal<_ list>(List.ofSeq settings.Mail.ErrorNotificationRecipients, ["user1@sample.com*"; "user2@sample.com*"; "user3@sample.com"])

    Assert.Equal<string>(settings.DB.ConnectionString, "Data Source=server1;Initial Catalog=Database1;Integrated Security=SSPI;")
    Assert.Equal(settings.DB.NumberOfDeadlockRepeats, 5)
    Assert.Equal(settings.DB.DefaultTimeout, TimeSpan.FromMinutes 5.)

[<Fact>]
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
    Assert.Equal(settings.Mail.Smtp.Port, 4430)

[<Fact>]
let ``Can load file and watch``() =
    let settings = Settings()
    let tempFile = Path.GetTempFileName()
    try
        File.Copy ("Settings.yaml", tempFile, overwrite = true)
        settings.LoadAndWatch tempFile |> ignore
    finally
        File.Delete tempFile

[<Fact>]
let ``Can load file and watch and detect errors``() =
    let settings = Settings()
    let tempFile = Path.GetTempFileName()
    let mutable err = false
    settings.Error.Add(fun _ -> err <- true) 
    try
        File.Copy ("Settings.yaml", tempFile, overwrite=true)
        use __ = settings.LoadAndWatch tempFile
        System.Threading.Thread.Sleep(800)
        let f = new FileStream(tempFile, FileMode.Append)
        let data = "<junk:>:asd:DF" |> System.Text.Encoding.ASCII.GetBytes
        f.Write(data, 0, data.Length)
        f.Flush()
        f.Dispose()
        System.Threading.Thread.Sleep(800)
        err,  true
    finally
        File.Delete tempFile

[<Fact>]
let ``Can load empty lists``() =
    let settings = Settings()
    settings.LoadText """
Mail:
  ErrorNotificationRecipients: []
"""
    settings.Mail.ErrorNotificationRecipients.Count,  0

type private Listener(event: IEvent<EventHandler, _>) =
    let events = ref 0
    do event.Add (fun _ -> incr events)
    member __.Events = !events

[<Fact>]
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
    Assert.Equal<seq<_>>(
        [rootListener.Events
         mailListener.Events
         pop3Listener.Events
         smtpListener.Events
         dbListener.Events],  
        [1; 1; 1; 0; 0])
    
[<Fact>]
let ``Does not raise duplicates of parent Changed events even though several children changed``() =
    let settings = Settings()
    let rootListener = Listener settings.Changed
    let mailListener = Listener settings.Mail.Changed
    let pop3Listener = Listener settings.Mail.Pop3.Changed
    let smtpListener = Listener settings.Mail.Smtp.Changed

    settings.LoadText """
Mail:
  Smtp:
    Port: 4430
  Pop3:
    CheckPeriod: 00:02:00
"""
    Assert.Equal<seq<_>>(
        [rootListener.Events
         mailListener.Events
         pop3Listener.Events
         smtpListener.Events],
        [1; 1; 1; 1])

type Lists = YamlConfig<"Lists.yaml">

[<Fact>]
let ``Can load sequence of maps (single item)``() =
    let settings = Lists()
    settings.LoadText """
items:
    - part_no:   Test
      descrip:   Some description
      price:     347
      quantity:  14
"""
    Assert.Equal(settings.items.Count, 1)
    Assert.Equal<string>(settings.items.[0].part_no, "Test")
    Assert.Equal<string>(settings.items.[0].descrip, "Some description")
    Assert.Equal(settings.items.[0].quantity, 14)

[<Fact>]
let ``Can load sequence of maps (multiple items)``() =
    let settings = Lists()
    settings.LoadText """
items:
    - part_no:   Test
      descrip:   Some description
      price:     347
      quantity:  14
    - part_no:   A4786
      descrip:   Water Bucket (Filled)
      price:     147
      quantity:  4

    - part_no:   E1628
      descrip:   High Heeled "Ruby" Slippers
      size:      8
      price:     10027
      quantity:  1
"""
    Assert.Equal(settings.items.Count, 3)
    Assert.Equal<string>(settings.items.[0].part_no, "Test")
    Assert.Equal<string>(settings.items.[0].descrip, "Some description")
    Assert.Equal(settings.items.[0].quantity, 14)
    
    Assert.Equal<string>(settings.items.[2].part_no, "E1628")
    Assert.Equal<string>(settings.items.[2].descrip, "High Heeled \"Ruby\" Slippers")
    Assert.Equal(settings.items.[2].quantity, 1)

[<Fact>]
let ``Can load nested lists``() =
    let settings = Lists()
    settings.LoadText """
Fix82:
  id: "myApp"
  constraints:
    -
      - "attribute"
      - "OPERATOR"
      - "value"
    -
      - "field"
      - "OP"
  labels:
    environment: "staging"
"""
    Assert.Equal(settings.Fix82.constraints.Count, 2)
    Assert.Equal(settings.Fix82.constraints.[0].Count, 3)
    Assert.Equal<string>(settings.Fix82.constraints.[0].[0], "attribute")
    Assert.Equal<string>(settings.Fix82.constraints.[0].[1], "OPERATOR")
    Assert.Equal<string>(settings.Fix82.constraints.[0].[2], "value")
    Assert.Equal(settings.Fix82.constraints.[1].Count, 2)
    Assert.Equal<string>(settings.Fix82.constraints.[1].[0], "field")
    Assert.Equal<string>(settings.Fix82.constraints.[1].[1], "OP")

//[<Fact>]
let ``Check that list defaults are OK``() =
    let settings = Lists()
    Assert.Equal(settings.items.Count, 2)
    Assert.Equal(settings.Archive.Count, 3)

type NumericKeys = YamlConfig<YamlText = 
"""
1: 10
2: 20
3a: foo
""">

[<Fact>]
let ``Numeric map keys are OK``() =
    let sut = NumericKeys()
    Assert.Equal(sut.``1``, 10)
    Assert.Equal(sut.``2``, 20)
    Assert.Equal<string>(sut.``3a``, "foo")


type MixedTypes = YamlConfig<YamlText = 
"""
Items:
    - Item: asdf
    - Item: http://fsharp.org
    - Item: 14 
""">

[<Fact>]
let ``Mixed types are read as strings``() =
    let mt = MixedTypes()
    mt.LoadText("""
Items:
    - Item: asdf
    - Item: http://fsharp.org
    - Item: 14 
    """)
    Assert.Equal<string>(mt.Items.[0].Item, "asdf")
    Assert.Equal<string>(mt.Items.[1].Item, "http://fsharp.org/")
    Assert.Equal<string>(mt.Items.[2].Item, "14")