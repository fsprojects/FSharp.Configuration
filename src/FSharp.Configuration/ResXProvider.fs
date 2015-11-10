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
open System.Globalization
open System.Collections.Generic

type private ResourceFileInfo = {
    Directory: string
    Filename: string
}
with
    static member Create(filePath) =
        { Directory = Path.GetDirectoryName(filePath)
          Filename = Path.GetFileName(filePath) }
    member this.CompileTimePath = findConfigFile this.Directory this.Filename
    member this.ResourceName = Path.GetFileNameWithoutExtension(this.Filename)

let private culture = CultureInfo.CurrentCulture

let private managers = Dictionary<_, _>()

let readValue (resourceName, name) =
    let asm = Assembly.GetCallingAssembly()
    match managers.TryGetValue((resourceName, asm)) with
    | false, _ ->
        let manager = ResourceManager(resourceName, asm)
        managers.[(resourceName, asm)] <- manager
        manager.GetObject(name, culture)
    | true, manager -> manager.GetObject(name, culture)

/// Converts ResX entries to provided properties
let private toProperties (resXFilePath: ResourceFileInfo) =
    use reader = new ResXResourceReader(resXFilePath.CompileTimePath)
    reader.UseResXDataNodes <- true
    [ for (entry:DictionaryEntry) in reader |> Seq.cast ->
        let node = entry.Value :?> ResXDataNode
        let name = node.Name
        let typ = node.GetValueTypeName(Unchecked.defaultof<ITypeResolutionService>) |> System.Type.GetType
        let comment = node.Comment
        let getter _args =
            let resourceName = resXFilePath.ResourceName
            <@@ readValue(resourceName, name) @@>
        let resource = ProvidedProperty(name, typ, IsStatic=true, GetterCode=getter)
        if not(String.IsNullOrEmpty(comment)) then resource.AddXmlDoc(node.Comment)
        resource :> MemberInfo ]

/// Creates resource data type from specified ResX file
let private createResourceDataType (resXFilePath: ResourceFileInfo) =
    let resourceName = Path.GetFileNameWithoutExtension(resXFilePath.CompileTimePath)
    let data = ProvidedTypeDefinition(resourceName, baseType=Some typeof<obj>, HideObjectMethods=true)
    let properties = toProperties resXFilePath
    properties |> Seq.iter data.AddMember
    data

/// Creates provided type from static resource file parameter
let private createResXProvider typeName resXFilePath =
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
                    let resourceFileInfo = ResourceFileInfo.Create(filePath)
                    let providedType = createResXProvider typeName resourceFileInfo
                    context.WatchFile filePath
                    providedType
                | _ -> failwith "unexpected parameter values")
            cache.GetOrAdd (typeName, value)))
    resXType
