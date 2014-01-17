module FSharp.Configuration.AppSettingsTypeProvider

open FSharp.Configuration.Helper
open Microsoft.FSharp.Core.CompilerServices
open Samples.FSharp.ProvidedTypes
open System
open System.Configuration
open System.IO
open System.Reflection

/// Converts a function returning bool,value to a function returning value option.
/// Useful to process TryXX style functions.
let inline tryParseWith func = func >> function
    | true, value -> Some value
    | false, _ -> None

let (|Bool|_|) = tryParseWith Boolean.TryParse
let (|Int|_|) = tryParseWith Int32.TryParse
let (|Double|_|) text =  
    match Double.TryParse(text,Globalization.NumberStyles.Any,Globalization.CultureInfo.InvariantCulture) with
    | true, value -> Some value
    | _ -> None

let getConfigValue(filePath,key) = 
    let settings = ConfigurationManager.OpenMappedExeConfiguration(ExeConfigurationFileMap(ExeConfigFilename=filePath), ConfigurationUserLevel.None).AppSettings.Settings
    settings.[key].Value

let internal typedAppSettings (ownerType:TypeProviderForNamespaces) (cfg:TypeProviderConfig) =
    let appSettings = erasedType<obj> thisAssembly rootNamespace "AppSettings"

    appSettings.DefineStaticParameters(
        parameters = [ProvidedStaticParameter("configFileName", typeof<string>)], 
        instantiationFunction = (fun typeName parameterValues ->
            match parameterValues with 
            | [| :? string as configFileName |] ->
                let typeDef = erasedType<obj> thisAssembly rootNamespace typeName
                let names = System.Collections.Generic.HashSet<_>()
                try
                    let filePath = findConfigFile cfg.ResolutionFolder configFileName
                    let fileMap = ExeConfigurationFileMap(ExeConfigFilename=filePath)
                    let appSettings = ConfigurationManager.OpenMappedExeConfiguration(fileMap, ConfigurationUserLevel.None).AppSettings.Settings

                    for key in appSettings.AllKeys do
                        let name = niceName names key
                        let prop =
                            match (appSettings.Item key).Value with
                            | Int _ ->    ProvidedProperty(name, typeof<int>, GetterCode = (fun _ -> <@@ Int32.Parse(getConfigValue(filePath,key) ) @@>))
                            | Bool _ ->   ProvidedProperty(name, typeof<bool>, GetterCode = (fun _ -> <@@ Boolean.Parse(getConfigValue(filePath,key) ) @@>))
                            | Double _ -> ProvidedProperty(name, typeof<float>, GetterCode = (fun _ -> <@@ Double.Parse(getConfigValue(filePath,key) , Globalization.NumberStyles.Any, Globalization.CultureInfo.InvariantCulture) @@>))
                            | _ ->        ProvidedProperty(name, typeof<string>, GetterCode = (fun _ -> <@@ getConfigValue(filePath,key) @@>))

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
                    typeDef
                with 
                | exn -> typeDef
            | x -> failwithf "unexpected parameter values %A" x))

    appSettings