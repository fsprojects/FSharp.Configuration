module internal FSharp.Configuration.ResX

open System
open System.Collections
open System.Resources
open System.ComponentModel.Design
open Samples.FSharp.ProvidedTypes

/// Converts ResX entries to provided properties
let toProperties (resXFilePath:string) =       
    use reader = new ResXResourceReader(resXFilePath)
    reader.UseResXDataNodes <- true
    [|for (entry:DictionaryEntry) in reader |> Seq.cast ->                   
        let node = entry.Value :?> ResXDataNode
        let name = string node.Name
        let value = node.GetValue(Unchecked.defaultof<ITypeResolutionService>)
        let comment = node.Comment
        let getter args = <@@ value @@>
        let resource = ProvidedProperty(name, typeof<string>, IsStatic=true, GetterCode=getter)                          
        if not(String.IsNullOrEmpty(comment)) then resource.AddXmlDoc(node.Comment)
        resource
    |]