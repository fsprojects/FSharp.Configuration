namespace FSharp.Configuration.Web.Test.Controllers

open System.Web.Mvc
open FSharp.Configuration

type Settings = AppSettings<"Web.config">

type HomeController() =
    inherit Controller()
    member this.Index () =
        let _ = Settings.WebpagesEnabled
        this.View()

