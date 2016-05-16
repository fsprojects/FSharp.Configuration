module FSharp.Configuration.Tests.ResXTests

open FSharp.Configuration
open Xunit

type ResX = ResXProvider<file="Resource1.resx">

[<Fact>] 
let ``Can return a string from the resource file``() =   
    Assert.Equal<string>(ResX.Resource1.Greetings, "Hello World!")

[<Fact>] 
let ``Can return an image from the resource file``() =
    Assert.IsType<System.Drawing.Bitmap>(ResX.Resource1.Flowers) |> ignore
    Assert.NotNull ResX.Resource1.Flowers

[<Fact>] 
let ``Can return an int from the resource file``() =
    Assert.Equal(ResX.Resource1.Answer, 42)
