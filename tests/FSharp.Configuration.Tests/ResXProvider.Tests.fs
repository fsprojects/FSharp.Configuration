module FSharp.Configuration.Tests.ResXTests

open FSharp.Configuration
open Xunit

type Resource1 = ResXProvider<"Resource1.resx">

[<Fact>] 
let ``Can return a string from the resource file``() =
    Assert.Equal<string>(Resource1.Greetings, "Hello World!")

[<Fact>] 
let ``Can return an image from the resource file``() =
    Assert.IsType<System.Drawing.Bitmap>(Resource1.Flowers) |> ignore
    Assert.NotNull Resource1.Flowers

[<Fact>] 
let ``Can return an int from the resource file``() =
    Assert.Equal(Resource1.Answer, 42)

[<Fact>] 
let ``Can return a text file from the resource file``() =
    Assert.Equal<string>(Resource1.TextFile, "Text")