module FSharp.Configuration.ConnectionStringsTypeProvider

open FSharp.Configuration.Helper
open Samples.FSharp.ProvidedTypes
open System
open System.Configuration
open System.Collections.Generic

let getConfigValue(key: string) =
    let connectionStrings = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None).ConnectionStrings.ConnectionStrings
    connectionStrings.[key].ConnectionString

let setConfigValue(key:string, value) = 
    let settings = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None)
    settings.ConnectionStrings.ConnectionStrings.[key].ConnectionString <- value
    settings.Save()

let internal typedConnectionStrings (context: Context) =
    let connectionStrings = erasedType<obj> thisAssembly rootNamespace "ConnectionStrings"

    connectionStrings.DefineStaticParameters(
        parameters = [ProvidedStaticParameter("configFileName", typeof<string>)], 
        instantiationFunction = (fun typeName parameterValues ->
            match parameterValues with 
            | [| :? string as configFileName |] ->
                let typeDef = erasedType<obj> thisAssembly rootNamespace typeName
                let names = HashSet()
                try
                    let filePath = findConfigFile context.ResolutionFolder configFileName
                    let fileMap = ExeConfigurationFileMap(ExeConfigFilename=filePath)
                    let connectionStrings = ConfigurationManager.OpenMappedExeConfiguration(fileMap, ConfigurationUserLevel.None).ConnectionStrings.ConnectionStrings

                    for connectionString in connectionStrings do
                        let niceName = niceName names connectionString.Name
                        let name = connectionString.Name
                        let prop = 
                            ProvidedProperty(niceName, typeof<string>,
                                GetterCode = (fun _ -> <@@ getConfigValue name @@>),
                                SetterCode = fun args -> <@@ setConfigValue(name, %%args.[0]) @@>)

                        prop.IsStatic <- true
                        prop.AddXmlDoc (sprintf "Returns the connection string from %s with name %s" configFileName name)
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
                with _ -> typeDef
            | x -> failwithf "unexpected parameter values %A" x))
    connectionStrings