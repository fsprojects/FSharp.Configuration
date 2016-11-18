module FSharp.Configuration.Tests.AppSettingsTests

open System
open FSharp.Configuration
open Xunit

type Settings = AppSettings<"app.config">

[<Fact>] 
let ``Can return a string from the config file``() =   
    Assert.Equal<string>(Settings.Test2, "Some Test Value 5")

[<Fact>] 
let ``Can return an integer from the config file``() =
    Assert.Equal(Settings.TestInt, 102)

[<Fact>] 
let ``Can return a double from the config file``() =
    Assert.Equal(Settings.TestDouble, 10.01)

[<Fact>] 
let ``Can return a boolean from the config file``() =
    Assert.True Settings.TestBool

[<Fact>] 
let ``Can return a TimeSpan from the config file``() =
    Assert.Equal(Settings.TestTimeSpan, TimeSpan.Parse "2.01:02:03.444")

[<Fact>]
let ``Can return a DateTime from the config file``() =
    Assert.Equal(Settings.TestDateTime.ToUniversalTime(), DateTime (2014, 2, 1, 3, 4, 5, 777))

[<Fact>]
let ``Can return a Uri from the config file``() =
    Assert.Equal(Settings.TestUri, Uri "http://fsharp.org")

[<Fact>] 
let ``Can return a connection string from the config file``() =   
    Assert.Equal<string>(Settings.ConnectionStrings.Test1, "Server=myServerAddress;Database=myDataBase;Trusted_Connection=True;")

[<Fact>]
let ``Can return a guid from the config file``() =
    Assert.Equal(Settings.TestGuid, Guid.Parse "{7B7EB384-FEBA-4409-B560-66FF63F1E8D0}")
     
[<Fact>] 
let ``Can read multiple connection strings from the config file``() =   
    Assert.NotEqual<string>(Settings.ConnectionStrings.Test1, Settings.ConnectionStrings.Test2)

[<Literal>] 
let fakeConfig = __SOURCE_DIRECTORY__ + @"/../../packages/FAKE/tools/FAKE.Deploy.exe.config"
type FakeSettings = AppSettings<fakeConfig>

[<Fact>] 
let ``Can read different configuration file``() =
    let exePath = [| __SOURCE_DIRECTORY__; ".."; ".."; "packages"; "FAKE"; "tools"; "FAKE.Deploy.exe" |]
                  |> System.IO.Path.Combine |> System.IO.Path.GetFullPath
    FakeSettings.SelectExecutableFile exePath

#if INTERACTIVE //Travis can't handle fakeConfig-directory
    FakeSettings.ServerName =! "localhost"
#endif
