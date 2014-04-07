module FSharp.Configuration.Tests.ResXTests

open FSharp.Configuration
open NUnit.Framework
open FsUnit

type ResX = ResXProvider<file="Resource1.resx">

[<Test>] 
let ``Can return a string from the resource file``() =   
    ResX.Resource1.Greetings.GetType() |> should equal typeof<string>   
    ResX.Resource1.Greetings |> should equal "Hello World!"

[<Test>] 
let ``Can return an image from the resource file``() =
    ResX.Resource1.Flowers.GetType() |> should equal typeof<System.Drawing.Bitmap>
    ResX.Resource1.Flowers |> should not' (be Null)

[<Test>] 
let ``Can return an int from the resource file``() =
    ResX.Resource1.Answer.GetType() |> should equal typeof<int>
    ResX.Resource1.Answer |> should equal 42
