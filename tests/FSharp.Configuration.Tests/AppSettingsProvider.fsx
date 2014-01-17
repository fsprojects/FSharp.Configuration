#r"System"
#r"System.Management"
#r"System.Configuration"
#r"bin/debug/FSharp.Configuration.dll"

open System
open System.Configuration
open FSharp.Configuration

type settings = AppSettings<"app.config">
settings.Test2
settings.TestBool
settings.Test2

