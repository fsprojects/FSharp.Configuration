module FSharp.Configuration.AppSettingsTypeProvider

#nowarn "57"

open FSharp.Configuration.Helper
open ProviderImplementation.ProvidedTypes
open System
open System.Configuration
open System.Globalization
open FSharp.Configuration

let internal typedAppSettings(context: Context) =
    try
        let appSettings = erasedType<obj> thisAssembly rootNamespace "AppSettings" None

        appSettings.DefineStaticParameters(
            parameters = [ ProvidedStaticParameter("configFileName", typeof<string>) ],
            instantiationFunction =
                fun typeName parameterValues ->
                    let typedConnectionStrings(config: Configuration, filePath, configFileName) =
                        let typeDef =
                            ProvidedTypeDefinition("ConnectionStrings", Some typeof<obj>, hideObjectMethods = true)

                        typeDef.AddXmlDoc(sprintf "Represents the available connection strings from %s" configFileName)
                        let niceName = createNiceNameProvider()
                        let connectionStrings = config.ConnectionStrings.ConnectionStrings

                        for connectionString in connectionStrings do
                            let key = connectionString.Name
                            let name = niceName key

                            let prop =
                                ProvidedProperty(
                                    name,
                                    typeof<string>,
                                    getterCode = (fun _ -> <@@ AppSettings.getConnectionString(filePath, key) @@>),
                                    setterCode = (fun args -> <@@ AppSettings.setConnectionString(filePath, key, %%args.[0]) @@>),
                                    isStatic = true
                                )

                            prop.AddXmlDoc(sprintf "Returns the connection string from %s with name %s" configFileName name)
                            prop.AddDefinitionLocation(1, 1, filePath)
                            typeDef.AddMember prop

                        typeDef

                    match parameterValues with
                    | [| :? string as configFileName |] ->
                        let typeDef = erasedType<obj> thisAssembly rootNamespace typeName None
                        let niceName = createNiceNameProvider()

                        try
                            let filePath = findConfigFile context.ResolutionFolder configFileName
                            let fileMap = ExeConfigurationFileMap(ExeConfigFilename = filePath)

                            let config =
                                ConfigurationManager.OpenMappedExeConfiguration(fileMap, ConfigurationUserLevel.None)

                            let appSettings = config.AppSettings.Settings

                            for key in appSettings.AllKeys do
                                let name = niceName key

                                let prop =
                                    match (appSettings.Item key).Value with
                                    | ValueParser.Uri _ ->
                                        ProvidedProperty(
                                            name,
                                            typeof<Uri>,
                                            isStatic = true,
                                            getterCode = (fun _ -> <@@ Uri(AppSettings.getConfigValue(filePath, key)) @@>),
                                            setterCode = fun args -> <@@ AppSettings.setConfigValue(filePath, key, string(%%args.[0]: Uri)) @@>
                                        )
                                    | ValueParser.Int _ ->
                                        ProvidedProperty(
                                            name,
                                            typeof<int>,
                                            isStatic = true,
                                            getterCode = (fun _ -> <@@ Int32.Parse(AppSettings.getConfigValue(filePath, key)) @@>),
                                            setterCode = fun args -> <@@ AppSettings.setConfigValue(filePath, key, string(%%args.[0]: Int32)) @@>
                                        )
                                    | ValueParser.Bool _ ->
                                        ProvidedProperty(
                                            name,
                                            typeof<bool>,
                                            isStatic = true,
                                            getterCode = (fun _ -> <@@ Boolean.Parse(AppSettings.getConfigValue(filePath, key)) @@>),
                                            setterCode = fun args -> <@@ AppSettings.setConfigValue(filePath, key, string(%%args.[0]: Boolean)) @@>
                                        )
                                    | ValueParser.Float _ ->
                                        ProvidedProperty(
                                            name,
                                            typeof<float>,
                                            isStatic = true,
                                            getterCode =
                                                (fun _ ->
                                                    <@@
                                                        Double.Parse(
                                                            AppSettings.getConfigValue(filePath, key),
                                                            NumberStyles.Any,
                                                            CultureInfo.InvariantCulture
                                                        )
                                                    @@>),
                                            setterCode = fun args -> <@@ AppSettings.setConfigValue(filePath, key, string(%%args.[0]: float)) @@>
                                        )
                                    | ValueParser.TimeSpan _ ->
                                        ProvidedProperty(
                                            name,
                                            typeof<TimeSpan>,
                                            isStatic = true,
                                            getterCode =
                                                (fun _ ->
                                                    <@@ TimeSpan.Parse(AppSettings.getConfigValue(filePath, key), CultureInfo.InvariantCulture) @@>),
                                            setterCode = fun args -> <@@ AppSettings.setConfigValue(filePath, key, string(%%args.[0]: TimeSpan)) @@>
                                        )
                                    | ValueParser.DateTime _ ->
                                        ProvidedProperty(
                                            name,
                                            typeof<DateTime>,
                                            isStatic = true,
                                            getterCode =
                                                (fun _ ->
                                                    <@@
                                                        DateTime.Parse(
                                                            AppSettings.getConfigValue(filePath, key),
                                                            CultureInfo.InvariantCulture,
                                                            DateTimeStyles.AssumeUniversal
                                                        )
                                                    @@>),
                                            setterCode =
                                                fun args -> <@@ AppSettings.setConfigValue(filePath, key, (%%args.[0]: DateTime).ToString("o")) @@>
                                        )
                                    | ValueParser.Guid _ ->
                                        ProvidedProperty(
                                            name,
                                            typeof<Guid>,
                                            isStatic = true,
                                            getterCode = (fun _ -> <@@ Guid.Parse(AppSettings.getConfigValue(filePath, key)) @@>),
                                            setterCode =
                                                fun args -> <@@ AppSettings.setConfigValue(filePath, key, (%%args.[0]: Guid).ToString("B")) @@>
                                        )
                                    | _ ->
                                        ProvidedProperty(
                                            name,
                                            typeof<string>,
                                            isStatic = true,
                                            getterCode = (fun _ -> <@@ AppSettings.getConfigValue(filePath, key) @@>),
                                            setterCode = fun args -> <@@ AppSettings.setConfigValue(filePath, key, %%args.[0]) @@>
                                        )

                                prop.AddXmlDoc(sprintf "Returns the value from %s with key %s" configFileName key)
                                prop.AddDefinitionLocation(1, 1, filePath)

                                typeDef.AddMember prop

                            let executeSelector =
                                ProvidedMethod(
                                    niceName "SelectExecutableFile",
                                    [ ProvidedParameter("pathOfExe", typeof<string>) ],
                                    typeof<Unit>,
                                    isStatic = true,
                                    invokeCode = fun args -> <@@ AppSettings.setExeFilePath filePath %%args.[0] @@>
                                )

                            executeSelector.AddXmlDoc
                                "Property to change the executable file that is read for configurations. This idea is that you can manage other executables also (e.g. from script)."

                            typeDef.AddMember executeSelector


                            let configFileNameProp =
                                ProvidedProperty(niceName "ConfigFileName", typeof<string>, isStatic = true, getterCode = fun _ -> <@@ filePath @@>)

                            configFileNameProp.AddXmlDoc "Returns the Filename"
                            typeDef.AddMember configFileNameProp

                            let connectionStringTypeDefinition =
                                typedConnectionStrings(config, filePath, configFileName)

                            typeDef.AddMember connectionStringTypeDefinition

                            context.WatchFile filePath
                            typeDef
                        with _ ->
                            typeDef
                    | x -> failwithf "unexpected parameter values %A" x
        )

        appSettings
    with ex ->
        debug "Error in AppSettingsProvider: %s\n\t%s" ex.Message ex.StackTrace
        reraise()
