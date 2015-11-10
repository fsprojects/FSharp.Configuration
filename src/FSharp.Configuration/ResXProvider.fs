module FSharp.Configuration.ResXProvider

open System
open System.IO
open System.Reflection
open ProviderImplementation.ProvidedTypes
open FSharp.Configuration.Helper
open System.Resources
open System.ComponentModel.Design
open System.Collections
open System.Runtime.Caching

let readValue (filePath: FilePath, name) =
    use reader = new ResXResourceReader(filePath)
    reader.UseResXDataNodes <- true
    let entry =
        reader
        |> Seq.cast
        |> Seq.map (fun (x: DictionaryEntry) -> x.Value :?> ResXDataNode)
        |> Seq.find (fun e -> name = e.Name)
    entry.GetValue(Unchecked.defaultof<ITypeResolutionService>)

/// Converts ResX entries to provided properties
let internal toProperties (resXFilePath:string) =
    use reader = new ResXResourceReader(resXFilePath)
    reader.UseResXDataNodes <- true
    [ for (entry:DictionaryEntry) in reader |> Seq.cast ->
        let node = entry.Value :?> ResXDataNode
        let name = node.Name
        let typ = node.GetValueTypeName(Unchecked.defaultof<ITypeResolutionService>) |> System.Type.GetType
        let comment = node.Comment
        let getter _args = <@@ readValue(resXFilePath, name) @@>
        let resource = ProvidedProperty(name, typ, IsStatic=true, GetterCode=getter)
        if not(String.IsNullOrEmpty(comment)) then resource.AddXmlDoc(node.Comment)
        resource :> MemberInfo ]

/// Creates resource data type from specified ResX file
let internal createResourceDataType (resXFilePath) =
    let resourceName = Path.GetFileNameWithoutExtension(resXFilePath)
    let data = ProvidedTypeDefinition(resourceName, baseType=Some typeof<obj>, HideObjectMethods=true)
    let properties = toProperties resXFilePath
    properties |> Seq.iter data.AddMember
    data

/// Creates provided type from static resource file parameter
let internal createResXProvider typeName resXFilePath =
    let ty = ProvidedTypeDefinition(thisAssembly, rootNamespace, typeName, baseType=Some typeof<obj>)
    ty.AddMember (createResourceDataType resXFilePath)
    ty

let internal typedResources (context: Context) =
    let resXType = erasedType<obj> thisAssembly rootNamespace "ResXProvider"
    let cache = new MemoryCache("ResXProvider")
    context.AddDisposable cache

    resXType.DefineStaticParameters(
        parameters = [ ProvidedStaticParameter ("file", typeof<string>) ],
        instantiationFunction = (fun typeName parameterValues ->
            let value = lazy (
                match parameterValues with
                | [| :? string as resourcePath|] ->
                    let filePath = findConfigFile context.ResolutionFolder resourcePath
                    if not (File.Exists filePath) then invalidArg "file" "Resouce file not found"
                    let providedType = createResXProvider typeName filePath
                    context.WatchFile filePath
                    providedType
                | _ -> failwith "unexpected parameter values")
            cache.GetOrAdd (typeName, value)))
    resXType
