module FSharp.Configuration.IniFileProvider

open System
open System.IO

module Parser =
    open System.Text.RegularExpressions

    type Key = string
    type Value = string
    type Setting = { Key: Key; Value: Value }
    type SectionName = string
    type Section = { Name: SectionName; Settings: Setting list }

    type Stream = Stream of int * string list

    let (|Regex|_|) pattern = function
        | Stream (n, line :: rest) ->
            let m = Regex.Match (line, pattern)
            if m.Success then
                let values = [ for gr in m.Groups -> gr.Value ]
                Some (values, Stream (n, rest))
            else None
        | _ -> None

    let (|Comment|_|) = function
        | Regex @"^\s*;.*" (_, s) -> Some s
        | _ -> None

    let (|Header|_|) = function
        | Regex @"\[\s*(\S+)\s*\]" ([_; name], s) -> Some (name, s)
        | _ -> None

    let (|Setting|_|) = function
        | Regex @"\s*(\S+)\s*=\s*([^;]*)" ([_; key; value], s) -> Some ({ Key = key; Value = value }, s)
        | _ -> None

    let (|Settings|_|) s =
        let rec loop s settings =
            match s with
            | Setting (setting, s) -> loop s (setting :: settings)
            | Comment s -> loop s settings
            | _ -> (List.rev settings, s)
        let settings, s = loop s []
        Some (settings, s) 

    let rec (|Section|_|) = function
        | Header (name, Settings (settings, s)) -> Some ({ Name = name; Settings = settings }, s)
        | _ -> None

    let (|Sections|_|) s =
        let rec loop s sections =
            match s with
            | Section (section, s) -> loop s (section :: sections)
            | Comment s -> loop s sections
            | _ -> (sections |> List.rev, s)
        let sections, s = loop s []
        Some (sections, s)

    let streamOfLines lines = Stream (0, lines |> Seq.filter (not << String.IsNullOrWhiteSpace) |> List.ofSeq)
    let streamOfFile path = File.ReadLines path |> streamOfLines
    let parse path = 
        match streamOfFile path with
        | Sections (sections, _) -> Choice1Of2 sections
        | e -> Choice2Of2 e

//    let input = """
//[Section1]
//k1=v1
//; comment
//[Section2]
//k2 = v2
//k3 = v3 ; comment 2
//k4 =
//"""
//    match streamOfLines (input.Split([| '\r'; '\n' |], StringSplitOptions.RemoveEmptyEntries)) with
//    | Sections (sections, _) -> printfn "Success %A" sections
//    | x -> printfn "Failed: %A" x
//    
//    match Stream (0, ["[Section1]"]) with
//    | Header (name, s) -> printfn "Success %s, %A" name s
//    | x -> printfn "Error: %A" x
//    
//    match Stream (0, ["k=v"]) with
//    | Setting (setting, s) -> printfn "Success %A, %A" setting s
//    | x -> printfn "Error: %A" x

open Samples.FSharp.ProvidedTypes
open System.Collections.Generic
open System
open System.Globalization

let getValue iniFileName section key = ""

let internal typedIniFile (context: Context) =
    let iniFile = erasedType<obj> thisAssembly rootNamespace "IniFile"

    iniFile.DefineStaticParameters(
        parameters = [ ProvidedStaticParameter ("configFileName", typeof<string>) ], 
        instantiationFunction = (fun typeName parameterValues ->
            match parameterValues with 
            | [| :? string as iniFileName |] ->
                let typeDef = erasedType<obj> thisAssembly rootNamespace typeName
                let names = HashSet()
                try
                    let filePath = findConfigFile context.ResolutionFolder iniFileName
                    match Parser.parse filePath with
                    | Choice1Of2 sections ->
                        for section in sections do
                            let sectionTy = erasedType<obj> thisAssembly rootNamespace section.Name
                            for setting in section.Settings do
                                let prop =
                                    match setting.Value with
                                    | Int -> ProvidedProperty(setting.Key, typeof<int>, GetterCode = fun _ -> 
                                        <@@ Int32.Parse (getValue filePath section setting.Key) @@>)
                                    | Bool -> ProvidedProperty(setting.Key, typeof<bool>, GetterCode = fun _ -> 
                                        <@@ Boolean.Parse (getValue filePath section setting.Key) @@>)
                                    | Double -> ProvidedProperty(setting.Key, typeof<float>, GetterCode = fun _ -> 
                                        <@@ Double.Parse (getValue filePath section setting.Key, NumberStyles.Any, CultureInfo.InvariantCulture) @@>)
                                    | _ -> ProvidedProperty(setting.Key, typeof<string>, GetterCode = fun _ -> 
                                        <@@ getValue filePath section setting.Key @@>)

                                prop.IsStatic <- true
                                prop.AddXmlDoc (sprintf "Returns the value from %s from section %s with key %s" iniFileName section.Name setting.Key)
                                prop.AddDefinitionLocation(1, 1, filePath)

                                sectionTy.AddMember prop
                            typeDef.AddMember sectionTy
                    | Choice2Of2 e -> failwithf "%A" e

                    let name = niceName names "ConfigFileName"
                    let getValue = <@@ filePath @@>
                    let prop = ProvidedProperty(name, typeof<string>, GetterCode = (fun _ -> getValue))

                    prop.IsStatic <- true
                    prop.AddXmlDoc "Returns the Filename"

                    typeDef.AddMember prop
                    context.WatchFile filePath
                    typeDef
                with 
                | exn -> typeDef
            | x -> failwithf "unexpected parameter values %A" x))
    iniFile