module internal FSharp.Configuration.ResXProvider

open System
open System.IO
open System.Reflection
open Microsoft.FSharp.Core.CompilerServices
open Samples.FSharp.ProvidedTypes
open FSharp.Configuration.Helper
open System.Resources
open System.ComponentModel.Design
open System.Collections

/// Converts ResX entries to provided properties
let toProperties (resXFilePath:string) =       
    use reader = new ResXResourceReader(resXFilePath)
    reader.UseResXDataNodes <- true
    [|for (entry:DictionaryEntry) in reader |> Seq.cast ->                   
        let node = entry.Value :?> ResXDataNode
        let name = string node.Name
        let value = node.GetValue(Unchecked.defaultof<ITypeResolutionService>)
        let comment = node.Comment
        let getter args = <@@ value @@>
        let resource = ProvidedProperty(name, typeof<string>, IsStatic=true, GetterCode=getter)                          
        if not(String.IsNullOrEmpty(comment)) then resource.AddXmlDoc(node.Comment)
        resource
    |]

/// Creates resource data type from specified ResX file
let createResourceDataType (resXFilePath) =
    let resourceName = Path.GetFileNameWithoutExtension(resXFilePath)
    let data = ProvidedTypeDefinition(resourceName, baseType=Some typeof<obj>, HideObjectMethods=true)
    let properties = toProperties resXFilePath
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
                let resXFilePath = findConfigFile cfg.ResolutionFolder resourcePath
                if not(File.Exists resXFilePath) then invalidArg "file" "Resouce file not found"
                createResXProvider typeName resXFilePath
            | _ -> failwith "unexpected parameter values"))

    resXType