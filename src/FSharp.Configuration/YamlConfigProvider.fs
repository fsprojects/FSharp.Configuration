namespace FSharp.Configuration

#nowarn "57"

open System.Reflection
open Microsoft.FSharp.Core.CompilerServices
open Samples.FSharp.ProvidedTypes
open System
open System.IO
open SharpYaml.Serialization
open SharpYaml.Serialization.Serializers
open Microsoft.FSharp.Quotations
open System.Collections.Generic
open FSharp.Configuration.Helper

module Parser =
    type Scalar =
        | Int of int
        | String of string
        | TimeSpan of TimeSpan
        | Bool of bool
        | Uri of Uri
        static member Parse (value: string) =
            let isUri (value: string) = 
                ["http";"https";"ftp";"ftps";"sftp";"amqp"] 
                |> List.exists (fun x -> value.Trim().StartsWith(x + ":", StringComparison.InvariantCultureIgnoreCase))

            match bool.TryParse value with
            | true, x -> Bool x
            | _ ->
                match Int32.TryParse value with
                | true, x -> Int x
                | _ -> match TimeSpan.TryParse value with
                        | true, x -> TimeSpan x
                        | _ -> 
                            match isUri value, Uri.TryCreate(value, UriKind.Absolute) with
                            | true, (true, x) -> Uri x 
                            | _ -> String value
        member x.UnderlyingType = 
            match x with
            | Int x -> x.GetType()
            | String x -> x.GetType()
            | Bool x -> x.GetType()
            | TimeSpan x -> x.GetType()
            | Uri x -> x.GetType()
        
    type Node =
        | Scalar of Scalar
        | List of Node list
        | Map of (string * Node) list

    let parse : (string -> Node) =
        let rec loop (n: obj) =
            match n with
            | :? List<obj> as l -> Node.List (l |> Seq.map loop |> Seq.toList)
            | :? Dictionary<obj,obj> as m -> 
                Map (m |> Seq.choose (fun p -> 
                    match p.Key with
                    | :? string as key -> Some (key, loop p.Value)
                    | _ -> None) |> Seq.toList)
            | scalar ->
                let scalar = if scalar = null then "" else scalar.ToString()
                Scalar (Scalar.Parse scalar)

        let settings = SerializerSettings(EmitDefaultValues=true, EmitTags=false, SortKeyForMapping=false)
        let serializer = Serializer(settings)
        fun text -> serializer.Deserialize(fromText=text) |> loop

    let update (target: 'a) (updater: Node) =
        let getBoxedNodeValue =
            function
            | Int x -> box x
            | String x -> box x
            | TimeSpan x -> box x
            | Bool x -> box x
            | Uri x -> box x

        let getField o name =
            let ty = o.GetType()
            let field = ty.GetField(name, BindingFlags.Instance ||| BindingFlags.NonPublic)
            if field = null then failwithf "Field %s was not found in %s." name ty.Name
            field

        let getChangedDelegate x = 
            x.GetType().GetField("_changed", BindingFlags.Instance ||| BindingFlags.NonPublic).GetValue x :?> MulticastDelegate 

        let rec update (target: obj) name (updater: Node) =
            match name, updater with
            | _, Scalar (_ as x) -> updateScalar target name x 
            | _, Map m -> updateMap target name m
            | Some name, List l -> updateList target name l
            | None, _ -> failwithf "Only Maps are allowed at the root level."
    
        and updateScalar (target: obj) name (node: Scalar) =
            match name with
            | Some name -> 
                let field = getField target ("_" + name)
        
                if field.FieldType <> node.UnderlyingType then 
                    failwithf "Cannot assign value of type %s to field of %s: %s." node.UnderlyingType.Name name field.FieldType.Name

                let oldValue = field.GetValue(target)
                let newValue = getBoxedNodeValue node
        
                if oldValue <> newValue then
                    field.SetValue(target, newValue)
                    [getChangedDelegate target]
                else []
            | _ -> []

        and updateList (target: obj) name (updaters: Node list) =
            let updaters = updaters |> List.choose (function Scalar x -> Some x | _ -> None)

            let field = getField target ("_" + name)
        
            let fieldType = 
                match updaters |> Seq.groupBy (fun n -> n.UnderlyingType) |> Seq.map fst |> Seq.toList with
                | [] -> field.FieldType
                | [ty] -> typedefof<ResizeArray<_>>.MakeGenericType ty
                | types -> failwithf "List cannot contain elements of heterohenius types (attempt to mix types: %A)." types

            if field.FieldType <> fieldType then failwithf "Cannot assign %O to %O." fieldType.Name field.FieldType.Name

            let sort (xs: obj seq) = 
                xs 
                |> Seq.sortBy (function
                   | :? Uri as uri -> uri.OriginalString :> IComparable
                   | :? IComparable as x -> x
                   | x -> failwithf "%A is not comparable, so it cannot be included into a list."  x)
                |> Seq.toList

            let oldValues = field.GetValue(target) :?> Collections.IEnumerable |> Seq.cast<obj> |> sort
            let newValues = updaters |> List.map getBoxedNodeValue |> sort

            if oldValues <> newValues then
                let list = Activator.CreateInstance fieldType
                let addMethod = fieldType.GetMethod("Add", [|fieldType.GetGenericArguments().[0]|])
                updaters |> List.iter (fun x -> addMethod.Invoke(list, [|getBoxedNodeValue x|]) |> ignore)
                field.SetValue(target, list)
                [getChangedDelegate target]
            else []

        and updateMap (target: obj) name (updaters: (string * Node) list) =
            let target = 
                match name with
                | Some name ->
                    let ty = target.GetType()
                    let mapProp = ty.GetProperty name
                    if mapProp = null then failwithf "Type %s does not contain %s property." ty.Name name
                    mapProp.GetValue (target, [||])
                | None -> target

            match updaters |> List.collect (fun (name, node) -> update target (Some name) node) with
            | [] -> []
            | events -> getChangedDelegate target :: events // if any child is raising the event, we also do (pull it up the hierarchy)

        update target None updater
        |> Seq.filter ((<>) null)
        |> Seq.collect (fun x -> x.GetInvocationList())
        |> Seq.distinct
        //|> fun x -> printfn "Updated. %d events to raise: %A" (Seq.length x) x; Seq.toList x
        |> Seq.iter (fun h -> h.Method.Invoke(h.Target, [|box target; EventArgs.Empty|]) |> ignore)

module TypesFactory =
    open Parser

    type Scalar with
        member x.ToExpr() = 
            match x with
            | Int x -> Expr.Value x
            | String x -> Expr.Value x
            | Bool x -> Expr.Value x
            | TimeSpan x -> 
                let parse = typeof<TimeSpan>.GetMethod("Parse", [|typeof<string>|])
                Expr.Call(parse, [Expr.Value (x.ToString())])
            | Uri x ->
                let ctr = typeof<Uri>.GetConstructor [|typeof<string>|]
                Expr.NewObject(ctr, [Expr.Value x.OriginalString])

    type T =
        { MainType: Type option
          Types: MemberInfo list
          Init: Expr -> Expr }

    let private generateChangedEvent =
        let eventType = typeof<EventHandler>
        let delegateType = typeof<Delegate>
        let combineMethod = delegateType.GetMethod("Combine", [|delegateType; delegateType|])
        let removeMethod = delegateType.GetMethod("Remove", [|delegateType; delegateType|])

        fun() ->
            let eventField = ProvidedField("_changed", eventType)
            let event = ProvidedEvent("Changed", eventType)

            let changeEvent m me v = 
                let current = Expr.Coerce (Expr.FieldGet(me, eventField), delegateType)
                let other = Expr.Coerce (v, delegateType)
                Expr.Coerce (Expr.Call (m, [current; other]), eventType)

            let adder = changeEvent combineMethod
            let remover = changeEvent removeMethod

            event.AdderCode <- fun [me; v] -> Expr.FieldSet(me, eventField, adder me v)
            event.RemoverCode <- fun [me; v] -> Expr.FieldSet(me, eventField, remover me v)
            eventField, event

    let rec transform readOnly name (node: Node) =
        match name, node with
        | Some name, Scalar (_ as x) -> transformScalar readOnly name x
        | _, Map m -> transformMap readOnly name m
        | Some name, List l -> transformList readOnly name l
        | None, _ -> failwithf "Only Maps are allowed at the root level."
    
    and transformScalar readOnly name (node: Scalar) =
        let rawType = node.UnderlyingType
        let field = ProvidedField("_" +  name, rawType)
        let prop = ProvidedProperty (name, rawType, IsStatic=false, GetterCode = (fun [me] -> Expr.FieldGet(me, field)))
        if not readOnly then prop.SetterCode <- (fun [me;v] -> Expr.FieldSet(me, field, v))
        let initValue = node.ToExpr()

        { MainType = Some rawType
          Types = [field :> MemberInfo; prop :> MemberInfo]
          Init = fun me -> Expr.FieldSet(me, field, initValue) }

    and transformList readOnly name (children: Node list) =
        let elements = 
            children 
            |> List.map (function
               | Scalar x -> { MainType = Some x.UnderlyingType; Types = []; Init = fun _ -> x.ToExpr() }
               | Map m -> transformMap readOnly None m)

        let elementType = 
            match elements |> Seq.groupBy (fun n -> n.MainType) |> Seq.map fst |> Seq.toList with
            | [Some ty] -> ty
            | types -> failwithf "List cannot contain elements of heterogeneous types (attempt to mix types: %A)." 
                                 (types |> List.map (Option.map (fun x -> x.Name)))

        let fieldType = typedefof<ResizeArray<_>>.MakeGenericType elementType
        let propType = typedefof<IList<_>>.MakeGenericType elementType
        let field = ProvidedField("_" + name, fieldType)
        let prop = ProvidedProperty (name, propType, IsStatic=false, GetterCode = (fun [me] -> Expr.Coerce(Expr.FieldGet(me, field), propType)))
        let listCtr = fieldType.GetConstructor([|typedefof<seq<_>>.MakeGenericType elementType|])
        if not readOnly then prop.SetterCode <- fun [me;v] -> Expr.FieldSet(me, field, Expr.NewObject(listCtr, [v]))
        let childTypes = elements |> List.collect (fun x -> x.Types)
        let initValue me = Expr.NewObject(listCtr, [Expr.NewArray(elementType, elements |> List.map (fun x -> x.Init me))])

        { MainType = Some fieldType
          Types = childTypes @ [field :> MemberInfo; prop :> MemberInfo]
          Init = fun me -> Expr.FieldSet(me, field, initValue me) }

    and foldChildren readOnly (children: (string * Node) list) =
        let childTypes, childInits =
            children
            |> List.map (fun (name, node) -> transform readOnly (Some name) node)
            |> List.fold (fun (types, inits) t -> types @ t.Types, inits @ [t.Init]) ([], [])

        let affinedChildInits me =
            childInits 
            |> List.fold (fun acc expr -> expr me :: acc) []
            |> List.reduce (fun res expr -> Expr.Sequential(res, expr))
        childTypes, affinedChildInits

    and transformMap readOnly name (children: (string * Node) list) =
        let childTypes, childInits = foldChildren readOnly children
        let eventField, event = generateChangedEvent()
        match name with
        | Some name ->
            let mapTy = ProvidedTypeDefinition(name + "_Type", Some typeof<obj>, HideObjectMethods=true, 
                                               IsErased=false, SuppressRelocation=false)
            let ctr = ProvidedConstructor([], InvokeCode = (fun [me] -> childInits me))
            mapTy.AddMembers (ctr :> MemberInfo :: childTypes)
            let field = ProvidedField("_" + name, mapTy)
            let prop = ProvidedProperty (name, mapTy, IsStatic=false, GetterCode = (fun [me] -> Expr.FieldGet(me, field)))
            mapTy.AddMember eventField
            mapTy.AddMember event

            { MainType = Some (mapTy :> _)
              Types = [mapTy :> MemberInfo; field :> MemberInfo; prop :> MemberInfo]
              Init = fun me -> Expr.FieldSet(me, field, Expr.NewObject(ctr, [])) }
        | None -> { MainType = None; Types = [eventField :> MemberInfo; event :> MemberInfo] @ childTypes; Init = childInits }

type Root () = 
    let serializer = 
        let settings = 
            SerializerSettings(EmitDefaultValues=true,
                               EmitTags=false,
                               SortKeyForMapping=false,
                               EmitAlias=false,
                               ComparerForKeySorting=null)

        settings.RegisterSerializer(typeof<System.Uri>, 
                                    { new ScalarSerializerBase() with
                                        member x.ConvertFrom (ctx, scalar) = 
                                                match System.Uri.TryCreate(scalar.Value, UriKind.Absolute) with
                                                | true, uri -> box uri
                                                | _ -> null
                                        member x.ConvertTo ctx = 
                                                match ctx.Instance with
                                                | :? Uri as uri -> uri.OriginalString
                                                | _ -> "" })
        Serializer(settings) 
    
    /// Load Yaml config as text and update itself with it.
    member x.LoadText (yamlText: string) = Parser.parse yamlText |> Parser.update x
    /// Load Yaml config from a TextReader and update itself with it.
    member x.Load (reader: TextReader) = reader.ReadToEnd() |> Parser.parse |> Parser.update x
    /// Load Yaml config from a file and update itself with it.
    member x.Load (filePath: string) = filePath |> Helper.File.tryReadNonEmptyTextFile |> x.LoadText
    /// Load Yaml config from a file, update itself with it, then start watching it for changes.
    /// If it detects any change, it reloads the file.
    member x.LoadAndWatch (filePath: string) = 
        x.Load filePath
        Helper.File.watch true filePath <| fun _ ->
            printfn "Loading %s..." filePath
            try x.Load filePath
            with e -> printfn "Cannot load file %s: %O" filePath e.Message; reraise()
    /// Saves content into a stream.
    member x.Save (stream: Stream) =
        use writer = new StreamWriter(stream)
        x.Save writer
    /// Saves content into a TestWriter.
    member x.Save (writer: TextWriter) = serializer.Serialize(writer, x)
    /// Saves content into a file.
    member x.Save (filePath: string) =
        // forbid any access to the file for atomicity
        use file = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None) 
        x.Save file
    /// Returns content as Yaml text.
    override x.ToString() = 
        use writer = new StringWriter()
        x.Save writer
        writer.ToString()

[<TypeProvider>]
type public YamlConfigProvider (cfg: TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces()
    let mutable watcher: IDisposable option = None

    let disposeWatcher() =
        watcher |> Option.iter (fun x -> x.Dispose())
        watcher <- None

    let baseTy = typeof<Root>
    
    let watchForChanges (fileName: string) =
        disposeWatcher()
        
        let fileName =
            match Path.IsPathRooted fileName with
            | true -> fileName
            | _ -> Path.Combine (cfg.ResolutionFolder, fileName)
        
        watcher <- Some (Helper.File.watch false fileName this.Invalidate)

    let newT = ProvidedTypeDefinition(thisAssembly, rootNamespace, "YamlConfig", Some baseTy, IsErased=false, SuppressRelocation=false)
    let staticParams = 
        [ ProvidedStaticParameter ("FilePath", typeof<string>, "") 
          ProvidedStaticParameter ("ReadOnly", typeof<bool>, false)
          ProvidedStaticParameter ("YamlText", typeof<string>, "") ]

    do newT.AddXmlDoc 
        """<summary>Statically typed YAML config.</summary>
           <param name='FilePath'>Path to YAML file.</param>
           <param name='ReadOnly'>Whether the resulting properties will be read-only or not.</param>
           <param name='YamlText'>Yaml as text. Mutually exclusive with FilePath parameter.</param>"""

    do newT.DefineStaticParameters(
        parameters = staticParams,
        instantiationFunction = fun typeName paramValues ->
            let createTy yaml readOnly filePath =
                let ty = ProvidedTypeDefinition (thisAssembly, rootNamespace, typeName, Some baseTy, IsErased=false, 
                                                 SuppressRelocation=false, HideObjectMethods=true)
                let types = TypesFactory.transform readOnly None (Parser.parse yaml)
                let ctr = ProvidedConstructor ([], InvokeCode = fun [me] -> types.Init me)
                ty.AddMembers (ctr :> MemberInfo :: types.Types)
                let assemblyPath = Path.ChangeExtension(System.IO.Path.GetTempFileName(), ".dll")
                let assembly = ProvidedAssembly assemblyPath 
                assembly.AddTypes [ty]
                ty

            match paramValues with
            | [| :? string as filePath; :? bool as readOnly; :? string as yamlText |] -> 
                 match filePath, yamlText with
                 | "", "" -> failwith "You must specify either FilePath or YamlText parameter."
                 | "", yamlText -> createTy yamlText readOnly None
                 | filePath, _ -> 
                      let filePath =
                          if Path.IsPathRooted filePath then filePath 
                          else Path.Combine(cfg.ResolutionFolder, filePath)
                      watchForChanges filePath
                      createTy (File.ReadAllText filePath) readOnly (Some filePath)
            | _ -> failwith "Wrong parameters")
    
    do this.AddNamespace(rootNamespace, [newT])
    interface IDisposable with 
        member x.Dispose() = disposeWatcher()

[<TypeProviderAssembly>]
do ()
