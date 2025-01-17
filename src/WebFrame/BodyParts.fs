module WebFrame.BodyParts

open System
open System.IO
open System.Text
open System.Threading.Tasks

open Microsoft.AspNetCore.Http
open Microsoft.Net.Http.Headers
open Microsoft.Extensions.Primitives

open Newtonsoft.Json
open Newtonsoft.Json.Linq
open Newtonsoft.Json.Serialization

open WebFrame.Converters
open WebFrame.Exceptions


type FormFiles ( files: IFormFileCollection ) =
    member _.All () = files |> Seq.toList
    member _.Required ( fileName: string ) =
        match files.GetFile fileName with
        | null -> raise ( MissingRequiredFormFileException fileName )
        | f -> f
    member this.Optional ( fileName: string ) =
        try
            this.Required fileName |> Some
        with
        | :? MissingRequiredFormFileException -> None
    
    member val Count = files.Count

type FormEncodedBody ( req: HttpRequest ) =
    let mutable form: IFormCollection option = None
    let mutable files: FormFiles option = None
    
    let getForm () =
        match form with
        | Some v -> v
        | None ->
            if req.HasFormContentType then
                form <- Some req.Form
            else
                raise ( MissingRequiredFormException () )
            
            req.Form
            
    let getFiles () =
        match files with
        | Some v -> v
        | None ->
            let f = getForm ()
            let f = FormFiles f.Files
            
            files <- Some f
            
            f
            
    let getStringList ( name: string ) =
        let form = getForm ()
        
        match form.TryGetValue name with
        | true, v -> Some v
        | _ -> None
        |> Option.map ( fun i -> i.ToArray () |> List.ofArray )
        
    member private this.TryHead<'T> ( name: string ) =
        name
        |> getStringList
        |> Option.bind List.tryHead
        |> Option.bind convertTo<'T>
            
    member this.List<'T when 'T : equality> ( name: string ) =
        name
        |> getStringList
        |> Option.map ( List.map convertTo<'T> )
        |> Option.bind ( fun i -> if i |> List.contains None then None else Some i )
        |> Option.map ( List.map Option.get )
        |> Option.defaultValue []
        
    member this.Optional<'T when 'T : equality> ( name: string ) =
        name
        |> this.TryHead<'T>
        
    member this.Get<'T when 'T : equality> ( name: string ) ( d: 'T ) =
        name
        |> this.Optional<'T>
        |> Option.defaultValue d
        
    member this.Required<'T when 'T : equality> ( name: string ) =
        name
        |> this.TryHead<'T>
        |> Option.defaultWith ( fun _ -> raise ( MissingRequiredFormFieldException name ) )
        
    member _.Raw with get () = try getForm () |> Some with | :? MissingRequiredFormException -> None
    member _.Files with get () = try getFiles () |> Some with | :? MissingRequiredFormException -> None
    member val IsPresent = req.HasFormContentType
    
type RequireAllPropertiesContractResolver() =
    inherit DefaultContractResolver()

    // Code examples are taken from:
    // https://stackoverflow.com/questions/29655502/json-net-require-all-properties-on-deserialization/29660550
        
    override this.CreateProperty ( memberInfo, serialization ) =
        let prop = base.CreateProperty ( memberInfo, serialization )
        let isRequired =
            not prop.PropertyType.IsGenericType || prop.PropertyType.GetGenericTypeDefinition () <> typedefof<Option<_>>
        if isRequired then prop.Required <- Required.Always
        prop

type JsonEncodedBody ( req: HttpRequest ) =
    let mutable unknownEncoding = false
    let mutable json: JObject option = None
    
    let jsonSettings =
        JsonSerializerSettings (
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Error,
            ContractResolver = RequireAllPropertiesContractResolver () )
    let jsonSerializer = JsonSerializer.CreateDefault jsonSettings
    
    let jsonCharset =
        match MediaTypeHeaderValue.TryParse ( StringSegment req.ContentType ) with
        | true, v ->
            if v.MediaType.Equals ( "application/json", StringComparison.OrdinalIgnoreCase ) then
                Some v.Charset
            elif v.Suffix.Equals ( "json", StringComparison.OrdinalIgnoreCase ) then
                Some v.Charset
            else
                None
        | _ ->
            None
            
    let jsonEncoding =
        match jsonCharset with
        | Some c ->
            try
                if c.Equals ( "utf-8", StringComparison.OrdinalIgnoreCase ) then
                    Encoding.UTF8 |> Some
                elif c.HasValue then
                    Encoding.GetEncoding c.Value |> Some
                else
                    None
            with
            | _ ->
                unknownEncoding <- true
                None
        | None -> None
        
    let notJsonContentType = jsonCharset.IsNone || unknownEncoding
    
    let getJson () = task {
        if notJsonContentType then raise ( MissingRequiredJsonException () )
        
        match json with
        | Some v -> return v
        | None ->
            let en = jsonEncoding |> Option.defaultValue Encoding.UTF8        
        
            use br = new StreamReader ( req.Body, en )
                    
            let! body = br.ReadToEndAsync ()
            
            try
                let v = JObject.Parse body
                
                json <- Some v
                
                return v
            with
            | :? JsonSerializationException -> return raise ( MissingRequiredJsonException () )
    }

    member private _.ReadJson<'T> () = task {
        let! j = getJson ()
        
        use tr = new JTokenReader ( j )
        
        try
            return jsonSerializer.Deserialize<'T> tr
        with
        | :? JsonSerializationException -> return raise ( MissingRequiredJsonException () )
    }
    
    member private _.GetField<'T> ( jsonPath: string ) = task {
        let! j = getJson ()
        let token = j.SelectToken jsonPath
        return token.ToObject<'T> ()
    }
    
    member private _.GetFields<'T> ( jsonPath: string ) = task {
        let! j = getJson ()
        
        return
            jsonPath
            |> j.SelectTokens
            |> Seq.map ( fun i -> i.ToObject<'T> () )
            |> List.ofSeq
    }
    
    member this.Exact<'T> () : Task<'T> = this.ReadJson<'T> ()
    
    member this.Get<'T> ( path: string ) ( d: 'T ) : Task<'T> = task {
        let! v = this.Optional<'T> path
        return v |> Option.defaultValue d
    }
    
    member this.Required<'T> ( path: string ) : Task<'T> = task {
        try
            return! this.GetField<'T> path
        with
        | :? NullReferenceException -> return raise ( MissingRequiredJsonFieldException path )
    }
            
    member this.Optional<'T> ( path: string ) : Task<'T option> = task {
        try
            let! r = this.Required<'T> path
            return Some r
        with
        | :? MissingRequiredJsonException -> return None
    }
    
    member this.List<'T> ( path: string ) : Task<'T list> = this.GetFields path
    
    member this.Raw with get () : Task<JObject> = task {
        let! isPresent = this.IsPresent ()
        
        if isPresent then
            return json.Value
        else
            return raise ( MissingRequiredJsonException () )
    }
        
    member this.IsPresent () = task {
        if this.IsJsonContentType then
            try
                let! _ = getJson ()
                return true
            with
            | :? MissingRequiredJsonException -> return false
        else
            return false
    }
    member val IsJsonContentType = not notJsonContentType
    
type Body ( req: HttpRequest ) =
    member val Form = FormEncodedBody req
    member val Json = JsonEncodedBody req
    member val Raw = req.Body
    member val RawPipe = req.BodyReader
