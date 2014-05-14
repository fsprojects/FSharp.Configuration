module FSharp.Configuration.ConfigTypeProvider

open FSharp.Configuration.Helper
open Microsoft.FSharp.Core.CompilerServices
open Samples.FSharp.ProvidedTypes
open System
open System.Configuration
open System.IO
open System.Reflection

[<TypeProvider>]
type public FSharpConfigurationProvider(cfg: TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces()
    let context = new Context(this, cfg)
    do this.AddNamespace (
        rootNamespace, 
        [ AppSettingsTypeProvider.typedAppSettings context
          ResXProvider.typedResources context
          YamlConfigTypeProvider.typedYamlConfig context ])
    interface IDisposable with 
        member x.Dispose() = dispose context

[<TypeProviderAssembly>]
do ()