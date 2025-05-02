/// Starting to implement some helpers on top of ProvidedTypes API
[<AutoOpen>]
module internal FSharp.Configuration.Helper

open System
open System.IO
open ProviderImplementation.ProvidedTypes
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Core.Printf
open System.Collections.Generic

type FilePath = string

// Active patterns & operators for parsing strings
type String with

    member x.TryGetChar i =
        if i >= x.Length then ValueNone else ValueSome x.[i]

let inline satisfies predicate (charOption: voption<char>) =
    match charOption with
    | ValueSome c when predicate c -> charOption
    | _ -> ValueNone

let dispose(x: IDisposable) =
    if x = null then () else x.Dispose()

let debug msg =
    let dt = DateTime.Now

    let writer msg =
        Diagnostics.Debug.WriteLine msg
    //        System.IO.File.AppendAllLines("debug.log", [sprintf "[%O] %s\n" dt msg])
    Printf.kprintf writer msg

[<return: Struct>]
let (|EOF|_|) =
    function
    | ValueSome _ -> ValueNone
    | _ -> ValueSome()

[<return: Struct>]
let (|LetterDigit|_|) = satisfies Char.IsLetterOrDigit

[<return: Struct>]
let (|Upper|_|) = satisfies Char.IsUpper

[<return: Struct>]
let (|Lower|_|) = satisfies Char.IsLower

/// Path.Combine
let (</>) x y =
    Path.Combine(x, y)

[<RequireQualifiedAccess>]
module Option =
    let inline ofNull value =
        if obj.ReferenceEquals(value, null) then
            None
        else
            Some value

    /// Gets the value associated with the option or the supplied default value.
    let inline getOrElse v =
        function
        | Some x -> x
        | None -> v

/// Maybe computation expression builder, copied from ExtCore library
/// https://github.com/jack-pappas/ExtCore/blob/master/ExtCore/Control.fs
[<Sealed>]
type MaybeBuilder() =
    // 'T -> M<'T>
    member inline __.Return value : 'T option =
        Some value

    // M<'T> -> M<'T>
    member inline __.ReturnFrom value : 'T option = value

    // unit -> M<'T>
    member inline __.Zero() : unit option = Some() // TODO: Should this be None?

    // (unit -> M<'T>) -> M<'T>
    member __.Delay(f: unit -> 'T option) : 'T option = f()

    // M<'T> -> M<'T> -> M<'T>
    // or
    // M<unit> -> M<'T> -> M<'T>
    member inline __.Combine(r1, r2: 'T option) : 'T option =
        match r1 with
        | None -> None
        | Some() -> r2

    // M<'T> * ('T -> M<'U>) -> M<'U>
    member inline __.Bind(value, f: 'T -> 'U option) : 'U option =
        Option.bind f value

    // 'T * ('T -> M<'U>) -> M<'U> when 'U :> IDisposable
    member __.Using(resource: ('T :> System.IDisposable), body: _ -> _ option) : _ option =
        try
            body resource
        finally
            if not <| obj.ReferenceEquals(null, box resource) then
                resource.Dispose()

    // (unit -> bool) * M<'T> -> M<'T>
    member x.While(guard, body: _ option) : _ option =
        if guard() then
            // OPTIMIZE: This could be simplified so we don't need to make calls to Bind and While.
            x.Bind(body, (fun () -> x.While(guard, body)))
        else
            x.Zero()

    // seq<'T> * ('T -> M<'U>) -> M<'U>
    // or
    // seq<'T> * ('T -> M<'U>) -> seq<M<'U>>
    member x.For(sequence: seq<_>, body: 'T -> unit option) : _ option =
        // OPTIMIZE: This could be simplified so we don't need to make calls to Using, While, Delay.
        x.Using(sequence.GetEnumerator(), (fun enum -> x.While(enum.MoveNext, x.Delay(fun () -> body enum.Current))))

let maybe = MaybeBuilder()

[<RequireQualifiedAccess>]
module ValueParser =
    open System.Globalization

    /// Converts a function returning bool,value to a function returning value option.
    /// Useful to process TryXX style functions.
    let inline private tryParseWith(func: string -> bool * 'b) =
        func
        >> function
            | true, value -> ValueSome value
            | false, _ -> ValueNone

    [<return: Struct>]
    let (|Bool|_|) = tryParseWith Boolean.TryParse

    [<return: Struct>]
    let (|Int|_|) = tryParseWith Int32.TryParse

    [<return: Struct>]
    let (|Int64|_|) = tryParseWith Int64.TryParse

    [<return: Struct>]
    let (|Float|_|) =
        tryParseWith(fun x -> Double.TryParse(x, NumberStyles.Any, CultureInfo.InvariantCulture))

    [<return: Struct>]
    let (|TimeSpan|_|) =
        tryParseWith(fun x -> TimeSpan.TryParse(x, CultureInfo.InvariantCulture))

    [<return: Struct>]
    let (|Guid|_|) = tryParseWith Guid.TryParse

    [<return: Struct>]
    let (|DateTime|_|) =
        tryParseWith(fun x -> DateTime.TryParse(x, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal))

    let (|Uri|_|)(text: string) =
        [ "http"; "https"; "ftp"; "ftps"; "sftp"; "amqp"; "file"; "ssh"; "tcp" ]
        |> List.tryPick(fun x ->
            if text.Trim().StartsWith(x + ":", StringComparison.InvariantCultureIgnoreCase) then
                match System.Uri.TryCreate(text, UriKind.Absolute) with
                | true, uri -> Some uri
                | _ -> None
            else
                None)

let inline forall predicate (source: ReadOnlySpan<_>) =
    let mutable state = true
    let mutable e = source.GetEnumerator()

    while state && e.MoveNext() do
        state <- predicate e.Current

    state

/// Turns a string into a nice PascalCase identifier
let createNiceNameProvider() =
    let set = HashSet()

    fun (s: string) ->
        if s = s.ToUpper() then
            s
        else
            // Starting to parse a new segment
            let rec restart i =
                match s.TryGetChar i with
                | EOF -> Seq.empty
                | LetterDigit _ & Upper _ -> upperStart i (i + 1)
                | LetterDigit _ -> consume i false (i + 1)
                | _ -> restart(i + 1)

            // Parsed first upper case letter, continue either all lower or all upper
            and upperStart from i =
                match s.TryGetChar i with
                | Upper _ -> consume from true (i + 1)
                | Lower _ -> consume from false (i + 1)
                | _ -> restart(i + 1)

            // Consume are letters of the same kind (either all lower or all upper)
            and consume from takeUpper i =
                match s.TryGetChar i with
                | Lower _ when not takeUpper -> consume from takeUpper (i + 1)
                | Upper _ when takeUpper -> consume from takeUpper (i + 1)
                | _ -> seq {
                    yield struct (from, i)
                    yield! restart i
                  }

            // Split string into segments and turn them to PascalCase
            let mutable name =
                seq {
                    for i1, i2 in restart 0 do
                        let sub = s.AsSpan(i1, i2 - i1)

                        if forall Char.IsLetterOrDigit sub then
                            yield Char.ToUpper(sub.[0]).ToString() + sub.Slice(1).ToString().ToLower()
                }
                |> String.concat ""

            while set.Contains name do
                let mutable lastLetterPos = String.length name - 1

                while Char.IsDigit name.[lastLetterPos] && lastLetterPos > 0 do
                    lastLetterPos <- lastLetterPos - 1

                if lastLetterPos = name.Length - 1 then
                    name <- name + "2"
                elif lastLetterPos = 0 then
                    name <- (UInt64.Parse name + 1UL).ToString()
                else
                    let number = name.Substring(lastLetterPos + 1)

                    name <-
                        name.Substring(0, lastLetterPos + 1)
                        + (UInt64.Parse number + 1UL).ToString()

            set.Add name |> ignore
            name

let findConfigFile resolutionFolder (configFileName: string) =
    if Path.IsPathRooted configFileName then
        configFileName
    else
        resolutionFolder </> configFileName
    |> Path.GetFullPath

let erasedType<'T> assemblyName rootNamespace typeName (hideObjectMethods: bool option) =
    match hideObjectMethods with
    | None -> ProvidedTypeDefinition(assemblyName, rootNamespace, typeName, Some(typeof<'T>))
    | Some hideObjectMethods -> ProvidedTypeDefinition(assemblyName, rootNamespace, typeName, Some(typeof<'T>), hideObjectMethods = hideObjectMethods)

// Get the assembly and namespace used to house the provided types
let thisAssembly = System.Reflection.Assembly.GetExecutingAssembly()
let rootNamespace = "FSharp.Configuration"

module File =
    let tryOpenFile filePath =
        try
            Some(new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        with _ ->
            None

    let tryReadNonEmptyTextFile filePath =
        let maxAttempts = 5

        let rec sleepAndRun attempt = task {
            do! System.Threading.Tasks.Task.Delay 1000
            return! loop(attempt - 1)
        }

        and loop attemptsLeft = task {
            let attempt = maxAttempts - attemptsLeft + 1

            match tryOpenFile filePath with
            | Some file ->
                try
                    use reader = new StreamReader(file)

                    match attemptsLeft, reader.ReadToEnd() with
                    | 0, x -> return x
                    | _, "" ->
                        printfn "Attempt %d of %d: %s is empty. Sleep for 1 sec, then retry..." attempt maxAttempts filePath
                        return! sleepAndRun attemptsLeft
                    | _, content -> return content
                finally
                    file.Dispose()
            | None ->
                if attemptsLeft = 0 then
                    return raise(FileNotFoundException(sprintf "File, %s could not be opened after %d attempts." filePath maxAttempts))
                else
                    printfn "Attempt %d of %d: cannot read %s. Sleep for 1 sec, then retry..." attempt maxAttempts filePath
                    return! sleepAndRun attemptsLeft
        }

        loop maxAttempts

    type private State = {
        LastFileWriteTime: DateTime
        Updated: DateTime
    }

    let watch changesOnly (filePath: string) onChanged =
        let getLastWrite() =
            File.GetLastWriteTime filePath

        let state =
            ref
                {
                    LastFileWriteTime = getLastWrite()
                    Updated = DateTime.Now
                }

        let changed(_: FileSystemEventArgs) =
            let curr = getLastWrite()

            if
                curr <> (!state).LastFileWriteTime
                && DateTime.Now - (!state).Updated > TimeSpan.FromMilliseconds 500.
            then
                onChanged()

                state
                := {
                       LastFileWriteTime = curr
                       Updated = DateTime.Now
                   }

        let watcher =
            new FileSystemWatcher(Path.GetDirectoryName filePath, Path.GetFileName filePath)

        watcher.NotifyFilter <-
            NotifyFilters.CreationTime
            ||| NotifyFilters.LastWrite
            ||| NotifyFilters.Size

        watcher.Changed.Add changed

        if not changesOnly then
            watcher.Deleted.Add changed
            watcher.Renamed.Add changed

        watcher.EnableRaisingEvents <- true
        watcher :> IDisposable

    let getFullPath resolutionFolder (fileName: string) =
        if Path.IsPathRooted fileName then
            fileName
        else
            resolutionFolder </> fileName

type private ContextMessage =
    | Watch of FilePath
    | AddDisposable of IDisposable
    | Cancel

type Context(provider: TypeProviderForNamespaces, cfg: TypeProviderConfig) =
    let watcher: IDisposable option ref = ref None

    let disposeWatcher() =
        !watcher |> Option.iter(fun x -> x.Dispose())
        watcher := None

    let watchForChanges(fileName: string) =
        disposeWatcher()
        let fileName = File.getFullPath cfg.ResolutionFolder fileName
        File.watch false fileName provider.Invalidate

    let agent =
        MailboxProcessor.Start(fun inbox ->
            let rec loop (files: Map<string, IDisposable>) (disposables: IDisposable list) = async {
                let unwatch file =
                    match files |> Map.tryFind file with
                    | Some disposable ->
                        disposable.Dispose()
                        files |> Map.remove file
                    | None -> files

                let! msg = inbox.Receive()

                match msg with
                | Watch file -> return! loop (unwatch file |> Map.add file (watchForChanges file)) disposables
                | AddDisposable x -> return! loop files (x :: disposables)
                | Cancel ->
                    files |> Map.toSeq |> Seq.map snd |> Seq.iter dispose
                    disposables |> List.iter dispose
            }

            loop Map.empty [])

    member __.ResolutionFolder = cfg.ResolutionFolder

    member __.WatchFile(file: FilePath) =
        agent.Post(Watch file)

    member __.AddDisposable x =
        agent.Post(AddDisposable x)

    interface IDisposable with
        member __.Dispose() =
            agent.Post Cancel

// open System.Runtime.Caching

// type MemoryCache with
//     member x.GetOrAdd(key: string, value: Lazy<ProvidedTypeDefinition>, ?expiration: TimeSpan) =
//         let policy = CacheItemPolicy()
//         policy.SlidingExpiration <- defaultArg expiration <| TimeSpan.FromHours 24.

//         match x.AddOrGetExisting(key, value, policy) with
//         | :? Lazy<ProvidedTypeDefinition> as item ->
//             try item.Value
//             with _ ->
//                 x.Remove key |> ignore
//                 value.Value
//         | x ->
//             assert(x = null)
//             value.Value
