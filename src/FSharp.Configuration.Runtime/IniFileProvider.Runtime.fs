module FSharp.Configuration.Ini

open System
open System.IO

module Parser =
    open System.Text.RegularExpressions

    type Key = string
    type Value = string
    type Setting = { Key: Key; Value: Value }
    type SectionName = string

    type Section = {
        Name: SectionName
        Settings: Setting list
    }

    type Stream = | Stream of int * string list

    let (|Regex|_|) pattern =
        function
        | Stream(n, line :: rest) ->
            let m = Regex.Match(line, pattern)

            if m.Success then
                let values = [ for gr in m.Groups -> gr.Value ]
                Some(values, Stream(n, rest))
            else
                None
        | _ -> None

    let (|Comment|_|) =
        function
        | Regex @"^\s*;.*" (_, s) -> Some s
        | _ -> None

    let (|Header|_|) =
        function
        | Regex @"\[\s*(\S+)\s*\]" ([ _; name ], s) -> Some(name, s)
        | _ -> None

    let (|Setting|_|) =
        function
        | Regex @"\s*(\S+)\s*=\s*(.*)" ([ _; key; value ], s) -> Some({ Key = key; Value = value.Trim() }, s)
        | _ -> None

    let (|Settings|_|) s =
        let rec loop s settings =
            match s with
            | Setting(setting, s) -> loop s (setting :: settings)
            | Comment s -> loop s settings
            | _ -> (List.rev settings, s)

        let settings, s = loop s []
        Some(settings, s)

    let rec (|Section|_|) =
        function
        | Header(name, Settings(settings, s)) -> Some({ Name = name; Settings = settings }, s)
        | _ -> None

    let (|Sections|_|) s =
        let rec loop s sections =
            match s with
            | Section(section, s) -> loop s (section :: sections)
            | Comment s -> loop s sections
            | _ -> (sections |> List.rev, s)

        let sections, s = loop s []
        Some(sections, s)

    let streamOfLines lines =
        Stream(0, lines |> Seq.filter(not << String.IsNullOrWhiteSpace) |> List.ofSeq)

    let streamOfFile path =
        File.ReadLines path |> streamOfLines

    let parse path =
        match streamOfFile path with
        | Sections(sections, _) -> Choice1Of2 sections
        | e -> Choice2Of2 e

let getValue (iniFilePath: string) (section: string) (key: string) =
    match Parser.parse iniFilePath with
    | Choice1Of2 sections -> maybe {
        let! section = sections |> List.tryFind(fun s -> s.Name = section)
        let! setting = section.Settings |> List.tryFind(fun s -> s.Key = key)
        return setting.Value
      }
    | Choice2Of2 _ -> None
