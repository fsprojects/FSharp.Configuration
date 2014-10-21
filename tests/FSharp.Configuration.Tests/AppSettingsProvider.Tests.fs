module FSharp.Configuration.Tests.AppSettingsTests

open System
open FSharp.Configuration
open NUnit.Framework
open FsUnit

type Settings = AppSettings<"app.config">

[<Test>] 
let ``Can return a string from the config file``() =   
    Settings.Test2.GetType() |> should equal typeof<string>   
    Settings.Test2 |> should equal "Some Test Value 5"

[<Test>] 
let ``Can return an integer from the config file``() =
    Settings.TestInt.GetType() |> should equal typeof<int>
    Settings.TestInt |> should equal 102

[<Test>] 
let ``Can return a double from the config file``() =
    Settings.TestDouble.GetType() |> should equal typeof<float>
    Settings.TestDouble |> should equal 10.01

[<Test>] 
let ``Can return a boolean from the config file``() =
    Settings.TestBool.GetType() |> should equal typeof<bool>
    Settings.TestBool |> should equal true

[<Test>] 
let ``Can return a TimeSpan from the config file``() =
    Settings.TestTimeSpan.GetType() |> should equal typeof<TimeSpan>
    Settings.TestTimeSpan |> should equal (TimeSpan.Parse "2.01:02:03.444")

[<Test>]
let ``Cat return a DateTime from the config file``() =
    Settings.TestDateTime.GetType() |> should equal typeof<DateTime>
    Settings.TestDateTime.ToUniversalTime() |> should equal (DateTime (2014, 2, 1, 3, 4, 5, 777))

[<Test>] 
let ``Can return a connection string from the config file``() =   
    Settings.ConnectionStrings.Test1 |> should equal "Server=myServerAddress;Database=myDataBase;Trusted_Connection=True;"

[<Test>] 
let ``Can read multiple connection strings from the config file``() =   
    Settings.ConnectionStrings.Test1 |> should not' (equal Settings.ConnectionStrings.Test2)

