module FSharp.Configuration.Tests.YamlTests

open FSharp.Configuration
open NUnit.Framework
open FsUnit
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

[<Test>] 
let ``Can save a settings file to a specified location``() =
    let settings = Settings()
    settings.DB.NumberOfDeadlockRepeats <- 11
    settings.DB.DefaultTimeout <- System.TimeSpan.FromMinutes 6.
    settings.Save("SettingsModifed.yaml")

    File.ReadAllText "SettingsModifed.yaml"
    |> should equal (File.ReadAllText "Settings2.yaml")