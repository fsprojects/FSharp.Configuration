module FSharp.Configuration.Tests.YamlTests

open FSharp.Configuration
open NUnit.Framework
open FsUnit

type Settings = Yaml<"Settings.yaml">
let settings = Settings()

[<Test>] 
let ``Can return a string from the settings file``() =   
    settings.DB.ConnectionString.GetType() |> should equal typeof<string>   
    settings.DB.ConnectionString |> should equal "Data Source=server1;Initial Catalog=Database1;Integrated Security=SSPI;"

[<Test>] 
let ``Can return an int from the settings file``() =   
    settings.DB.NumberOfDeadlockRepeats.GetType() |> should equal typeof<int>   
    settings.DB.NumberOfDeadlockRepeats |> should equal 5

[<Test>] 
let ``Can return a TimeSpan from the settings file``() =   
    settings.DB.DefaultTimeout.GetType() |> should equal typeof<System.TimeSpan>   
    settings.DB.DefaultTimeout |> should equal (System.TimeSpan.FromMinutes 5.)

[<Test>] 
let ``Can return a list from the settings file``() =       
    settings.Mail.ErrorNotificationRecipients.Count |> should equal 2
    settings.Mail.ErrorNotificationRecipients.[0] |> should equal "user1@sample.com"
    settings.Mail.ErrorNotificationRecipients.[1] |> should equal "user2@sample.com"