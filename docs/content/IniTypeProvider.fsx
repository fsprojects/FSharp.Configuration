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
    intSetting = 2
    stringSetting = stringValue
    ; you are free to add comments like this. They are only valid if they begin a new line
    [Section2]
    ; float settings are also supported
    floatSetting = 1.23
    boolSetting = true
    anotherBoolSetting = False
    ; settings with no value are OK
    emptySetting =
    stringWithSemiColonValue = DataSource=foo@bar;UserName=blah 

Reference the type provider assembly and configure it to use your ini file:
*)

#r "FSharp.Configuration.dll"
open FSharp.Configuration

// Let the type provider do it's work
type IniFileType = IniFile<"Sample.ini ">

// read a value from the config
IniFileType.Section1.intSetting

// [fsi:val it : int = ]
// [fsi:  2]