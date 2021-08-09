namespace FSharp.Configuration.Yaml

open System.Reflection
open System
open System.IO
open System.Collections.Generic

open YamlDotNet.Core
open YamlDotNet.Serialization
open FSharp.Configuration

type Helper () =
    static member CreateResizeArray<'a>(data : 'a seq) :ResizeArray<'a> = ResizeArray<'a>(data)

module Parser =
    type Scalar =
        | Int of int
        | Int64 of int64
        | String of string
        | TimeSpan of TimeSpan
        | Bool of bool
        | Uri of Uri
        | Float of double
        | Guid of Guid

        static member ParseStr = function
            | ValueParser.Bool x -> Bool x
            | ValueParser.Int x -> Int x
            | ValueParser.Int64 x -> Int64 x
            | ValueParser.Float x -> Float x
            | ValueParser.TimeSpan x -> TimeSpan x
            | ValueParser.Uri x -> Uri x
            | ValueParser.Guid x -> Guid x
            | x -> String x

        static member FromObj (inferTypesFromStrings: bool) : obj -> Scalar = function
            | null -> String ""
            | :? System.Boolean as b -> Bool b
            | :? System.Int32 as i -> Int i
            | :? System.Int64 as i -> Int64 i
            | :? System.Double as d -> Float d
            | :? System.String as s ->
                if inferTypesFromStrings then Scalar.ParseStr s
                else Scalar.String s
            | t -> failwithf "Unknown type %s" (string (t.GetType()))

        member x.UnderlyingType =
            match x with
            | Int x -> x.GetType()
            | Int64 x -> x.GetType()
            | String x -> x.GetType()
            | Bool x -> x.GetType()
            | TimeSpan x -> x.GetType()
            | Uri x -> x.GetType()
            | Float x -> x.GetType()
            | Guid x -> x.GetType()

        member x.BoxedValue =
            match x with
            | Int x -> box x
            | Int64 x -> box x
            | String x -> box x
            | TimeSpan x -> box x
            | Bool x -> box x
            | Uri x -> box x
            | Float x -> box x
            | Guid x -> box x

    type Node =
        | Scalar of Scalar
        | List of Node list
        | Map of (string * Node) list

    let parse (inferTypesFromStrings: bool) : (string -> Node) =
        let rec loop (n: obj) =
            match n with
            | :? List<obj> as l -> Node.List (l |> Seq.map loop |> Seq.toList)
            | :? Dictionary<obj,obj> as map ->
                map
                |> Seq.map (fun p -> string p.Key, loop p.Value)
                |> Seq.toList
                |> Map
            | scalar -> Scalar (Scalar.FromObj inferTypesFromStrings scalar)

        //let settings = SerializerSettings (EmitDefaultValues = true, EmitTags = false, SortKeyForMapping = false)
        let deserializer = DeserializerBuilder().Build()
        fun text ->
            try
                deserializer.Deserialize(text) |> loop
            with
              | :? YamlDotNet.Core.YamlException as e when e.InnerException <> null ->
                  raise e.InnerException // inner exceptions are much more informative
              | _ -> reraise()

    let private inferListType (targetType: Type) (nodes: Node list) =
        let types =
            nodes
            |> List.choose (function Scalar x -> Some x | _ -> None)
            |> Seq.groupBy (fun n -> n.UnderlyingType)
            |> Seq.map fst
            |> Seq.toList

        match types with
        | [] -> targetType
        | [ty] -> typedefof<ResizeArray<_>>.MakeGenericType ty
        | types -> failwithf "List cannot contain elements of heterohenius types (attempt to mix types: %A)." types

    let update (target: 'a) (updater: Node) =
        let tryGetField x name = x.GetType().GetField(name, BindingFlags.Instance ||| BindingFlags.NonPublic) |> Option.ofNull

        let getChangedDelegate x =
            x.GetType().GetField("_changed", BindingFlags.Instance ||| BindingFlags.NonPublic).GetValue x :?> MulticastDelegate

        let rec update (target: obj) name (updater: Node) =
            match name, updater with
            | _, Scalar x -> updateScalar target name x
            | _, Map m -> updateMap target name m
            | Some name, List l -> updateList target name l
            | None, _ -> failwithf "Only Maps are allowed at the root level."

        and updateScalar (target: obj) name (node: Scalar) =
            maybe {
                let! name = name
                let! field = tryGetField target ("_" + name)

                let newValue =
                    if field.FieldType <> node.UnderlyingType then
                        if node.UnderlyingType <> typeof<string> && field.FieldType = typeof<string> then
                            node.BoxedValue |> string |> box
                        elif node.UnderlyingType = typeof<int32> && field.FieldType = typeof<int64> then
                            node.BoxedValue :?> int32 |> int64 |> box
                        else
                            failwithf "Cannot assign value of type %s to field of %s: %s." node.UnderlyingType.Name name field.FieldType.Name
                    else
                        node.BoxedValue

                let oldValue = field.GetValue(target)

                return!
                    if oldValue <> newValue then
                        field.SetValue(target, newValue)
                        Some (getChangedDelegate target)
                    else None
            } |> Option.toList

        and makeListItemUpdaters (itemType: Type) (itemNodes: Node list) =
            itemNodes
            |> List.choose (function
                | Scalar x -> Some x.BoxedValue
                | Map m ->
                    let mapItem = Activator.CreateInstance itemType
                    updateMap mapItem None m |> ignore
                    Some mapItem
                | List l -> Some (fillList itemType l))

        and makeListInstance (listType: Type) (itemType: Type) (updaters: obj list) =
            let list = Activator.CreateInstance listType
            let addMethod = listType.GetMethod("Add", [|itemType|])
            for updater in updaters do
                addMethod.Invoke(list, [|updater|]) |> ignore
            list

        and fillList (targetType: Type) (updaters: Node list) =
            let fieldType = inferListType targetType updaters
            let itemType = fieldType.GetGenericArguments().[0]
            let updaters = makeListItemUpdaters itemType updaters

            if not (targetType.IsAssignableFrom fieldType) then
                failwithf "Cannot assign %O to %O." fieldType.Name targetType.Name

            makeListInstance fieldType itemType updaters

        and updateList (target: obj) name (updaters: Node list) =
            maybe {
                let! field = tryGetField target ("_" + name)
                let fieldType = inferListType field.FieldType updaters

                if field.FieldType <> fieldType then
                    failwithf "Cannot assign %O to %O." fieldType.Name field.FieldType.Name

                let isComparable (x: obj) = x :? Uri || x :? IComparable
                let values = field.GetValue target :?> Collections.IEnumerable |> Seq.cast<obj>
                // NOTE: another solution would be to make our provided type implement IComparable
                // On the other side I'm not completely sure why we sort at all.
                // What if the ordering of the item matters for the user?
                let isSortable = values |> Seq.forall isComparable

                let sort (xs: obj seq) =
                    xs
                    |> Seq.sortBy (function
                       | :? Uri as uri -> uri.OriginalString :> IComparable
                       | :? IComparable as x -> x
                       | x -> failwithf "%A is not comparable, so it cannot be included into a list."  x)
                    |> Seq.toList

                let itemType = fieldType.GetGenericArguments().[0]
                let updaters = makeListItemUpdaters itemType updaters

                let oldValues, newValues =
                    if isSortable then sort values, sort updaters
                    else Seq.toList values, updaters

                return!
                    if not isSortable || oldValues <> newValues then
                        let list = makeListInstance fieldType itemType updaters
                        field.SetValue(target, list)
                        Some (getChangedDelegate target)
                    else None
            } |> function Some dlg -> [dlg] | None -> []

        and updateMap (target: obj) name (updaters: (string * Node) list) =
            let target =
                maybe {
                    let! name = name
                    let ty = target.GetType()
                    let mapProp = Option.ofNull (ty.GetProperty name)
                    return!
                        match mapProp with
                        | None ->
                            debug "Type %s does not contain %s property." ty.Name name
                            None
                        | Some prop -> Some (prop.GetValue (target, [||]))
                } |> Option.getOrElse target

            match updaters |> List.collect (fun (name, node) -> update target (Some name) node) with
            | [] -> []
            | events -> getChangedDelegate target :: events // if any child is raising the event, we also do (pull it up the hierarchy)

        update target None updater
        |> Seq.filter ((<>) null)
        |> Seq.collect (fun x -> x.GetInvocationList())
        |> Seq.distinct
        //|> fun x -> printfn "Updated. %d events to raise: %A" (Seq.length x) x; Seq.toList x
        |> Seq.iter (fun h -> h.Method.Invoke(h.Target, [|box target; EventArgs.Empty|]) |> ignore)


type Root (inferTypesFromStrings: bool) =
    let serializer =
        (*
        let settings = SerializerSettings(EmitDefaultValues = true, EmitTags = false, SortKeyForMapping = false,
                                          EmitAlias = false, ComparerForKeySorting = null)
        settings.RegisterSerializer (
            typeof<System.Uri>,
            { new ScalarSerializerBase() with
                member __.ConvertFrom (_, scalar) =
                    match System.Uri.TryCreate (scalar.Value, UriKind.Absolute) with
                    | true, uri -> box uri
                    | _ -> null
                member __.ConvertTo ctx =
                    match ctx.Instance with
                    | :? Uri as uri -> uri.OriginalString
                    | _ -> "" })

        settings.RegisterSerializer (
            typeof<Guid>,
            { new ScalarSerializerBase() with
                member __.ConvertFrom (_, scalar) =
                    match Guid.TryParse (scalar.Value) with
                    | true, guid -> box guid
                    | _ -> null
                member __.ConvertTo ctx =
                    match ctx.Instance with
                    | :? Guid as guid -> guid.ToString("D")
                    | _ -> "" })
        Serializer settings *)
        SerializerBuilder()
            .WithTypeConverter(
            {   new IYamlTypeConverter with
                    member __.Accepts ty =
                        ty = typeof<TimeSpan>
                    member __.ReadYaml(parser, ty) =
                        failwith "Not implemented"
                    member __.WriteYaml(emitter:YamlDotNet.Core.IEmitter, value, ty) =
                        match value with
                        | :? TimeSpan as ts ->
                            let formattedValue = ts.ToString("G")
                            emitter.Emit(YamlDotNet.Core.Events.Scalar(TagName(), formattedValue));
                        | _ -> failwithf "Expected TimeSpan but received %A" value
            })
            .Build()

    let mutable lastLoadedFrom = None

    let errorEvent = new Event<Exception>()

    /// Load Yaml config as text and update itself with it.
    member x.LoadText (yamlText: string) =
      try
        Parser.parse inferTypesFromStrings yamlText |> Parser.update x
      with e ->
        async { errorEvent.Trigger e } |> Async.Start
        reraise()

    /// Load Yaml config from a TextReader and update itself with it.
    member x.Load (reader: TextReader) =
      try
        reader.ReadToEnd() |> Parser.parse inferTypesFromStrings |> Parser.update x
      with e ->
        async { errorEvent.Trigger e } |> Async.Start
        reraise()

    /// Load Yaml config from a file and update itself with it.
    member x.Load (filePath: string) =
      try
        filePath |> Helper.File.tryReadNonEmptyTextFile |> x.LoadText
        lastLoadedFrom <- Some filePath
      with e ->
        async { errorEvent.Trigger e } |> Async.Start
        reraise()

    /// Load Yaml config from a file, update itself with it, then start watching it for changes.
    /// If it detects any change, it reloads the file.
    member x.LoadAndWatch (filePath: string) =
        x.Load filePath
        Helper.File.watch true filePath <| fun _ ->
            Diagnostics.Debug.WriteLine (sprintf "Loading %s..." filePath)
            try
                x.Load filePath
            with e ->
                Diagnostics.Debug.WriteLine (sprintf "Cannot load file %s: %O" filePath e.Message)

    /// Saves configuration as Yaml text into a stream.
    member x.Save (stream: Stream) =
        use writer = new StreamWriter(stream)
        x.Save writer

    /// Saves configuration as Yaml text into a TextWriter.
    member x.Save (writer: TextWriter) = serializer.Serialize(writer, x)

    /// Saves configuration as Yaml text into a file.
    member x.Save (filePath: string) =
        // forbid any access to the file for atomicity
        use file = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None)
        x.Save file

    /// Saves configuration as Yaml text into the last file it was loaded (if any).
    /// Throws InvalidOperationException if configuration has not been loaded at all or if it has loaded from
    /// a different kind of source (string or TextReader).
    member x.Save () =
        match lastLoadedFrom with
        | Some filePath -> x.Save filePath
        | None -> invalidOp "Cannot save configuration because it was not loaded from a file."

    /// Returns content as Yaml text.
    override x.ToString() =
        use writer = new StringWriter()
        x.Save writer
        writer.ToString()

    // Error channel to announce parse errors on
    [<CLIEvent>]
    member __.Error = errorEvent.Publish



// Put the TypeProviderAssemblyAttribute in the runtime DLL, pointing to the design-time DLL
[<assembly:CompilerServices.TypeProviderAssembly("FSharp.Configuration.DesignTime.dll")>]
do ()
