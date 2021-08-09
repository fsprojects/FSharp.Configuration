#r @"paket:
source https://nuget.org/api/v2
framework netstandard2.0
nuget FSharp.Core 4.7.2
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

#if !FAKE
#load "./.fake/build.fsx/intellisense.fsx"
#r "netstandard" // Temp fix for https://github.com/fsharp/FAKE/issues/1985
#endif

open Fake 
open Fake.Core.TargetOperators
open Fake.Core 
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.DotNet
open Fake.DotNet.Testing
open Fake.Tools
open Fake.Tools.Git
open System
open System.IO

Target.initEnvironment()

// --------------------------------------------------------------------------------------

// The name of the project
// (used by attributes in AssemblyInfo, name of a NuGet package and directory in 'src')
let project = "FSharp.Configuration"

// Short summary of the project
// (used as description in AssemblyInfo and as a short summary for NuGet package)
let summary = "The FSharp.Configuration project contains type providers for the configuration of .NET projects."

// Longer description of the project
// (used as a description for NuGet package; line breaks are automatically cleaned up)
let description = "The FSharp.Configuration project contains type providers for the configuration of .NET projects."

// List of author names (for NuGet package)
let authors = ["Steffen Forkmann"; "Sergey Tihon"; "Daniel Mohl"; "Tomas Petricek"; "Ryan Riley"; "Mauricio Scheffer"; "Phil Trelford"; "Vasily Kirichenko"; "Reed Copsey, Jr."]

// Tags for your project (for NuGet package)
let tags = "appsettings, YAML, F#, ResX, Ini, config"

// File system information
let solutionFile  = "FSharp.Configuration"

// Git configuration (used for publishing documentation in gh-pages branch)
// The profile where the project is posted
let gitOwner = "fsprojects"
let gitHome = "https://github.com/" + gitOwner
// The name of the project on GitHub
let gitName = "FSharp.Configuration"

// --------------------------------------------------------------------------------------
// END TODO: The rest of the file includes standard build steps
// --------------------------------------------------------------------------------------

let buildDir = "bin"


// Read additional information from the release notes document
let release = ReleaseNotes.load "RELEASE_NOTES.md"

let genFSAssemblyInfo (projectPath:string) =
    let projectName = System.IO.Path.GetFileNameWithoutExtension(projectPath)
    let basePath = "src/" + projectName
    let fileName = basePath + "/AssemblyInfo.fs"
    AssemblyInfoFile.createFSharp fileName
      [ AssemblyInfo.Title (projectName)
        AssemblyInfo.Product project
        AssemblyInfo.Description summary
        AssemblyInfo.Version release.AssemblyVersion
        AssemblyInfo.FileVersion release.AssemblyVersion ]

let genCSAssemblyInfo (projectPath:string) =
    let projectName = System.IO.Path.GetFileNameWithoutExtension(projectPath)
    let basePath = "src/" + projectName + "/Properties"
    let fileName = basePath + "/AssemblyInfo.cs"
    AssemblyInfoFile.createCSharp fileName
      [ AssemblyInfo.Title (projectName)
        AssemblyInfo.Product project
        AssemblyInfo.Description summary
        AssemblyInfo.Version release.AssemblyVersion
        AssemblyInfo.FileVersion release.AssemblyVersion ]

// Generate assembly info files with the right version & up-to-date information
Target.create "AssemblyInfo" (fun _ ->
  let fsProjs =  !! "src/**/*.fsproj"
  let csProjs = !! "src/**/*.csproj"
  fsProjs |> Seq.iter genFSAssemblyInfo
  csProjs |> Seq.iter genCSAssemblyInfo
)

// --------------------------------------------------------------------------------------
// Clean build results & restore NuGet packages

Target.create "Clean" (fun _ ->
    Shell.cleanDirs [buildDir]
)

Target.create "CleanDocs" (fun _ ->
    Shell.cleanDirs ["docs/output"]
)

// --------------------------------------------------------------------------------------
// Build library & test project

Target.create "Build" (fun _ ->
    DotNet.exec id "build" "FSharp.Configuration.sln -c Release" |> ignore

    // let outDir = __SOURCE_DIRECTORY__ + "/bin/lib/net461/"
    // CreateDir outDir
    // DotNetCli.Publish (fun p -> 
    //     { p with
    //         Output = outDir
    //         Framework = "net461"
    //         WorkingDir = "src/FSharp.Configuration/" })

    // let outDir = __SOURCE_DIRECTORY__ + "/bin/lib/netstandard2.0/"
    // CreateDir outDir
    // DotNetCli.Publish (fun p -> 
    //     { p with
    //         Output = outDir
    //         Framework = "netstandard2.0"
    //         WorkingDir = "src/FSharp.Configuration/" })
)

Target.create "BuildTests" (fun _ ->
    DotNet.exec id "build" "FSharp.Configuration.Tests.sln -c Release -v n" |> ignore
)

// --------------------------------------------------------------------------------------
// Run the unit tests using test runner

open Fake.Testing

Target.create "RunTests" (fun _ ->
    !! "tests/**/bin/Release/net461/*Tests*.exe"
    |> Seq.iter (fun path ->
        Trace.tracefn "Running tests '%s' ..." path

        let args = "--fail-on-focused-tests --summary --sequenced --version"
        (if Environment.isWindows
          then CreateProcess.fromRawCommandLine path args
          else CreateProcess.fromRawCommandLine "mono" (path + " " + args))
        |> CreateProcess.ensureExitCode
        |> Proc.run
        |> ignore
    )    
    // |> Testing.Expecto.run (fun p ->
    //         { p with 
    //             //WorkingDirectory = __SOURCE_DIRECTORY__
    //             FailOnFocusedTests = true
    //             PrintVersion = true
    //             Parallel = false
    //             Summary =  true
    //             Debug = false
    //         })
)

Target.create "RunTestsNetCore" (fun _ ->
    DotNet.exec 
        (fun r -> { r with  WorkingDirectory = "tests/FSharp.Configuration.Tests/" }) 
        "run" "--framework net5.0"
    |> ignore
)

// --------------------------------------------------------------------------------------
// Build a NuGet package

Target.create "NuGet" (fun _ ->
    Paket.pack(fun p ->
        { p with
            ToolType = ToolType.CreateLocalTool()
            OutputPath = "bin"
            Version = release.NugetVersion
            ReleaseNotes = String.toLines release.Notes})
)

// --------------------------------------------------------------------------------------
// Generate the documentation

//let fakePath = "packages" @@ "build" @@ "FAKE" @@ "tools" @@ "FAKE.exe"
//let fakeStartInfo script workingDirectory args fsiargs environmentVars =
//    (fun (info: System.Diagnostics.ProcessStartInfo) ->
//        info.FileName <- System.IO.Path.GetFullPath fakePath
//        info.Arguments <- sprintf "%s --fsiargs -d:FAKE %s \"%s\"" args fsiargs script
//        info.WorkingDirectory <- workingDirectory
//        let setVar k v =
//            info.EnvironmentVariables.[k] <- v
//        for (k, v) in environmentVars do
//            setVar k v
//        setVar "MSBuild" msBuildExe
//        setVar "GIT" Git.CommandHelper.gitPath
//        setVar "FSI" fsiPath)

///// Run the given buildscript with FAKE.exe
//let executeFAKEWithOutput workingDirectory script fsiargs envArgs =
//    let exitCode =
//        ExecProcessWithLambdas
//            (fakeStartInfo script workingDirectory "" fsiargs envArgs)
//            TimeSpan.MaxValue false ignore ignore
//    System.Threading.Thread.Sleep 1000
//    exitCode

//// Documentation
//let buildDocumentationTarget fsiargs target =
    //Trace.traceImportantfn "Building documentation (%s), this could take some time, please wait..." target
    //let exit = executeFAKEWithOutput "docs/tools" "generate.fsx" fsiargs ["target", target]
    //if exit <> 0 then
    //    failwith "generating reference documentation failed"
    //()

Target.create "GenerateReferenceDocs" (fun _ ->
    ()
    //buildDocumentationTarget "-d:RELEASE -d:REFERENCE" "Default"
)

//let generateHelp' fail debug =
//    let args =
//        if debug then "--define:HELP"
//        else "--define:RELEASE --define:HELP"
//    try
//        buildDocumentationTarget args "Default"
//        Trace.traceImportant "Help generated"
//    with
//    | _ when not fail ->
//        Trace.traceImportant "generating help documentation failed"

//let generateHelp fail =
    //generateHelp' fail false

Target.create "GenerateHelp" (fun _ ->
    ()
    //DeleteFile "docs/content/release-notes.md"
    //CopyFile "docs/content/" "RELEASE_NOTES.md"
    //Rename "docs/content/release-notes.md" "docs/content/RELEASE_NOTES.md"

    //DeleteFile "docs/content/license.md"
    //CopyFile "docs/content/" "LICENSE.txt"
    //Rename "docs/content/license.md" "docs/content/LICENSE.txt"

    //generateHelp true
)

Target.create "GenerateDocs" ignore

// --------------------------------------------------------------------------------------
// Release Scripts

Target.create "ReleaseDocs" (fun _ ->
    ()
    //let tempDocsDir = "temp/gh-pages"
    //CleanDir tempDocsDir
    //Repository.cloneSingleBranch "" (gitHome + "/" + gitName + ".git") "gh-pages" tempDocsDir

    //fullclean tempDocsDir
    //CopyRecursive "docs/output" tempDocsDir true |> tracefn "%A"
    //StageAll tempDocsDir
    //Git.Commit.Commit tempDocsDir (sprintf "Update generated documentation for version %s" release.NugetVersion)
    //Branches.push tempDocsDir
)

Target.create "Release" (fun _ ->
    ()
    //StageAll ""
    //Git.Commit.Commit "" (sprintf "Bump version to %s" release.NugetVersion)
    //Branches.push ""

    //Branches.tag "" release.NugetVersion
    //Branches.pushTag "" "origin" release.NugetVersion

    //// release on github
    //createClient (getBuildParamOrDefault "github-user" "") (getBuildParamOrDefault "github-pw" "")
    //|> createDraft gitOwner gitName release.NugetVersion (release.SemVer.PreRelease <> None) release.Notes
    //|> releaseDraft
    //|> Async.RunSynchronously
)

Target.create "BuildPackage" ignore

// --------------------------------------------------------------------------------------
// Run all targets by default. Invoke 'build <Target>' to override

Target.create "All" ignore

"Clean"
  ==> "AssemblyInfo"
  ==> "Build"
  ==> "BuildTests"
  ==> "RunTestsNetCore"
  ==> "RunTests"
  //=?> ("GenerateReferenceDocs",isLocalBuild && not isMono)
  //=?> ("GenerateDocs",isLocalBuild && not isMono)
  ==> "All"
  //=?> ("ReleaseDocs",isLocalBuild && not isMono)

"All"
  ==> "NuGet"
  ==> "BuildPackage"

"CleanDocs"
  ==> "GenerateHelp"
  ==> "GenerateReferenceDocs"
  ==> "GenerateDocs"

"ReleaseDocs"
  ==> "Release"

"BuildPackage"
  ==> "Release"

Target.runOrDefault "All"
