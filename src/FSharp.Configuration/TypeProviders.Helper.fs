/// Starting to implement some helpers on top of ProvidedTypes API
module internal FSharp.Configuration.Helper
open System
open System.IO
open Samples.FSharp.ProvidedTypes

type Context (onChanged : unit -> unit) = 
    let disposingEvent = Event<_>()
    let lastChanged = ref (DateTime.Now.AddSeconds -1.0)
    let sync = obj()

    let trigger() =
        let shouldTrigger = lock sync (fun _ ->
            match !lastChanged with
            | time when DateTime.Now - time <= TimeSpan.FromSeconds 1. -> false
            | _ -> 
                lastChanged := DateTime.Now
                true
            )
        if shouldTrigger then onChanged()

    member this.Disposing: IEvent<unit> = disposingEvent.Publish
    member this.Trigger = trigger
    interface IDisposable with
        member x.Dispose() = disposingEvent.Trigger()

// Active patterns & operators for parsing strings
let (@?) (s:string) i = if i >= s.Length then None else Some s.[i]

let inline satisfies predicate (charOption:option<char>) = 
    match charOption with 
    | Some c when predicate c -> charOption 
    | _ -> None

let (|EOF|_|) = function 
    | Some _ -> None
    | _ -> Some ()

let (|LetterDigit|_|) = satisfies Char.IsLetterOrDigit
let (|Upper|_|) = satisfies Char.IsUpper
let (|Lower|_|) = satisfies Char.IsLower

/// Turns a string into a nice PascalCase identifier
let niceName (set:System.Collections.Generic.HashSet<_>) =     
    fun (s: string) ->
        if s = s.ToUpper() then s else
        // Starting to parse a new segment 
        let rec restart i = seq {
            match s @? i with 
            | EOF -> ()
            | LetterDigit _ & Upper _ -> yield! upperStart i (i + 1)
            | LetterDigit _ -> yield! consume i false (i + 1)
            | _ -> yield! restart (i + 1) }

        // Parsed first upper case letter, continue either all lower or all upper
        and upperStart from i = seq {
            match s @? i with 
            | Upper _ -> yield! consume from true (i + 1) 
            | Lower _ -> yield! consume from false (i + 1) 
            | _ -> yield! restart (i + 1) }
        // Consume are letters of the same kind (either all lower or all upper)
        and consume from takeUpper i = seq {
            match s @? i with
            | Lower _ when not takeUpper -> yield! consume from takeUpper (i + 1)
            | Upper _ when takeUpper -> yield! consume from takeUpper (i + 1)
            | _ -> 
                yield from, i
                yield! restart i }
    
        // Split string into segments and turn them to PascalCase
        let mutable name =
            seq { for i1, i2 in restart 0 do 
                    let sub = s.Substring(i1, i2 - i1) 
                    if Seq.forall Char.IsLetterOrDigit sub then
                        yield sub.[0].ToString().ToUpper() + sub.ToLower().Substring(1) }
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
            name <- name.Substring(0, lastLetterPos + 1) + (UInt64.Parse number + 1UL).ToString()
        set.Add name |> ignore
        name


let findConfigFile resolutionFolder configFileName =
    if Path.IsPathRooted configFileName then 
        configFileName 
    else 
        Path.Combine(resolutionFolder, configFileName)

let erasedType<'T> assemblyName rootNamespace typeName = 
    ProvidedTypeDefinition(assemblyName, rootNamespace, typeName, Some(typeof<'T>))

let generalTypeSet = System.Collections.Generic.HashSet()

let runtimeType<'T> typeName = ProvidedTypeDefinition(niceName generalTypeSet typeName, Some typeof<'T>)

let seqType ty = typedefof<seq<_>>.MakeGenericType[| ty |]
let listType ty = typedefof<list<_>>.MakeGenericType[| ty |]
let optionType ty = typedefof<option<_>>.MakeGenericType[| ty |]

// Get the assembly and namespace used to house the provided types
let thisAssembly = System.Reflection.Assembly.GetExecutingAssembly()
let rootNamespace = "FSharp.Configuration"
let missingValue = "@@@missingValue###"

module File =
    let tryOpenFile filePath =
        try Some (new FileStream (filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        with _ -> None

    let tryReadNonEmptyTextFile filePath =
        let maxAttempts = 5
        let rec sleepAndRun attempt = async {
            do! Async.Sleep 1000
            return! loop (attempt - 1) }

        and loop attempt = async {
            match tryOpenFile filePath with
            | Some file ->
                try
                    use reader = new StreamReader (file)
                    match attempt, reader.ReadToEnd() with
                    | 0, x -> return x
                    | _, "" -> 
                        printfn "Attempt %d of %d: %s is empty. Sleep for 1 sec, then retry..." attempt maxAttempts filePath
                        return! sleepAndRun (attempt - 1)
                    | _, content -> return content 
                finally file.Dispose() 
            | None -> 
                printfn "Attempt %d of %d: cannot read %s. Sleep for 1 sec, then retry..." attempt maxAttempts filePath
                return! sleepAndRun (attempt - 1) }
        loop maxAttempts |> Async.RunSynchronously

    type private State = 
        { LastFileWriteTime: DateTime
          Updated: DateTime }

    let watch changesOnly filePath onChanged =
        let getLastWrite() = File.GetLastWriteTime filePath
        let state = ref { LastFileWriteTime = getLastWrite(); Updated = DateTime.Now }
        
        let changed (args: FileSystemEventArgs) =
            let curr = getLastWrite()
            // log (sprintf "%A. Last = %A, Curr = %A" args.ChangeType !lastWrite curr)
            if curr <> (!state).LastFileWriteTime && DateTime.Now - (!state).Updated > TimeSpan.FromMilliseconds 500. then
//                try 
                    onChanged()
                    state := { LastFileWriteTime = curr; Updated = DateTime.Now }
//                with e -> ()
                //log "call onChanged"
                

        let w = new FileSystemWatcher(Path.GetDirectoryName filePath, Path.GetFileName filePath)
        w.NotifyFilter <- NotifyFilters.CreationTime ||| NotifyFilters.LastWrite ||| NotifyFilters.Size
        w.Changed.Add changed
        if not changesOnly then 
            w.Deleted.Add changed
            w.Renamed.Add changed
        w.EnableRaisingEvents <- true
        w :> IDisposable
