module FSharp.Configuration.ResX

#if ENABLE_RESXPROVIDER

open System
open System.IO
open System.Reflection
open System.Resources
open System.ComponentModel.Design
open System.Collections
open System.Collections.Concurrent
open ProviderImplementation.ProvidedTypes
open FSharp.Configuration.Helper

let readFile (filePath: FilePath) : ResXDataNode list =
    use reader = new ResXResourceReader(filePath, UseResXDataNodes = true)
    reader
    |> Seq.cast
    |> Seq.map (fun (x: DictionaryEntry) -> x.Value :?> ResXDataNode)
    |> Seq.toList

let resourceManCache = ConcurrentDictionary<string * Assembly, ResourceManager> ()

let readValue resourceName assembly key =
    let resourceMan = resourceManCache.GetOrAdd ((resourceName, assembly),
                        fun _ -> ResourceManager (resourceName, assembly))
    downcast (resourceMan.GetObject key)

#endif
