module FSharp.Configuration.Tests.IniFile

open FSharp.Configuration
open NUnit.Framework
open FsUnit

type IniFileType = IniFile<"Sample.ini">

[<Test>] 
let ``Can return a string from the config file``() =   
    IniFileType.Section1.key2 |> should equal "stringValue"

[<Test>] 
let ``Can return an integer from the config file``() =
    IniFileType.Section1.key1.GetType() |> should equal typeof<int>
    IniFileType.Section1.key1 |> should equal 2

[<Test>] 
let ``Can return a double from the config file``() =
    IniFileType.Section2.key3.GetType() |> should equal typeof<float>
    IniFileType.Section2.key3 |> should equal 1.23

[<Test>] 
let ``Can return a boolean from the config file``() =
    IniFileType.Section2.key5.GetType() |> should equal typeof<bool>
    IniFileType.Section2.key5 |> should equal true

    IniFileType.Section2.key6.GetType() |> should equal typeof<bool>
    IniFileType.Section2.key6 |> should equal false

