module FSharp.Configuration.Tests.AppSettingsTests

open System
open FSharp.Configuration
open Expecto

type Settings = AppSettings<"app.config">

let [<Tests>] tests = 
    testList "App Settings Provider tests" [
        testCase "Can return a string from the config file" (fun _ -> Expect.equal Settings.Test2 "Some Test Value 5" "value")
        testCase "Can return an integer from the config file" (fun _ -> Expect.equal Settings.TestInt 102 "value")
        testCase "Can return a double from the config file" (fun _ -> Expect.equal Settings.TestDouble 10.01 "value")
        testCase "Can return a boolean from the config file" (fun _ -> Expect.isTrue Settings.TestBool "value")
        testCase "Can return a TimeSpan from the config file" (fun _ -> Expect.equal Settings.TestTimeSpan (TimeSpan.Parse "2.01:02:03.444") "value")
        testCase "Can return a DateTime from the config file" (fun _ -> Expect.equal (Settings.TestDateTime.ToUniversalTime()) (DateTime (2014, 2, 1, 3, 4, 5, 777)) "value")
        testCase "Can return a Uri from the config file" (fun _ -> Expect.equal Settings.TestUri (Uri "http://fsharp.org") "value")
        
        testCase "Can return a connection string from the config file" (fun _ -> 
            Expect.equal Settings.ConnectionStrings.Test1 "Server=myServerAddress;Database=myDataBase;Trusted_Connection=True;" "value")
        
        testCase "Can return a guid from the config file" (fun _ -> Expect.equal Settings.TestGuid (Guid.Parse "{7B7EB384-FEBA-4409-B560-66FF63F1E8D0}") "value")
        testCase "Can read multiple connection strings from the config file" (fun _ -> Expect.notEqual Settings.ConnectionStrings.Test1 Settings.ConnectionStrings.Test2 "value")
    ]

[<Literal>] 
let fakeConfig = __SOURCE_DIRECTORY__ + @"/../../packages/FAKE/tools/FAKE.Deploy.exe.config"
type FakeSettings = AppSettings<fakeConfig>
    
let [<Tests>] test =
    testCase "Can read different configuration file" (fun _ -> 
        [| __SOURCE_DIRECTORY__; ".."; ".."; "packages"; "FAKE"; "tools"; "FAKE.Deploy.exe" |]
        |> System.IO.Path.Combine |> System.IO.Path.GetFullPath
        |> FakeSettings.SelectExecutableFile
    
    #if INTERACTIVE //Travis can't handle fakeConfig-directory
        FakeSettings.ServerName =! "localhost"
    #endif
    )
    