#r @"paket:
source https://api.nuget.org/v3/index.json
framework net6.0
nuget FSharp.Core
nuget Fake.Core.Target
nuget Fake.Core.Process
nuget Fake.Core.ReleaseNotes 
nuget Fake.IO.FileSystem
nuget Fake.DotNet.Cli
nuget Fake.DotNet.MSBuild
nuget Fake.DotNet.AssemblyInfoFile
nuget Fake.DotNet.Paket
nuget Fake.DotNet.Testing.Expecto 
nuget Fake.DotNet.FSFormatting 
nuget Fake.Tools.Git
nuget Fake.Api.GitHub //"

#load "./.fake/build.fsx/intellisense.fsx"

open Fake
open Fake.Core.TargetOperators
open Fake.Core
open Fake.IO
open Fake.IO.Globbing.Operators
open Fake.DotNet
open Fake.Tools
open System

Target.initEnvironment()

// --------------------------------------------------------------------------------------

// The name of the project
// (used by attributes in AssemblyInfo, name of a NuGet package and directory in 'src')
let project = "FSharp.Configuration"

// Short summary of the project
// (used as description in AssemblyInfo and as a short summary for NuGet package)
let summary =
    "The FSharp.Configuration project contains type providers for the configuration of .NET projects."

let cloneUrl = "git@github.com:fsprojects/FSharp.Configuration.git"
// --------------------------------------------------------------------------------------
// END TODO: The rest of the file includes standard build steps
// --------------------------------------------------------------------------------------

let buildDir = "bin"


// Read additional information from the release notes document
let release = ReleaseNotes.load "RELEASE_NOTES.md"

let genFSAssemblyInfo(projectPath: string) =
    let projectName = System.IO.Path.GetFileNameWithoutExtension(projectPath)
    let basePath = "src/" + projectName
    let fileName = basePath + "/AssemblyInfo.fs"

    AssemblyInfoFile.createFSharp fileName [
        AssemblyInfo.Title(projectName)
        AssemblyInfo.Product project
        AssemblyInfo.Description summary
        AssemblyInfo.Version release.AssemblyVersion
        AssemblyInfo.FileVersion release.AssemblyVersion
    ]

let genCSAssemblyInfo(projectPath: string) =
    let projectName = System.IO.Path.GetFileNameWithoutExtension(projectPath)
    let basePath = "src/" + projectName + "/Properties"
    let fileName = basePath + "/AssemblyInfo.cs"

    AssemblyInfoFile.createCSharp fileName [
        AssemblyInfo.Title(projectName)
        AssemblyInfo.Product project
        AssemblyInfo.Description summary
        AssemblyInfo.Version release.AssemblyVersion
        AssemblyInfo.FileVersion release.AssemblyVersion
    ]

// Generate assembly info files with the right version & up-to-date information
Target.create "AssemblyInfo" (fun _ ->
    let fsProjs = !! "src/**/*.fsproj"
    let csProjs = !! "src/**/*.csproj"
    fsProjs |> Seq.iter genFSAssemblyInfo
    csProjs |> Seq.iter genCSAssemblyInfo)

// --------------------------------------------------------------------------------------
// Clean build results & restore NuGet packages

Target.create "Clean" (fun _ -> Shell.cleanDirs [ buildDir ])

Target.create "CleanDocs" (fun _ -> Shell.cleanDirs [ "docs/output" ])

// --------------------------------------------------------------------------------------
// Build library & test project

let dotnet buildOptions command args =
    DotNet.exec buildOptions command args
    |> fun x ->
        if x.ExitCode <> 0 then
            let messages = List.concat [ x.Errors; x.Messages ]
            failwith <| String.Join("\n", messages)

Target.create "Build" (fun _ -> dotnet id "build" "FSharp.Configuration.sln -c Release")

Target.create "RunTests" (fun _ ->
    dotnet
        (fun r ->
            { r with
                WorkingDirectory = "tests/FSharp.Configuration.Tests/"
            })
        "run"
        "")

Target.create "NuGet" (fun _ ->
    Paket.pack(fun p ->
        { p with
            ToolType = ToolType.CreateLocalTool()
            OutputPath = "bin"
            Version = release.NugetVersion
            ReleaseNotes = String.toLines release.Notes
        }))

// --------------------------------------------------------------------------------------
// Fantomas code formatting and style checking

let sourceFiles =
    !! "**/*.fs" ++ "**/*.fsx"
    -- "packages/**/*.*"
    -- "paket-files/**/*.*"
    -- ".fake/**/*.*"
    -- "**/obj/**/*.*"
    -- "**/AssemblyInfo.fs"

Target.create "Format" (fun _ ->
    let result =
        sourceFiles
        |> Seq.map(sprintf "\"%s\"")
        |> String.concat " "
        |> DotNet.exec id "fantomas"

    if not result.OK then
        printfn "Errors while formatting all files: %A" result.Messages)

Target.create "CheckFormat" (fun _ ->
    let result =
        sourceFiles
        |> Seq.map(sprintf "\"%s\"")
        |> String.concat " "
        |> sprintf "%s --check"
        |> DotNet.exec id "fantomas"

    if result.ExitCode = 0 then
        Trace.log "No files need formatting"
    elif result.ExitCode = 99 then
        failwith "Some files need formatting, run `dotnet fake build -t Format` to format them"
    else
        Trace.logf "Errors while formatting: %A" result.Errors
        failwith "Unknown errors while formatting")

// --------------------------------------------------------------------------------------
// Generate the documentation

Target.create "GenerateDocs" (fun _ ->
    Shell.cleanDir ".fsdocs"
    DotNet.exec id "fsdocs" "build --clean" |> ignore)

Target.create "ReleaseDocs" (fun _ ->
    Git.Repository.clone "" cloneUrl "temp/gh-pages"
    Git.Branches.checkoutBranch "temp/gh-pages" "gh-pages"
    Shell.copyRecursive "output" "temp/gh-pages" true |> printfn "%A"

    Git.CommandHelper.runSimpleGitCommand "temp/gh-pages" "add ."
    |> printfn "%s"

    let cmd =
        sprintf """commit -a -m "Update generated documentation for version %s""" release.NugetVersion

    Git.CommandHelper.runSimpleGitCommand "temp/gh-pages" cmd
    |> printfn "%s"

    Git.Branches.push "temp/gh-pages")

Target.create "Release" ignore

Target.create "BuildPackage" ignore

// --------------------------------------------------------------------------------------
// Run all targets by default. Invoke 'build <Target>' to override

Target.create "All" ignore

"Clean"
==> "AssemblyInfo"
==> "CheckFormat"
==> "Build"
==> "RunTests"
==> "All"

"All" ==> "NuGet" ==> "BuildPackage" ==> "Release"

"GenerateDocs" ==> "ReleaseDocs" ==> "Release"

Target.runOrDefault "All"
