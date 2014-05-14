module FSharp.Configuration.AppSettingsTypeProvider

open FSharp.Configuration.Helper
open Microsoft.FSharp.Core.CompilerServices
open Samples.FSharp.ProvidedTypes
open System
open System.Configuration
open System.IO
open System.Reflection
open System.Collections.Generic
open System.Globalization

/// Converts a function returning bool,value to a function returning value option.
/// Useful to process TryXX style functions.
let inline tryParseWith func = func >> function
    | true, _ -> Some()
    | false, _ -> None

let (|Bool|_|) = tryParseWith Boolean.TryParse
let (|Int|_|) = tryParseWith Int32.TryParse
let (|Double|_|) text =  
    match Double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture) with
    | true, _ -> Some()
    | _ -> None

let getConfigValue(key) = 
    let settings = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None).AppSettings.Settings
    settings.[key].Value

let internal typedAppSettings (context: Context) =
    let appSettings = erasedType<obj> thisAssembly rootNamespace "AppSettings"

    appSettings.DefineStaticParameters(
        parameters = [ProvidedStaticParameter("configFileName", typeof<string>)], 
        instantiationFunction = (fun typeName parameterValues ->
            match parameterValues with 
            | [| :? string as configFileName |] ->
                let typeDef = erasedType<obj> thisAssembly rootNamespace typeName
                let names = HashSet()
                try
                    let filePath = findConfigFile context.ResolutionFolder configFileName
                    let fileMap = ExeConfigurationFileMap(ExeConfigFilename=filePath)
                    let appSettings = ConfigurationManager.OpenMappedExeConfiguration(fileMap, ConfigurationUserLevel.None).AppSettings.Settings

                    for key in appSettings.AllKeys do
                        let name = niceName names key
                        let prop =
                            match (appSettings.Item key).Value with
                            | Int -> ProvidedProperty(name, typeof<int>, GetterCode = fun _ -> 
                                <@@ Int32.Parse (getConfigValue key) @@>)
                            | Bool -> ProvidedProperty(name, typeof<bool>, GetterCode = fun _ -> 
                                <@@ Boolean.Parse (getConfigValue key) @@>)
                            | Double -> ProvidedProperty(name, typeof<float>, GetterCode = fun _ -> 
                                <@@ Double.Parse (getConfigValue key, NumberStyles.Any, CultureInfo.InvariantCulture) @@>)
                            | _ -> ProvidedProperty(name, typeof<string>, GetterCode = fun _ -> <@@ getConfigValue key @@>)

                        prop.IsStatic <- true
                        prop.AddXmlDoc (sprintf "Returns the value from %s with key %s" configFileName key)
                        prop.AddDefinitionLocation(1,1,filePath)

                        typeDef.AddMember prop

                    let name = niceName names "ConfigFileName"
                    let getValue = <@@ filePath @@>
                    let prop = ProvidedProperty(name, typeof<string>, GetterCode = (fun _ -> getValue))

                    prop.IsStatic <- true
                    prop.AddXmlDoc "Returns the Filename"

                    typeDef.AddMember prop
                    context.WatchFile filePath
                    typeDef
                with 
                | exn -> typeDef
            | x -> failwithf "unexpected parameter values %A" x))
    appSettings