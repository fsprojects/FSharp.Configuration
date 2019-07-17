module FSharp.Configuration.ConfigTypeProvider

open FSharp.Configuration.Helper
open Microsoft.FSharp.Core.CompilerServices
open ProviderImplementation.ProvidedTypes

[<TypeProvider>]
type FSharpConfigurationProvider(cfg: TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces(cfg, addDefaultProbingLocation = true)
    let context = new Context(this, cfg)
    do this.AddNamespace (
            rootNamespace,
            [
#if NET461
              ResXProvider.typedResources context
#endif
              AppSettingsTypeProvider.typedAppSettings context
              YamlConfigTypeProvider.typedYamlConfig context
              IniFileProvider.typedIniFile context ])
    do this.Disposing.Add (fun _ -> dispose context)

[<TypeProviderAssembly>]
do ()
