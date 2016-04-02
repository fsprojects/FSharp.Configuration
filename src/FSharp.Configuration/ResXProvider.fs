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

let readFile (filePath: FilePath) : ResXDataNode list =
    use reader = new ResXResourceReader(filePath, UseResXDataNodes = true)
    reader
    |> Seq.cast
    |> Seq.map (fun (x: DictionaryEntry) -> x.Value :?> ResXDataNode)
    |> Seq.toList

let readValue (filePath: FilePath, name) =
    let entry = readFile filePath |> Seq.find (fun e -> name = e.Name)
    entry.GetValue Unchecked.defaultof<ITypeResolutionService>

/// Converts ResX entries to provided properties
let private toProperties (filePath: FilePath) : MemberInfo list =       
    readFile filePath
    |> List.map (fun node ->
        let name = node.Name
        let ty = node.GetValueTypeName Unchecked.defaultof<ITypeResolutionService> |> Type.GetType
        let resource = 
          ProvidedProperty(
            name, 
            ty, 
            IsStatic = true, 
            GetterCode = fun _ -> <@@ readValue(filePath, name) @@>)                          
        if not (String.IsNullOrEmpty node.Comment) then 
          resource.AddXmlDoc node.Comment
        resource :> MemberInfo)

/// Creates resource data type from specified ResX file
let private createResourceDataType filePath =
    let resourceName = Path.GetFileNameWithoutExtension filePath
    let data = ProvidedTypeDefinition (resourceName, baseType = Some typeof<obj>, HideObjectMethods = true)
    toProperties filePath |> Seq.iter data.AddMember
    data

/// Creates provided type from static resource file parameter
let private createResXProvider typeName filePath =
    let ty = ProvidedTypeDefinition (thisAssembly, rootNamespace, typeName, baseType = Some typeof<obj>)
    ty.AddMember (createResourceDataType filePath)
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