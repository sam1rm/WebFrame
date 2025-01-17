module WebFrame.Services

open System.Threading.Tasks

open Microsoft.AspNetCore.Http

open Microsoft.Extensions.Configuration
open WebFrame.ConfigParts
open WebFrame.Http
open WebFrame.BodyParts
open WebFrame.CookieParts
open WebFrame.Exceptions
open WebFrame.HeaderParts
open WebFrame.QueryParts
open WebFrame.RouteParts
open WebFrame.RouteTypes
    
type RequestServices ( ctx: HttpContext ) =
    member val Context = ctx
    member val Path = RequestPathProperties ctx.Request    
    member val Route = RouteParameters ctx.Request
    member val Query = QueryParameters ctx.Request
    member val Headers = Headers ( ctx.Request, ctx.Response )
    member val Cookies = Cookies ( ctx.Request, ctx.Response )
    member val Body = Body ctx.Request
    
    member val AppRoutes = AllRoutes ( fun () ->
        let t = typeof<IRouteDescriptorService>
        match ctx.RequestServices.GetService t with
        | null -> raise ( MissingDependencyException t.Name )
        | s -> s :?> IRouteDescriptorService )
    
    member _.GetService<'T> () =
        let t = typeof<'T>
        match ctx.RequestServices.GetService t with
        | null -> raise ( MissingDependencyException t.Name )
        | s -> s :?> 'T
        
    member _.Redirect ( url, permanent ) =
        ctx.Response.Redirect ( url, permanent )
        EndResponse
        
    member this.Config = this.GetService<IConfiguration> () |> RuntimeConfigs
    member this.Redirect url = this.Redirect ( url, false )
    member _.EndResponse () = EndResponse
    member _.EndResponse ( t: string ) = TextResponse t
    member _.EndResponse ( j: obj ) = JsonResponse j
    member _.File n = FileResponse n
    member this.File ( name: string, contentType: string ) =
        this.ContentType <- contentType
        this.File name
    member _.GetEndpoint () = ctx.GetEndpoint ()
    member _.StatusCode with get () = ctx.Response.StatusCode and set v = ctx.Response.StatusCode <- v
    member this.ContentType
        with get () = this.Headers.Get "Content-Type" ""
        and set v = this.Headers.Set "Content-Type" [ v ]
    member _.EnableBuffering () = ctx.Request.EnableBuffering ()
        
type ServicedHandler = RequestServices -> HttpWorkload
type TaskServicedHandler = RequestServices -> Task<HttpWorkload>

type HandlerSetup = ServicedHandler -> HttpHandler
type TaskHandlerSetup = TaskServicedHandler -> TaskHttpHandler
