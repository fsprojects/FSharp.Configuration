module FSharp.Configuration.Tests.YamlTests

open FSharp.Configuration
open System
open System.IO
open Expecto

type Settings = YamlConfig<"Settings.yaml">

let private assertFilesAreEqual expected actual =
    let read file = (File.ReadAllText file).Replace("\r\n", "\n")
    Expect.equal (read expected) (read actual) "files"

type private Listener(event: IEvent<EventHandler, _>) =
    let events = ref 0
    do event.Add (fun _ -> incr events)
    member __.Events = !events

let [<Tests>] tests = 
    testList "Yaml Config Provider tests" [
        testCase "Can return a string from the settings file" (fun _ ->
            let settings = Settings()  
            Expect.equal settings.DB.ConnectionString "Data Source=server1;Initial Catalog=Database1;Integrated Security=SSPI;" "value")
        
        testCase "Can return an int from the settings file" (fun _ ->
            let settings = Settings()
            Expect.equal settings.DB.NumberOfDeadlockRepeats 5 "value")
        
        testCase "Can return an int64 from the settings file" (fun _ ->   
            let settings = Settings()
            Expect.equal settings.DB.Id 21474836470L "value")
        
        testCase "Can return an double from the settings file" (fun _ ->   
            let settings = Settings()
            Expect.equal settings.JustStuff.SomeDoubleValue 0.5 "value")
        
        testCase "Can return a TimeSpan from the settings file" (fun _ ->   
            let settings = Settings()
            Expect.equal settings.DB.DefaultTimeout (TimeSpan.FromMinutes 5.0) "value")
        
        testCase "Can return a guid from the settings file" (fun _ ->
            let settings = Settings()
            Expect.equal settings.JustStuff.SomeGuid (Guid.Parse "{7B7EB384-FEBA-4409-B560-66FF63F1E8D0}")  "value")
        
        testCase "Can return a different guid from the settings file" (fun _ ->
            let settings = Settings()
            Expect.equal settings.JustStuff.DifferentGuid (Guid.Parse "9d165087-9b74-4313-ab90-89be897d3d93") "value")
        
        testCase "Can return a list from the settings file" (fun _ -> 
            let settings = Settings()
            Expect.equal settings.Mail.ErrorNotificationRecipients.Count 2 "value"
            Expect.equal settings.Mail.ErrorNotificationRecipients.[0] "user1@sample.com" "value"
            Expect.equal settings.Mail.ErrorNotificationRecipients.[1] "user2@sample.com" "value")
        
        testCase "Can write to a string in the settings file" (fun _ ->
            let settings = Settings()
            settings.DB.ConnectionString <- "Data Source=server2"
            Expect.equal settings.DB.ConnectionString "Data Source=server2" "value")
        
        testCase "Can write to an int in the settings file" (fun _ ->
            let settings = Settings()
            settings.DB.NumberOfDeadlockRepeats <- 6
            Expect.equal settings.DB.NumberOfDeadlockRepeats 6 "value")
        
        testCase "Can write to an double in the settings file" (fun _ ->
            let settings = Settings()
            settings.JustStuff.SomeDoubleValue <- 0.5
            Expect.equal settings.JustStuff.SomeDoubleValue 0.5 "value")
        
        testCase "Can write to a guid in the settings file" (fun _ ->
            let settings = Settings()
            let guid = Guid.NewGuid()
            settings.JustStuff.SomeGuid <- guid
            Expect.equal settings.JustStuff.SomeGuid guid "value")
        
        testCase "Can save a settings file to a specified location" (fun _ ->
            let settings = Settings()
            settings.DB.NumberOfDeadlockRepeats <- 11
            settings.DB.DefaultTimeout <- System.TimeSpan.FromMinutes 6.
            settings.JustStuff.SomeDoubleValue <- 0.5
            settings.Save "SettingsModifed.yaml"
            assertFilesAreEqual "SettingsModifed.yaml" "Settings2.yaml")
        
        testCase "Can save settings to the file it was loaded from last time" (fun _ ->
            let settings = Settings()
            let tempFile = Path.GetTempFileName()
            try
                File.Copy ("Settings.yaml", tempFile, overwrite=true)
                settings.Load tempFile
                settings.DB.NumberOfDeadlockRepeats <- 11
                settings.DB.DefaultTimeout <- System.TimeSpan.FromMinutes 6.
                settings.Save()
                assertFilesAreEqual tempFile "Settings2.yaml"
            finally File.Delete tempFile)
        
        testCase "Throws exception during saving if it was not loaded from a file and location is not specified" (fun _ ->
            let settings = Settings()
            Expect.throwsT<InvalidOperationException> (fun() -> settings.Save()) "throws")
            
        testCase "Can loads full settings" (fun _ ->
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
            Expect.equal settings.Mail.Smtp.Host "smtp.sample.com*" "value"
            Expect.equal settings.Mail.Smtp.Port 4430 "value"
            Expect.equal settings.Mail.Smtp.User "user1*" "value"
            Expect.equal settings.Mail.Smtp.Password "pass1*" "value"
        
            Expect.equal settings.Mail.Pop3.Host "pop3.sample.com*" "value"
            Expect.equal settings.Mail.Pop3.Port 3310 "value"
            Expect.equal settings.Mail.Pop3.User "user2*" "value"
            Expect.equal settings.Mail.Pop3.Password "pass2*" "value"
            Expect.equal settings.Mail.Pop3.CheckPeriod (TimeSpan.FromMinutes 2.0) "value"
        
            Expect.equal (List.ofSeq settings.Mail.ErrorNotificationRecipients) ["user1@sample.com*"; "user2@sample.com*"; "user3@sample.com"]  "value"
            Expect.equal settings.DB.ConnectionString "Data Source=server1;Initial Catalog=Database1;Integrated Security=SSPI;*" "value"
            Expect.equal settings.DB.NumberOfDeadlockRepeats 50 "value"
            Expect.equal settings.DB.DefaultTimeout (TimeSpan.FromMinutes 6.0) "value")
        
        testCase "Can load partial settings" (fun _ ->
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
            Expect.equal settings.Mail.Smtp.Host "smtp.sample.com" "value"
            Expect.equal settings.Mail.Smtp.Port 4430 "value"
            Expect.equal settings.Mail.Smtp.User "user1" "value"
            Expect.equal settings.Mail.Smtp.Password "pass1" "value"
           
            Expect.equal settings.Mail.Pop3.Host "pop3.sample.com" "value"
            Expect.equal settings.Mail.Pop3.Port 331 "value"
            Expect.equal settings.Mail.Pop3.User "user2" "value"
            Expect.equal settings.Mail.Pop3.Password "pass2" "value"
            Expect.equal settings.Mail.Pop3.CheckPeriod (TimeSpan.FromMinutes 2.0) "value"
        
            Expect.sequenceEqual settings.Mail.ErrorNotificationRecipients ["user1@sample.com*"; "user2@sample.com*"; "user3@sample.com"] "value"
        
            Expect.equal settings.DB.ConnectionString "Data Source=server1;Initial Catalog=Database1;Integrated Security=SSPI;" "value"
            Expect.equal settings.DB.NumberOfDeadlockRepeats 5 "value"
            Expect.equal settings.DB.DefaultTimeout (TimeSpan.FromMinutes 5.0) "value")
        
        testCase "Can load settings containing unknown nodes" (fun _ ->
            let settings = Settings()
            settings.LoadText """
        Mail:
          Smtp:
            Port: 4430
            NestedUnknown: value1
        TopLevelUnknown:
          Value: 1
        
        """
            Expect.equal settings.Mail.Smtp.Port 4430 "value")
        
        testCase "Can load file and watch" (fun _ ->
            let settings = Settings()
            let tempFile = Path.GetTempFileName()
            try
                File.Copy ("Settings.yaml", tempFile, overwrite = true)
                settings.LoadAndWatch tempFile |> ignore
            finally
                File.Delete tempFile)
        
        testCase "Can load file and watch and detect errors" (fun _ ->
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
                Threading.Thread.Sleep(800)
                Expect.isTrue err "error event raised"
            finally
                File.Delete tempFile)
        
        testCase "Can load empty lists" (fun _ ->
            let settings = Settings()
            settings.LoadText """
        Mail:
          ErrorNotificationRecipients: []
        """
            Expect.isEmpty settings.Mail.ErrorNotificationRecipients "value")
        
        testCase "Raises Changed events" (fun _ ->
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
            Expect.sequenceEqual
                [rootListener.Events
                 mailListener.Events
                 pop3Listener.Events
                 smtpListener.Events
                 dbListener.Events]  
                [1; 1; 1; 0; 0]  "value")
            
        testCase "Does not raise duplicates of parent Changed events even though several children changed" (fun _ ->
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
            Expect.sequenceEqual
                [rootListener.Events
                 mailListener.Events
                 pop3Listener.Events
                 smtpListener.Events]
                [1; 1; 1; 1] "value")
    ]

type Lists = YamlConfig<"Lists.yaml">

let [<Tests>] listTests =
    testList "Yaml Config Provider tests - lists" (Seq.toList <|
        testFixture (fun f () -> f (Lists())) [
            "Can load sequence of maps (single item)", fun settings ->
                settings.LoadText """
            items:
                - part_no:   Test
                  descrip:   Some description
                  price:     347
                  quantity:  14
            """
                Expect.equal settings.items.Count 1 "value"
                Expect.equal settings.items.[0].part_no "Test" "value"
                Expect.equal settings.items.[0].descrip "Some description" "value"
                Expect.equal settings.items.[0].quantity 14 "value"
        
            "Can load sequence of maps (multiple items)", fun settings ->
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
                Expect.equal settings.items.Count 3 "value"
                Expect.equal settings.items.[0].part_no "Test" "value"
                Expect.equal settings.items.[0].descrip "Some description" "value"
                Expect.equal settings.items.[0].quantity 14 "value"
                
                Expect.equal settings.items.[2].part_no "E1628" "value"
                Expect.equal settings.items.[2].descrip "High Heeled \"Ruby\" Slippers" "value"
                Expect.equal settings.items.[2].quantity 1 "value"
        
            "Can load nested lists", fun settings ->
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
                Expect.equal settings.Fix82.constraints.Count 2 "value"
                Expect.equal settings.Fix82.constraints.[0].Count 3 "value"
                Expect.equal settings.Fix82.constraints.[0].[0] "attribute" "value"
                Expect.equal settings.Fix82.constraints.[0].[1] "OPERATOR" "value"
                Expect.equal settings.Fix82.constraints.[0].[2] "value" "value"
                Expect.equal settings.Fix82.constraints.[1].Count 2 "value"
                Expect.equal settings.Fix82.constraints.[1].[0] "field" "value"
                Expect.equal settings.Fix82.constraints.[1].[1] "OP" "value"
        
            //"Check that list defaults are OK", fun settings ->
            //    Expect.equal settings.items.Count 2 "value"
            //    Expect.equal settings.Archive.Count 3 "value"
        ])

type NumericKeys = YamlConfig<YamlText = 
            """
            1: 10
            2: 20
            3a: foo
            """>
            
type MixedTypes = YamlConfig<YamlText = 
"""
Items:
    - Item: asdf
    - Item: http://fsharp.org
    - Item: 14 
""">

let [<Tests>] numericTests =
    testList "Yaml Config Provider - numeric tests" [
        testCase "Numeric map keys are OK" (fun _ ->
            let sut = NumericKeys()
            Expect.equal sut.``1`` 10 "value"
            Expect.equal sut.``2`` 20 "value"
            Expect.equal sut.``3a`` "foo" "value")
        
        testCase "Mixed types are read as strings" (fun _ ->
            let mt = MixedTypes()
            mt.LoadText("""
        Items:
            - Item: asdf
            - Item: http://fsharp.org
            - Item: 14 
            """)
            Expect.equal mt.Items.[0].Item "asdf" "value"
            Expect.equal mt.Items.[1].Item "http://fsharp.org/" "value"
            Expect.equal mt.Items.[2].Item "14" "value")
    ]