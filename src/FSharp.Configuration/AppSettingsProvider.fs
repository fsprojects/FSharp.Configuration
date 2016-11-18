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

let mutable private exePath = Map.empty
let setExeFilePath key filePath = exePath <- exePath.Add(key, filePath)

let getConfig file =
    let path = exePath
    if path.ContainsKey(file) && System.IO.File.Exists(path.[file]) then 
        ConfigurationManager.OpenExeConfiguration path.[file]
    else
        if HostingEnvironment.IsHosted then
            Web.Configuration.WebConfigurationManager.OpenWebConfiguration "~"
        else ConfigurationManager.OpenExeConfiguration ConfigurationUserLevel.None

let getConfigValue(file,key) =
    let conf = getConfig file
    match conf.AppSettings.Settings.[key] with
    | null -> raise <| KeyNotFoundException (sprintf "Cannot find name %s in <appSettings> section of config file. (%s)" key conf.FilePath)
    | settings -> settings.Value

let setConfigValue(file, key, value) = 
    let config = getConfig file
    config.AppSettings.Settings.[key].Value <- value
    config.Save()

let getConnectionString(file, key: string) =
    match getConfig(file).ConnectionStrings.ConnectionStrings.[key] with
    | null -> raise <| KeyNotFoundException (sprintf "Cannot find name %s in <connectionStrings> section of config file." key)
    | section -> section.ConnectionString

let setConnectionString(file, key: string, value) =
    let config = getConfig file
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
                    let typeDef = ProvidedTypeDefinition ("ConnectionStrings", Some typeof<obj>, HideObjectMethods = true)
                    typeDef.AddXmlDoc (sprintf "Represents the available connection strings from %s" configFileName)
                    let niceName = createNiceNameProvider()
                    let connectionStrings = config.ConnectionStrings.ConnectionStrings
                    for connectionString in connectionStrings do
                        let key = connectionString.Name
                        let name = niceName key
                        let prop =
                            ProvidedProperty(
                                name, 
                                typeof<string>,
                                GetterCode = (fun _ -> <@@ getConnectionString(filePath, key) @@>),
                                SetterCode = fun args -> <@@ setConnectionString(filePath, key, %%args.[0]) @@>)
            
                        prop.IsStatic <- true
                        prop.AddXmlDoc (sprintf "Returns the connection string from %s with name %s" configFileName name)
                        prop.AddDefinitionLocation(1,1,filePath)
                        typeDef.AddMember prop
                    typeDef

                match parameterValues with 
                | [| :? string as configFileName |] ->
                    let typeDef = erasedType<obj> thisAssembly rootNamespace typeName
                    let niceName = createNiceNameProvider()
                    try
                        let filePath = findConfigFile context.ResolutionFolder configFileName
                        let fileMap = ExeConfigurationFileMap(ExeConfigFilename=filePath)
                        let config = ConfigurationManager.OpenMappedExeConfiguration(fileMap, ConfigurationUserLevel.None)
                        let appSettings = config.AppSettings.Settings
    
                        for key in appSettings.AllKeys do
                            let name = niceName key
                            let prop =
                                match (appSettings.Item key).Value with
                                | ValueParser.Uri _ ->
                                    ProvidedProperty(name, typeof<Uri>,
                                        GetterCode = (fun _ -> <@@ Uri (getConfigValue(filePath, key)) @@>),
                                        SetterCode = fun args -> <@@ setConfigValue(filePath, key, string (%%args.[0]: Uri)) @@>)
                                | ValueParser.Int _ ->
                                    ProvidedProperty(name, typeof<int>,
                                        GetterCode = (fun _ -> <@@ Int32.Parse (getConfigValue(filePath, key)) @@>),
                                        SetterCode = fun args -> <@@ setConfigValue(filePath, key, string (%%args.[0]: Int32)) @@>)
                                | ValueParser.Bool _ ->
                                    ProvidedProperty(name, typeof<bool>,
                                        GetterCode = (fun _ -> <@@ Boolean.Parse (getConfigValue(filePath, key)) @@>),
                                        SetterCode = fun args -> <@@ setConfigValue(filePath, key, string (%%args.[0]: Boolean)) @@>)
                                | ValueParser.Float _ ->
                                    ProvidedProperty(name, typeof<float>,
                                        GetterCode = (fun _ -> <@@ Double.Parse (getConfigValue(filePath, key), NumberStyles.Any, CultureInfo.InvariantCulture) @@>),
                                        SetterCode = fun args -> <@@ setConfigValue(filePath, key, string (%%args.[0]: float)) @@>)
                                | ValueParser.TimeSpan _ ->
                                    ProvidedProperty(name, typeof<TimeSpan>,
                                        GetterCode = (fun _ -> <@@ TimeSpan.Parse(getConfigValue(filePath, key), CultureInfo.InvariantCulture) @@>),
                                        SetterCode = fun args -> <@@ setConfigValue(filePath, key, string (%%args.[0]: TimeSpan)) @@>)
                                | ValueParser.DateTime _ ->
                                    ProvidedProperty(name, typeof<DateTime>,
                                        GetterCode = (fun _ -> <@@ DateTime.Parse(getConfigValue(filePath, key), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal) @@>),
                                        SetterCode = fun args -> <@@ setConfigValue(filePath, key, (%%args.[0]: DateTime).ToString("o")) @@>)
                                | ValueParser.Guid _ ->
                                    ProvidedProperty(name, typeof<Guid>,
                                        GetterCode = (fun _ -> <@@ Guid.Parse(getConfigValue(filePath, key)) @@>),
                                        SetterCode = fun args -> <@@ setConfigValue(filePath, key, (%%args.[0]: Guid).ToString("B")) @@>)
                                | _ ->
                                    ProvidedProperty(name, typeof<string>,
                                        GetterCode = (fun _ -> <@@ getConfigValue(filePath, key) @@>),
                                        SetterCode = fun args -> <@@ setConfigValue(filePath, key, %%args.[0]) @@>)
    
                            prop.IsStatic <- true
                            prop.AddXmlDoc (sprintf "Returns the value from %s with key %s" configFileName key)
                            prop.AddDefinitionLocation(1, 1, filePath)
    
                            typeDef.AddMember prop
        
                        let executeSelector = 
                            ProvidedMethod(
                                niceName "SelectExecutableFile", 
                                [ ProvidedParameter ("pathOfExe", typeof<string>) ],
                                typeof<Unit>, IsStaticMethod=true,
                                InvokeCode = fun args -> <@@ setExeFilePath filePath %%args.[0] @@>)

                        executeSelector.AddXmlDoc "Property to change the executable file that is read for configurations. This idea is that you can manage other executables also (e.g. from script)."
                        typeDef.AddMember executeSelector


                        let configFileNameProp = 
                            ProvidedProperty(
                                niceName "ConfigFileName", 
                                typeof<string>, 
                                GetterCode = fun _ -> <@@ filePath @@>)
    
                        configFileNameProp.IsStatic <- true
                        configFileNameProp.AddXmlDoc "Returns the Filename"
                        typeDef.AddMember configFileNameProp
    
                        let connectionStringTypeDefinition = typedConnectionStrings (config, filePath, configFileName)
                        typeDef.AddMember connectionStringTypeDefinition
    
                        context.WatchFile filePath
                        typeDef
                    with _ -> typeDef
                | x -> failwithf "unexpected parameter values %A" x)
            cache.GetOrAdd (typeName, value)))
    appSettings