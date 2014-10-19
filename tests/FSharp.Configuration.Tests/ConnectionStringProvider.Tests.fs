module FSharp.Configuration.Tests.ConnectionStringProviderTests

open FSharp.Configuration
open NUnit.Framework
open FsUnit

type Settings = ConnectionStrings<"app.config">

[<Test>] 
let ``Can return a connection string from the config file``() =   
    Settings.Test1 |> should equal "Server=myServerAddress;Database=myDataBase;Trusted_Connection=True;"

