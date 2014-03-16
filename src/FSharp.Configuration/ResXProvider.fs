module FSharp.Configuration.ResXProvider

open System
open System.IO
open System.Reflection
open Microsoft.FSharp.Core.CompilerServices
open Samples.FSharp.ProvidedTypes
open FSharp.Configuration.Helper
open System.Resources
open System.ComponentModel.Design
open System.Collections
open Microsoft.FSharp.Quotations

let readValue (filepath: string,name) =
    use reader = new ResXResourceReader(filepath)
    reader.UseResXDataNodes <- true
    let entry = 
        reader
        |> Seq.cast
        |> Seq.map (fun (x:DictionaryEntry) -> x.Value :?> ResXDataNode)
        |> Seq.find (fun e -> name = e.Name)
    entry.GetValue(Unchecked.defaultof<ITypeResolutionService>)

/// Converts ResX entries to provided properties
let internal toProperties (resXFilePath:string) =       
    use reader = new ResXResourceReader(resXFilePath)
    reader.UseResXDataNodes <- true
    [for (entry:DictionaryEntry) in reader |> Seq.cast ->                   
        let node = entry.Value :?> ResXDataNode
        let name = node.Name
        let typ = node.GetValueTypeName(Unchecked.defaultof<ITypeResolutionService>) |> System.Type.GetType
        let comment = node.Comment
        let getter args = <@@ readValue(resXFilePath, name) @@>
        let resource = ProvidedProperty(name, typ, IsStatic=true, GetterCode=getter)                          
        if not(String.IsNullOrEmpty(comment)) then resource.AddXmlDoc(node.Comment)
        resource :> MemberInfo
    ]

/// Creates resource data type from specified ResX file
let internal createResourceDataType (resXFilePath) =
    let resourceName = Path.GetFileNameWithoutExtension(resXFilePath)
    let data = ProvidedTypeDefinition(resourceName, baseType=Some typeof<obj>, HideObjectMethods=true)
    let properties = toProperties resXFilePath
    for p in properties do data.AddMember(p)
    data

/// Creates provided type from static resource file parameter
let internal createResXProvider typeName resXFilePath =
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