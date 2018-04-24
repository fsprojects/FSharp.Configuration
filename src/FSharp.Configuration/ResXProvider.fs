module FSharp.Configuration.ResXProvider

open System
open System.IO
open System.Reflection
open System.Resources
open System.ComponentModel.Design
open System.Collections
open System.Collections.Concurrent
open System.Runtime.Caching
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

/// Converts ResX entries to provided properties
let private toProperties (filePath: FilePath) resourceName : MemberInfo list =
    readFile filePath
    |> List.map (fun node ->
        let key = node.Name
        let ty = node.GetValueTypeName Unchecked.defaultof<ITypeResolutionService> |> Type.GetType
        let resource =
          ProvidedProperty(
            key,
            ty,
            isStatic = true,
            getterCode = fun _ -> <@@ readValue resourceName (Assembly.GetExecutingAssembly ()) key @@>)
        if not (String.IsNullOrEmpty node.Comment) then
          resource.AddXmlDoc node.Comment
        resource :> MemberInfo)

/// Creates provided type from static resource file parameter
let private createResXProvider typeName resourceName filePath =
    let ty = ProvidedTypeDefinition (thisAssembly, rootNamespace, typeName, baseType = Some typeof<obj>)
    ty.SetAttributes (ty.Attributes ||| TypeAttributes.Abstract ||| TypeAttributes.Sealed)
    toProperties filePath resourceName
    |> Seq.iter ty.AddMember
    ty

let inline private replace (oldChar:char) (newChar:char) (s:string) = s.Replace(oldChar, newChar)

let internal typedResources (context: Context) =
    let resXType = erasedType<obj> thisAssembly rootNamespace "ResXProvider" None
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
                    let resourceName =
                        Path.ChangeExtension (resourcePath, null)
                        |> replace '\\' '.'
                        |> replace '/' '.'
                    let providedType = createResXProvider typeName resourceName filePath
                    context.WatchFile filePath
                    providedType
                | _ -> failwith "unexpected parameter values")
            cache.GetOrAdd (typeName, value)))
    resXType