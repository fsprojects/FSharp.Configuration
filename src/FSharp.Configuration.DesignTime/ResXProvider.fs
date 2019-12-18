module FSharp.Configuration.ResXProvider

#if NET461

open System
open System.IO
open System.Reflection
open System.Resources
open System.ComponentModel.Design
open System.Collections
open System.Collections.Concurrent
open ProviderImplementation.ProvidedTypes
open FSharp.Configuration.Helper

/// Converts ResX entries to provided properties
let private toProperties (filePath: FilePath) resourceName : MemberInfo list =
    ResX.readFile filePath
    |> List.map (fun node ->
        let key = node.Name
        let ty = node.GetValueTypeName Unchecked.defaultof<ITypeResolutionService> |> Type.GetType
        let resource =
          ProvidedProperty(
            key,
            ty,
            isStatic = true,
            getterCode = fun _ -> <@@ ResX.readValue resourceName (Assembly.GetExecutingAssembly ()) key @@>)
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
    try
        let resXType = erasedType<obj> thisAssembly rootNamespace "ResXProvider" None

        resXType.DefineStaticParameters(
            parameters = [ ProvidedStaticParameter ("file", typeof<string>) ],
            instantiationFunction = fun typeName parameterValues ->
                match parameterValues with
                | [| :? string as resourcePath|] ->
                    let filePath = findConfigFile context.ResolutionFolder resourcePath
                    if not (File.Exists filePath) then invalidArg "file" "Resource file not found"
                    let resourceName =
                        Path.ChangeExtension (resourcePath, null)
                        |> replace '\\' '.'
                        |> replace '/' '.'
                    let providedType = createResXProvider typeName resourceName filePath
                    context.WatchFile filePath
                    providedType
                | _ -> failwith "unexpected parameter values"
        )
        resXType
    with ex ->
        debug "Error in ResxProvider: %s\n\t%s" ex.Message ex.StackTrace
        reraise ()

#endif
