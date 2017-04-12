module FSharp.Configuration.Tests.ResXTests

open FSharp.Configuration
open Expecto

type Resource1 = ResXProvider<"Resource1.resx">

let [<Tests>] tests =
    testList "ResX Provider tests" [
        testCase "Can return a string from the resource file" (fun _ -> Expect.equal Resource1.Greetings "Hello World!" "value")
        
        testCase "Can return an image from the resource file" (fun _ -> 
            Expect.isNotNull Resource1.Flowers "Flowers"
            Expect.equal typeof<System.Drawing.Bitmap> (Resource1.Flowers.GetType()) "value")
        
        testCase "Can return an int from the resource file" (fun _ -> Expect.equal Resource1.Answer 42 "value")
        testCase "Can return a text file from the resource file" (fun _ -> Expect.equal Resource1.TextFile "Text" "value")
    ]