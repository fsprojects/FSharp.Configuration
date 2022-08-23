module FSharp.Configuration.ConfigTypeProvider

open FSharp.Configuration.Helper
open Microsoft.FSharp.Core.CompilerServices
open ProviderImplementation.ProvidedTypes
open System.Reflection

[<TypeProvider>]
type FSharpConfigurationProvider(cfg: TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces(cfg, assemblyReplacementMap=[("FSharp.Configuration.DesignTime", "FSharp.Configuration.Runtime")], addDefaultProbingLocation = true)

    let asm = Assembly.GetExecutingAssembly()
    // check we contain a copy of runtime files, and are not referencing the runtime DLL
    do assert (typeof<FSharp.Configuration.Yaml.Root>.Assembly.GetName().Name = asm.GetName().Name)

    let context = new Context(this, cfg)
    do this.AddNamespace (
            rootNamespace,
            [
#if ENABLE_RESXPROVIDER
              ResXProvider.typedResources context
#endif
              AppSettingsTypeProvider.typedAppSettings context
              YamlConfigTypeProvider.typedYamlConfig context
              IniFileProvider.typedIniFile context ])
    do this.Disposing.Add (fun _ -> dispose context)

[<TypeProviderAssembly>]
do ()
