module FSharp.Configuration.Tests.ResXTests

open FSharp.Configuration
open NUnit.Framework
open FsUnit

type ResX = ResXProvider<file="Resource1.resx">

[<Test>] 
let ``Can return a string from the resource file``() =   
    ResX.Resource1.HelloWorld.GetType() |> should equal typeof<string>   
    ResX.Resource1.HelloWorld |> should equal "Hello World"