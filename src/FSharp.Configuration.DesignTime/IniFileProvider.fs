module FSharp.Configuration.IniFileProvider

open System
open FSharp.Configuration
open ProviderImplementation.ProvidedTypes
open System.Globalization

let internal typedIniFile (context: Context) =
    try
        let iniFile = erasedType<obj> thisAssembly rootNamespace "IniFile" None

        iniFile.DefineStaticParameters(
            parameters = [ ProvidedStaticParameter ("configFileName", typeof<string>) ],
            instantiationFunction = fun typeName parameterValues ->
                match parameterValues with
                | [| :? string as iniFileName |] ->
                    let typeDef = erasedType<obj> thisAssembly rootNamespace typeName (Some true)
                    let niceName = createNiceNameProvider()
                    try
                        let filePath = findConfigFile context.ResolutionFolder iniFileName
                        match Ini.Parser.parse filePath with
                        | Choice1Of2 sections ->
                            for section in sections do
                                let sectionTy = ProvidedTypeDefinition(section.Name, Some typeof<obj>, hideObjectMethods = true)
                                for setting in section.Settings do
                                    let sectionName = section.Name
                                    let key = setting.Key
                                    let prop =
                                        match setting.Value with
                                        | ValueParser.Int value -> ProvidedProperty(key, typeof<int>, isStatic = true, getterCode = fun _ ->
                                            <@@
                                                match Ini.getValue filePath sectionName key with
                                                | Some v -> Int32.Parse v
                                                | None -> value
                                            @@>)
                                        | ValueParser.Bool value -> ProvidedProperty(key, typeof<bool>, isStatic = true, getterCode = fun _ ->
                                            <@@
                                                match Ini.getValue filePath sectionName key with
                                                | Some v -> Boolean.Parse v
                                                | None -> value
                                            @@>)
                                        | ValueParser.Float value -> ProvidedProperty(key, typeof<float>, isStatic = true, getterCode = fun _ ->
                                            <@@
                                                match Ini.getValue filePath sectionName key with
                                                | Some v -> Double.Parse (v, NumberStyles.Any, CultureInfo.InvariantCulture)
                                                | None -> value
                                            @@>)
                                        | value -> ProvidedProperty(key, typeof<string>, isStatic = true, getterCode = fun _ ->
                                            <@@
                                                match Ini.getValue filePath sectionName key with
                                                | Some v -> v
                                                | None -> value
                                            @@>)

                                    prop.AddXmlDoc (sprintf "Returns the value from %s from section %s with key %s" iniFileName section.Name setting.Key)
                                    prop.AddDefinitionLocation(1, 1, filePath)
                                    sectionTy.AddMember prop

                                typeDef.AddMember sectionTy
                        | Choice2Of2 e -> failwithf "%A" e

                        let name = niceName "ConfigFileName"
                        let getValue = <@@ filePath @@>
                        let prop = ProvidedProperty(name, typeof<string>, isStatic = true, getterCode = fun _ -> getValue)

                        prop.AddXmlDoc "Returns the Filename"

                        typeDef.AddMember prop
                        context.WatchFile filePath
                        typeDef
                    with _ -> typeDef
                | x -> failwithf "unexpected parameter values %A" x
        )
        iniFile
    with ex ->
        debug "Error in IniProvider: %s\n\t%s" ex.Message ex.StackTrace
        reraise ()
