module FSharp.Configuration.Tests.YamlTests

open FSharp.Configuration
open NUnit.Framework
open FsUnit

type Settings = Yaml<"Settings.yaml">
let db = Settings.DB_Type()

[<Test>] 
let ``Can return a string from the settings file``() =   
    db.ConnectionString.GetType() |> should equal typeof<string>   
    db.ConnectionString |> should equal "Data Source=server1;Initial Catalog=Database1;Integrated Security=SSPI;"

[<Test>] 
let ``Can return an int from the settings file``() =   
    db.NumberOfDeadlockRepeats.GetType() |> should equal typeof<int>   
    db.NumberOfDeadlockRepeats |> should equal 5

[<Test>] 
let ``Can return a TimeSpan from the settings file``() =   
    db.DefaultTimeout.GetType() |> should equal typeof<System.TimeSpan>   
    db.DefaultTimeout |> should equal (System.TimeSpan.FromMinutes 5.)


let mail = Settings.Mail_Type()

[<Test>] 
let ``Can return a list from the settings file``() =       
    mail.ErrorNotificationRecipients.Count |> should equal 2
    mail.ErrorNotificationRecipients.[0] |> should equal "user1@sample.com"
    mail.ErrorNotificationRecipients.[1] |> should equal "user2@sample.com"