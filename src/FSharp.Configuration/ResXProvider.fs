module internal FSharp.Configuration.ResXProvider

open System
open System.IO
open System.Reflection
open Microsoft.FSharp.Core.CompilerServices
open Samples.FSharp.ProvidedTypes
open FSharp.Configuration.Helper

/// Creates resource data type from specified ResX file
let createResourceDataType (resXFilePath) =
    let resourceName = System.IO.Path.GetFileNameWithoutExtension(resXFilePath)
    let data = ProvidedTypeDefinition(resourceName, baseType=Some typeof<obj>, HideObjectMethods=true)
    let properties = ResX.toProperties resXFilePath
    for p in properties do data.AddMember(p)
    data

/// Creates provided type from static resource file parameter
let createResXProvider typeName resXFilePath =
    let ty = ProvidedTypeDefinition(thisAssembly, rootNamespace, typeName, baseType=Some typeof<obj>)
    let data = createResourceDataType resXFilePath
    ty.AddMember(data)
    ty  

let internal typedResources (ownerType:TypeProviderForNamespaces) (cfg:TypeProviderConfig) =
    let resXType = erasedType<obj> thisAssembly rootNamespace "ResXProvider"

    resXType.DefineStaticParameters(
        parameters=[ProvidedStaticParameter("file", typeof<string>)], 
        instantiationFunction = (fun typeName parameterValues ->
            match parameterValues with 
            | [| :? string as resourcePath|] ->
                let resXFilePath = Path.Combine(cfg.ResolutionFolder, resourcePath)
                if not(File.Exists resXFilePath) then invalidArg "file" "Resouce file not found"
                createResXProvider typeName resXFilePath
            | _ -> failwith "unexpected parameter values"))

    resXType