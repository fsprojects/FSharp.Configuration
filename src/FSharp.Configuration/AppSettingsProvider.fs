module FSharp.Configuration.AppSettingsTypeProvider

#nowarn "57"

open FSharp.Configuration.Helper
open ProviderImplementation.ProvidedTypes
open System
open System.Configuration
open System.Collections.Generic
open System.Globalization
open System.Web.Hosting
open System.Runtime.Caching

let private getConfig() =
    if HostingEnvironment.IsHosted then
        Web.Configuration.WebConfigurationManager.OpenWebConfiguration "~"
    else ConfigurationManager.OpenExeConfiguration ConfigurationUserLevel.None

let getConfigValue key =
    let settings = getConfig().AppSettings.Settings
    settings.[key].Value

let setConfigValue (key, value) = 
    let config = getConfig() 
    config.AppSettings.Settings.[key].Value <- value
    config.Save()

let getConnectionString (key: string) =
    getConfig().ConnectionStrings.ConnectionStrings.[key].ConnectionString

let setConnectionString (key: string, value) =
    let config = getConfig()
    config.ConnectionStrings.ConnectionStrings.[key].ConnectionString <- value
    config.Save()

let internal typedAppSettings (context: Context) =
    let appSettings = erasedType<obj> thisAssembly rootNamespace "AppSettings"
    let cache = new MemoryCache("AppSettingProvider")
    context.AddDisposable cache
    
    appSettings.DefineStaticParameters(
        parameters = [ProvidedStaticParameter("configFileName", typeof<string>)],
        instantiationFunction = (fun typeName parameterValues ->
            let value = lazy (            
                let typedConnectionStrings (config: Configuration, filePath, configFileName) =
                    let typeDef = ProvidedTypeDefinition("ConnectionStrings", Some typeof<obj>, HideObjectMethods = true)
                    typeDef.AddXmlDoc (sprintf "Represents the available connection strings from %s" configFileName)
                    let names = HashSet()
                    let connectionStrings = config.ConnectionStrings.ConnectionStrings
                    for connectionString in connectionStrings do
                        let key = connectionString.Name
                        let name = niceName names key
                        let prop =
                            ProvidedProperty(name, 
                                             typeof<string>,
                                             GetterCode = (fun _ -> <@@ getConnectionString key @@>),
                                             SetterCode = fun args -> <@@ setConnectionString(key, %%args.[0]) @@>)
            
                        prop.IsStatic <- true
                        prop.AddXmlDoc (sprintf "Returns the connection string from %s with name %s" configFileName name)
                        prop.AddDefinitionLocation(1,1,filePath)
                        typeDef.AddMember prop
                    typeDef

                match parameterValues with 
                | [| :? string as configFileName |] ->
                    let typeDef = erasedType<obj> thisAssembly rootNamespace typeName
                    let names = HashSet()
                    try
                        let filePath = findConfigFile context.ResolutionFolder configFileName
                        let fileMap = ExeConfigurationFileMap(ExeConfigFilename=filePath)
                        let config = ConfigurationManager.OpenMappedExeConfiguration(fileMap, ConfigurationUserLevel.None)
                        let appSettings = config.AppSettings.Settings
    
                        for key in appSettings.AllKeys do
                            let name = niceName names key
                            let prop =
                                match (appSettings.Item key).Value with
                                | ValueParser.Uri _ ->
                                    ProvidedProperty(name, typeof<Uri>,
                                        GetterCode = (fun _ -> <@@ Uri (getConfigValue key) @@>),
                                        SetterCode = fun args -> <@@ setConfigValue(key, string (%%args.[0]: Uri)) @@>)
                                | ValueParser.Int _ ->
                                    ProvidedProperty(name, typeof<int>,
                                        GetterCode = (fun _ -> <@@ Int32.Parse (getConfigValue key) @@>),
                                        SetterCode = fun args -> <@@ setConfigValue(key, string (%%args.[0]: Int32)) @@>)
                                | ValueParser.Bool _ ->
                                    ProvidedProperty(name, typeof<bool>,
                                        GetterCode = (fun _ -> <@@ Boolean.Parse (getConfigValue key) @@>),
                                        SetterCode = fun args -> <@@ setConfigValue(key, string (%%args.[0]: Boolean)) @@>)
                                | ValueParser.Float _ ->
                                    ProvidedProperty(name, typeof<float>,
                                        GetterCode = (fun _ -> <@@ Double.Parse (getConfigValue key, NumberStyles.Any, CultureInfo.InvariantCulture) @@>),
                                        SetterCode = fun args -> <@@ setConfigValue(key, string (%%args.[0]: float)) @@>)
                                | ValueParser.TimeSpan _ ->
                                    ProvidedProperty(name, typeof<TimeSpan>,
                                        GetterCode = (fun _ -> <@@ TimeSpan.Parse(getConfigValue key, CultureInfo.InvariantCulture) @@>),
                                        SetterCode = fun args -> <@@ setConfigValue(key, string (%%args.[0]: TimeSpan)) @@>)
                                | ValueParser.DateTime _ ->
                                    ProvidedProperty(name, typeof<DateTime>,
                                        GetterCode = (fun _ -> <@@ DateTime.Parse(getConfigValue key, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal) @@>),
                                        SetterCode = fun args -> <@@ setConfigValue(key, (%%args.[0]: DateTime).ToString("o")) @@>)
                                | _ ->
                                    ProvidedProperty(name, typeof<string>,
                                        GetterCode = (fun _ -> <@@ getConfigValue key @@>),
                                        SetterCode = fun args -> <@@ setConfigValue(key, %%args.[0]) @@>)
    
                            prop.IsStatic <- true
                            prop.AddXmlDoc (sprintf "Returns the value from %s with key %s" configFileName key)
                            prop.AddDefinitionLocation(1, 1, filePath)
    
                            typeDef.AddMember prop
    
                        let prop = 
                            ProvidedProperty(niceName names "ConfigFileName", typeof<string>, GetterCode = fun _ -> <@@ filePath @@>)
    
                        prop.IsStatic <- true
                        prop.AddXmlDoc "Returns the Filename"
                        typeDef.AddMember prop
    
                        let connectionStringTypeDefinition = typedConnectionStrings (config, filePath, configFileName)
                        typeDef.AddMember connectionStringTypeDefinition
    
                        context.WatchFile filePath
                        typeDef
                    with _ -> typeDef
                | x -> failwithf "unexpected parameter values %A" x)
            cache.GetOrAdd (typeName, value)))
    appSettings