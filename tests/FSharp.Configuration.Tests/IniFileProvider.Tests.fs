module FSharp.Configuration.Tests.IniFile

open FSharp.Configuration
open Xunit

type IniFileType = IniFile<"Sample.ini">

[<Fact>] 
let ``Can return a string from the config file``() =   
    Assert.Equal<string>(IniFileType.Section1.key2, "stringValue")

[<Fact>] 
let ``Can return an integer from the config file``() =
    Assert.Equal(IniFileType.Section1.key1, 2)

[<Fact>] 
let ``Can return a double from the config file``() =
    Assert.Equal(IniFileType.Section2.key3, 1.23)

[<Fact>] 
let ``Can return a boolean from the config file``() =
    Assert.True IniFileType.Section2.key5
    Assert.False IniFileType.Section2.key6

