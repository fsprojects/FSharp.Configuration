module FSharp.Configuration.ConfigTypeProvider

open FSharp.Configuration.Helper
open Microsoft.FSharp.Core.CompilerServices
open ProviderImplementation.ProvidedTypes

[<TypeProvider>]
type FSharpConfigurationProvider(cfg: TypeProviderConfig) as this =
    class
        inherit TypeProviderForNamespaces(cfg)
        let context = new Context(this, cfg)
        do this.AddNamespace (
            rootNamespace,
            [ AppSettingsTypeProvider.typedAppSettings context
              ResXProvider.typedResources context
              YamlConfigTypeProvider.typedYamlConfig context
              IniFileProvider.typedIniFile context ])
        do this.Disposing.Add (fun _ -> dispose context)
    end

[<TypeProviderAssembly>]
do ()