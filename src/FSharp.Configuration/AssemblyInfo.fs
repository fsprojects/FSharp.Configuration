namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("FSharp.Configuration")>]
[<assembly: AssemblyProductAttribute("FSharp.Configuration")>]
[<assembly: AssemblyDescriptionAttribute("The FSharp.Configuration project contains type providers for the configuration of .NET projects.")>]
[<assembly: AssemblyVersionAttribute("0.5.12")>]
[<assembly: AssemblyFileVersionAttribute("0.5.12")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "0.5.12"
    let [<Literal>] InformationalVersion = "0.5.12"
