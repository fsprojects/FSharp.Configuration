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
    let disposing = Event<unit>()
    let disposingEvent = disposing.Publish
    do this.AddNamespace (
        rootNamespace, 
        [ AppSettingsTypeProvider.typedAppSettings this disposingEvent cfg
          ResXProvider.typedResources this cfg ])
    interface IDisposable with 
        member x.Dispose() = disposing.Trigger()

[<TypeProviderAssembly>]
do ()