module WebFrame.Endpoints

open WebFrame.Http
open WebFrame.Services
    
type Helpers () =
    static member always ( h: HttpWorkload ) = fun _ -> h
    static member always ( h: unit->HttpWorkload ) = fun _ -> h ()
    static member alwaysTask ( h: HttpWorkload ) = fun _ -> task { return h }
    static member alwaysTask ( h: unit->HttpWorkload ) = fun _ -> task { return h () }
    static member page ( p: string ) =
        if not <| p.EndsWith ".html" then failwith $"The specified file '{p}' is not an HTML page."
        fun ( serv: RequestServices ) ->
            serv.Headers.Set "Content-Type" [ "text/html" ]
            FileResponse p
    static member file ( path: string ) = fun _ -> FileResponse path
    static member file ( path: string, contentType: string ) =
        fun ( serv: RequestServices ) -> task {
            serv.Headers.Set "Content-Type" [ contentType ]
            return FileResponse path
        }
        
// Please ignore - left for future references
type private RouteBuilder () =
    member this.A = 9
type EndpointConfig () =
    member this.A = 0
