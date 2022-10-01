module FSharp.Configuration.AppSettings

open FSharp.Configuration.Helper
open ProviderImplementation.ProvidedTypes
open System
open System.Configuration
open System.Collections.Generic
open System.Globalization
#if NET461
open System.Web.Hosting
#endif

let mutable private exePath = Map.empty<string, string>

let setExeFilePath key filePath =
    exePath <- exePath.Add(key, filePath)

let getConfig file =
    let path = exePath

    if path.ContainsKey(file) && System.IO.File.Exists(path.[file]) then
        ConfigurationManager.OpenExeConfiguration path.[file]
    else
#if NET461
    if
        HostingEnvironment.IsHosted
    then
        Web.Configuration.WebConfigurationManager.OpenWebConfiguration "~"
    else
        ConfigurationManager.OpenExeConfiguration ConfigurationUserLevel.None
#else
        ConfigurationManager.OpenExeConfiguration ConfigurationUserLevel.None
#endif

let getConfigValue(file, key) =
    let conf = getConfig file

    match conf.AppSettings.Settings.[key] with
    | null ->
        raise
        <| KeyNotFoundException(sprintf "Cannot find name %s in <appSettings> section of config file. (%s)" key conf.FilePath)
    | settings -> settings.Value

let setConfigValue(file, key, value) =
    let config = getConfig file
    config.AppSettings.Settings.[key].Value <- value
    config.Save()

let getConnectionString(file, key: string) =
    match getConfig(file).ConnectionStrings.ConnectionStrings.[key] with
    | null ->
        raise
        <| KeyNotFoundException(sprintf "Cannot find name %s in <connectionStrings> section of config file." key)
    | section -> section.ConnectionString

let setConnectionString(file, key: string, value) =
    let config = getConfig file
    config.ConnectionStrings.ConnectionStrings.[key].ConnectionString <- value
    config.Save()
