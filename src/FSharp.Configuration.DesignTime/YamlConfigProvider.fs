module FSharp.Configuration.YamlConfigTypeProvider

#nowarn "57"
#nowarn "25"

open System.Reflection
open System
open System.IO
open System.Collections.Generic

open Microsoft.FSharp.Quotations

open YamlDotNet.Serialization

open ProviderImplementation.ProvidedTypes
open ProviderImplementation.ProvidedTypes.UncheckedQuotations
open FSharp.Configuration.Helper
open FSharp.Configuration.Yaml

module private TypesFactory =
    open Parser

    type Scalar with

        member x.ToExpr() =
            match x with
            | Int x -> Expr.Value x
            | Int64 x -> Expr.Value x
            | String x -> Expr.Value x
            | Bool x -> Expr.Value x
            | Float x -> Expr.Value x
            | TimeSpan x ->
                let parse = typeof<TimeSpan>.GetMethod ("Parse", [| typeof<string> |])
                Expr.Call(parse, [ Expr.Value(x.ToString()) ])
            | Uri x ->
                let ctr = typeof<Uri>.GetConstructor [| typeof<string> |]
                Expr.NewObject(ctr, [ Expr.Value x.OriginalString ])
            | Guid x ->
                let parse = typeof<Guid>.GetMethod ("Parse", [| typeof<string> |])
                Expr.Call(parse, [ Expr.Value(x.ToString()) ])

    type T = {
        MainType: Type option
        Types: MemberInfo list
        Init: Expr -> Expr
    }

    let private generateChangedEvent =
        let eventType = typeof<EventHandler>
        let delegateType = typeof<Delegate>

        let combineMethod =
            delegateType.GetMethod("Combine", [| delegateType; delegateType |])

        let removeMethod =
            delegateType.GetMethod("Remove", [| delegateType; delegateType |])

        fun () ->
            let eventField = ProvidedField("_changed", eventType)

            let changeEvent m me v =
                let current = Expr.Coerce(Expr.FieldGetUnchecked(me, eventField), delegateType)
                let other = Expr.Coerce(v, delegateType)
                Expr.Coerce(Expr.Call(m, [ current; other ]), eventType)

            let adder = changeEvent combineMethod
            let remover = changeEvent removeMethod

            let event =
                ProvidedEvent(
                    "Changed",
                    eventType,
                    adderCode = (fun [ me; v ] -> Expr.FieldSetUnchecked(me, eventField, adder me v)),
                    removerCode = (fun [ me; v ] -> Expr.FieldSetUnchecked(me, eventField, remover me v))
                )

            eventField, event

    let rec transform readOnly name (node: Node) =
        match name, node with
        | Some name, Scalar x -> transformScalar readOnly name x
        | _, Map m -> transformMap readOnly name m
        | Some name, List l -> transformList readOnly name true l
        | None, _ -> failwithf "Only Maps are allowed at the root level."

    and transformScalar readOnly name (node: Scalar) =
        let rawType = node.UnderlyingType
        let field = ProvidedField("_" + name, rawType)

        let prop =
            if readOnly then
                ProvidedProperty(name, rawType, isStatic = false, getterCode = (fun [ me ] -> Expr.FieldGetUnchecked(me, field)))
            else
                ProvidedProperty(
                    name,
                    rawType,
                    isStatic = false,
                    getterCode = (fun [ me ] -> Expr.FieldGetUnchecked(me, field)),
                    setterCode = (fun [ me; v ] -> Expr.FieldSetUnchecked(me, field, v))
                )

        let initValue = node.ToExpr()

        {
            MainType = Some rawType
            Types = [ field :> MemberInfo; prop :> MemberInfo ]
            Init = fun me -> Expr.FieldSetUnchecked(me, field, initValue)
        }

    and transformList readOnly name generateField (children: Node list) =
        let elements =
            children
            |> List.map (function
                | Scalar x -> {
                    MainType = Some x.UnderlyingType
                    Types = []
                    Init = fun _ -> x.ToExpr()
                  }
                | Map m -> transformMap readOnly None m
                | List l -> transformList readOnly (name + "_Items") false l)

        let elements, elementType =
            match
                elements
                |> Seq.groupBy(fun n -> n.MainType)
                |> Seq.map fst
                |> Seq.toList
            with
            | [ Some ty ] -> elements, ty
            | [ None ] ->
                // Sequence of maps: https://github.com/fsprojects/FSharp.Configuration/issues/51
                // TODOL Construct the type from all the elements (instead of only the first entry)
                let headChildren =
                    match children |> Seq.head with
                    | Map m -> m
                    | _ -> failwith "expected a sequence of maps."

                let childTypes, childInits = foldChildren readOnly headChildren
                let eventField, event = generateChangedEvent()

                let mapTy =
                    ProvidedTypeDefinition(
                        name + "_Item_Type",
                        Some typeof<obj>,
                        hideObjectMethods = true,
                        isErased = false,
                        SuppressRelocation = false
                    )

                let ctr = ProvidedConstructor([], invokeCode = (fun [ me ] -> childInits me))
                mapTy.AddMembers childTypes
                mapTy.AddMember ctr
                mapTy.AddMember eventField
                mapTy.AddMember event

                [
                    {
                        MainType = Some(mapTy :> _)
                        Types = [ mapTy :> MemberInfo ]
                        Init = fun _ -> Expr.NewObject(ctr, [])
                    }
                ],
                mapTy :> _
            | types ->
                failwithf
                    "List cannot contain elements of heterogeneous types (attempt to mix types: %A)."
                    (types |> List.map(Option.map(fun x -> x.Name)))

        let propType =
            ProvidedTypeBuilder.MakeGenericType(typedefof<IList<_>>, [ elementType ])

        let ctrType =
            ProvidedTypeBuilder.MakeGenericType(typedefof<seq<_>>, [ elementType ])

        let listCtr =
            let meth = typeof<Helper>.GetMethod ("CreateResizeArray")
            ProvidedTypeBuilder.MakeGenericMethod(meth, [ elementType ])

        let childTypes = elements |> List.collect(fun x -> x.Types)

        let initValue ty me =
            Expr.Coerce(
                Expr.CallUnchecked(
                    listCtr,
                    [
                        Expr.Coerce(Expr.NewArray(elementType, elements |> List.map(fun x -> x.Init me)), ctrType)
                    ]
                ),
                ty
            )

        if generateField then
            let fieldType =
                ProvidedTypeBuilder.MakeGenericType(typedefof<ResizeArray<_>>, [ elementType ])

            let field = ProvidedField("_" + name, fieldType)

            let prop =
                if readOnly then
                    ProvidedProperty(
                        name,
                        propType,
                        isStatic = false,
                        getterCode = (fun [ me ] -> Expr.Coerce(Expr.FieldGetUnchecked(me, field), propType))
                    )
                else
                    ProvidedProperty(
                        name,
                        propType,
                        isStatic = false,
                        getterCode = (fun [ me ] -> Expr.Coerce(Expr.FieldGetUnchecked(me, field), propType)),
                        setterCode =
                            (fun [ me; v ] ->
                                Expr.FieldSetUnchecked(me, field, Expr.Coerce(Expr.CallUnchecked(listCtr, [ Expr.Coerce(v, ctrType) ]), fieldType)))
                    )

            {
                MainType = Some fieldType
                Types = childTypes @ [ field :> MemberInfo; prop :> MemberInfo ]
                Init = fun me -> Expr.FieldSetUnchecked(me, field, initValue fieldType me)
            }
        else
            {
                MainType = Some propType
                Types = childTypes
                Init = initValue propType
            }

    and foldChildren readOnly (children: (string * Node) list) =
        let childTypes, childInits =
            children
            |> List.map(fun (name, node) -> transform readOnly (Some name) node)
            |> List.fold (fun (types, inits) t -> types @ t.Types, inits @ [ t.Init ]) ([], [])

        let affinedChildInits me =
            childInits
            |> List.fold (fun acc expr -> expr me :: acc) []
            |> List.reduce(fun res expr -> Expr.Sequential(res, expr))

        childTypes, affinedChildInits

    and transformMap readOnly name (children: (string * Node) list) =
        let childTypes, childInits = foldChildren readOnly children
        let eventField, event = generateChangedEvent()

        match name with
        | Some name ->
            let mapTy =
                ProvidedTypeDefinition(name + "_Type", Some typeof<obj>, hideObjectMethods = true, isErased = false, SuppressRelocation = false)

            let ctr = ProvidedConstructor([], invokeCode = (fun [ me ] -> childInits me))
            mapTy.AddMembers childTypes
            mapTy.AddMember ctr
            mapTy.AddMember eventField
            mapTy.AddMember event

            let field = ProvidedField("_" + name, mapTy)

            let prop =
                ProvidedProperty(name, mapTy, isStatic = false, getterCode = (fun [ me ] -> Expr.FieldGetUnchecked(me, field)))

            {
                MainType = Some(mapTy :> _)
                Types = [ mapTy :> MemberInfo; field :> MemberInfo; prop :> MemberInfo ]
                Init = fun me -> Expr.FieldSetUnchecked(me, field, Expr.NewObject(ctr, []))
            }
        | None -> {
            MainType = None
            Types = [ eventField :> MemberInfo; event :> MemberInfo ] @ childTypes
            Init = childInits
          }


let internal typedYamlConfig(context: Context) =
    try
        let baseTy = typeof<Root>

        let asm = Assembly.GetExecutingAssembly()

        let yamlConfig =
            ProvidedTypeDefinition(asm, rootNamespace, "YamlConfig", Some baseTy, isErased = false)

        let staticParams = [
            ProvidedStaticParameter("FilePath", typeof<string>, "")
            ProvidedStaticParameter("ReadOnly", typeof<bool>, false)
            ProvidedStaticParameter("YamlText", typeof<string>, "")
            ProvidedStaticParameter("InferTypesFromStrings", typeof<bool>, true)
        ]

        yamlConfig.AddXmlDoc
            """<summary>Statically typed YAML config.</summary>
               <param name='FilePath'>Path to YAML file.</param>
               <param name='ReadOnly'>Whether the resulting properties will be read-only or not.</param>
               <param name='YamlText'>Yaml as text. Mutually exclusive with FilePath parameter.</param>"""

        yamlConfig.DefineStaticParameters(
            parameters = staticParams,
            instantiationFunction =
                fun typeName paramValues ->
                    let createTy yaml readOnly inferTypesFromStrings =
                        let myAssem = ProvidedAssembly()

                        let ty =
                            ProvidedTypeDefinition(myAssem, rootNamespace, typeName, Some baseTy, isErased = false, hideObjectMethods = true)

                        let types =
                            TypesFactory.transform readOnly None (Parser.parse inferTypesFromStrings yaml)

                        let ctr = ProvidedConstructor([], invokeCode = fun (me :: _) -> types.Init me)

                        let baseCtor =
                            baseTy.GetConstructor(BindingFlags.Public ||| BindingFlags.Instance, null, [| typeof<bool> |], null)

                        ctr.BaseConstructorCall <- fun [ me ] -> baseCtor, [ me; Expr.Value inferTypesFromStrings ]
                        ty.AddMembers(ctr :> MemberInfo :: types.Types)
                        myAssem.AddTypes [ ty ]
                        ty

                    match paramValues with
                    | [| :? string as filePath; :? bool as readOnly; :? string as yamlText; :? bool as inferTypesFromStrings |] ->
                        match filePath, yamlText with
                        | "", "" -> failwith "You must specify either FilePath or YamlText parameter."
                        | "", yamlText -> createTy yamlText readOnly inferTypesFromStrings
                        | filePath, _ ->
                            let filePath = findConfigFile context.ResolutionFolder filePath
                            context.WatchFile filePath
                            createTy (File.ReadAllText filePath) readOnly inferTypesFromStrings
                    | _ -> failwith "Wrong parameters"

        )

        yamlConfig
    with ex ->
        debug "Error in YamlProvider: %s\n\t%s" ex.Message ex.StackTrace
        reraise()
