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
            [ 
#if NET45
              ResXProvider.typedResources context
#endif
              AppSettingsTypeProvider.typedAppSettings context
              YamlConfigTypeProvider.typedYamlConfig context
              IniFileProvider.typedIniFile context ])
        do this.Disposing.Add (fun _ -> dispose context)
    end

[<TypeProviderAssembly>]
do ()