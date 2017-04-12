open Expecto
open System

[<EntryPoint>]
let main args =
    let errorCode = runTestsInAssembly defaultConfig args
    Console.ReadKey() |> ignore
    errorCode

