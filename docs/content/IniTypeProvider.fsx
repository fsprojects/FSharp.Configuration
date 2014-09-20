(*** hide ***)
#I "../../bin"

(**
The Ini type provider
=====================

This tutorial shows the use of the Ini type provider. 

Using Ini type provider from F# scripts
---------------------------------------

Create a `Sample.ini` file like this:

    [lang=ini]
    [Section1]
    key1=2
     key2 = stringValue
    ;comment
    [  Section2 ]
    key3 = 1.23 ; comment
    key5 = true
    key6 = False
    ; comment
    key7 =

Reference the type provider assembly and configure it to use your yaml file:
*)

#r "FSharp.Configuration.dll"
open FSharp.Configuration

// Let the type provider do it's work
type IniFileType = IniFile<"Sample.ini">

// read a value from the config
IniFileType.Section1.key2

// [fsi:val it : string = ]
// [fsi:  "stringValue"]