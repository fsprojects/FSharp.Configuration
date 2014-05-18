module FSharp.Configuration.Tests.IniFile

open FSharp.Configuration
open NUnit.Framework
open FsUnit

type IniFileType = IniFile<"Sample.ini">

[<Test>] 
let ``Can return a string from the config file``() =   
    IniFileType.Section1.key1 |> should equal "value1"

[<Test>] 
let ``Can return an integer from the config file``() =
    IniFileType.TestInt.GetType() |> should equal typeof<int>
    IniFileType.TestInt |> should equal 102

[<Test>] 
let ``Can return a double from the config file``() =
    IniFileType.TestDouble.GetType() |> should equal typeof<float>
    IniFileType.TestDouble |> should equal 10.01

[<Test>] 
let ``Can return a boolean from the config file``() =
    IniFileType.TestBool.GetType() |> should equal typeof<bool>
    IniFileType.TestBool |> should equal true

