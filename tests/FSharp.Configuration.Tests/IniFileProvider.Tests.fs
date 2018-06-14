module FSharp.Configuration.Tests.IniFile

open FSharp.Configuration
open Expecto

type IniFileType = IniFile<"Sample.ini">

let [<Tests>] tests =
    testList "Ini File Provider tests" [
        testCase "Can return a string from the config file" (fun _ ->
            Expect.equal IniFileType.Section1.key2 "stringValue" "value")

        testCase "Can return an integer from the config file" (fun _ ->
            Expect.equal IniFileType.Section1.key1 2 "value")

        testCase "Can return a double from the config file" (fun _ ->
            Expect.equal IniFileType.Section2.key3 1.23 "value")

        testCase "Can return a boolean from the config file" (fun _ ->
            Expect.isTrue IniFileType.Section2.key5 "key5"
            Expect.isFalse IniFileType.Section2.key6 "key6")

        testCase "Can return a semicolon in the value" (fun _ ->
            Expect.equal IniFileType.Section2.key8 @"Data Source=localhost\sqlexpress;Initial Catalog=DB with Spaces" "value")

        testCase "Trailing spaces are not significant" (fun _ ->
            Expect.equal IniFileType.Section2.key9 @"Trailing spaces     are not significant" "value")
    ]
