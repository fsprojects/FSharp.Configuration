module FSharp.Configuration.ConfigTypeProvider

open FSharp.Configuration.Helper
open Microsoft.FSharp.Core.CompilerServices
open Samples.FSharp.ProvidedTypes
open System

[<TypeProvider>]
type public FSharpConfigurationProvider(cfg: TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces()
    let context = new Context(this, cfg)
    do this.AddNamespace (
        rootNamespace, 
        [ AppSettingsTypeProvider.typedAppSettings context
          ConnectionStringsTypeProvider.typedConnectionStrings context
          ResXProvider.typedResources context
          YamlConfigTypeProvider.typedYamlConfig context 
          IniFileProvider.typedIniFile context ])
    interface IDisposable with 
        member __.Dispose() = dispose context

[<TypeProviderAssembly>]
do ()