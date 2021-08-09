namespace FSharp.Configuration.Yaml

// Put the TypeProviderAssemblyAttribute in the runtime DLL, pointing to the design-time DLL
[<assembly:CompilerServices.TypeProviderAssembly("FSharp.Configuration.DesignTime.dll")>]
do ()
