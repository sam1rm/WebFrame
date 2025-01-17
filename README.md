# WebFrame
A massive helper F# framework for making rapid prototyping with ASP.NET Core pleasant.

### Fast Travel
* [Introduction - A Story Time](#introduction---a-story-time)
* [Guiding Principles](#guiding-principles)
* [Setup](#setup)
  * [For the first timers](#for-the-first-timers)
  * [For an advanced audience](#for-an-advanced-audience)
* [Examples](#examples)
  * [Sample Code](#sample-code)
* [Documentation](#documentation)
  * [Main App](#main-app)
  * [Request Handling](#request-handling)
  * [Request Services](#request-services)
    * [Route Parts](#route-parts)
    * [Query Parts](#query-parts)
    * [Header Parts](#header-parts)
    * [Cookie Parts](#cookie-parts)
    * [Config Parts](#config-parts)
    * [Body Parts](#body-parts)
      * [Form](#form)
      * [Json](#json)
  * [System Configuration](#system-configuration)
  * [Request Helpers](#request-helpers)
  * [Modules](#modules)
  * [Testing](#testing)
  * [Exceptions](#exceptions)
* [Changelog](#changelog)

## Introduction - A Story Time
Some long time ago I used to write web stuff using Python frameworks such as Django. More recently I got deeply into the F#. It satisfies a lot of my requirements. However, I was not satisfied with the current state of the F# web development. Every time I tried to write something quickly, I often had to choose between a heavily functional programming oriented frameworks or extremely tedious ASP.NET Core.

I wanted something quick. Why couldn't I just do the following?

```F#
open WebFrame

[<EntryPoint>]
let main _ =
    let app = App ()
    
    app.Get "/" <- fun serv -> serv.EndResponse "Hello World!"
    
    app.Run ()
    
    0 // exit code
```

So I did write it myself!

Yes, you can just write it and experience the full ASP.NET Core server!

There are a lot of helper methods available and mostly all of them are attached to the only (RequestServices) parameter that is passed to the handler on each request. This setup uses the endpoints api and all the routes can be inspected at any time.

This project is still work in progress and it is far from being a final product. Therefore - all contributions are absolutely welcome.

## Guiding Principles
Here are the guiding principals for the development and vision of the project:
* The common scenario should require the least amount of code
* The code should be obvious, self descriptive and visible
* Prefer existing F# syntax and avoid custom operators. Overloading is OK when it helps with previous points.
* Be recognisable to non-FP developers and web developers from other languages
* Make it easy to drop down to raw ASP.NET Core when the case requires it

Therefore, explicit is better than implicit but the speed of prototyping must always be considered. It is a number one priority.

* And finally, a beautiful code (in my eyes) is a better code.

## Setup
Before you begin, make sure that [.NET](https://dotnet.microsoft.com/download) 5.0+ is installed and reachable from your terminal (i.e. it is present in the Path environment variable)
### For the first timers
For those who just start with F#, I recommend starting with the following website ['F# for Fun and Profit: F# syntax in 60 seconds'](https://fsharpforfunandprofit.com/posts/fsharp-in-60-seconds/).

Once you familiarise yourself with the syntax and deal with the .Net runtime, you should check the `Samples` folder.

Clone this repository and then install the minimal template:

```
dotnet new -i ./path/to/templates/Minimal/
```

Create a new project just like you would normally do in a new directory of your choice:

```
dotnet new webframe
```

Before you can continue, you need to adjust a path to the cloned WebFrame project.

Please find your new project file called `YourProjectName.fsproj` and adjust the `Include` property in the following line (for the newcomers - it is internally an xml file) to point to currently cloned `WebFrame.fsproj` project file:

```xml
<ItemGroup>
    <ProjectReference Include="..\..\WebFrame\src\WebFrame.fsproj" />
</ItemGroup>
```

This manual adjusting step is currently required because I have not published this project to any package repository and this template is too simple to do it automagically.

Once this is done, run the following command (in the same folder where your .fsproj file is) to start the server:

`dotnet run`

Note: you may need to restore the project before your IDE can correctly work with the project: `dotnet restore` and `dotnet build`

Recommended editors by personal preferences for F#:
* VS Code with Ionide-fsharp extension
* JetBrains Rider
* Visual Studio

### For an advanced audience
Create a new console or an empty asp core project with F#.

If it is a console project, add Web framework reference.

If it is a Web project, delete the Setup file and clean up the Program file.

Add WebFrame project reference and open it in the main file. It will immediately import all the required stuff for the minimal setup.

Please consider using [Paket](https://fsprojects.github.io/Paket/) if you do not mind (as it can reference GitHub projects directly)

Update the dependencies if required.

## Examples
Please check the Samples folder for examples of most of available apis.
* Minimal - most basic setup
* Modules - shows how to modularise the app
* LocalServer - shows how to work with the static files. It sets up a server that will share the specified folder (first command line argument) on the local network.
* TestServer - shows how you can access a virtual test server that can be used for testing. You can also check out the WebFrame.Test folder for more details on how to use it.
* StandardServer - shows common scenarios 
* AdvancedServer - a kitchen sink of most other available apis and helpers from the simplest to the most complicated

### Sample Code
The following snippet shows some common scenarios and it is taken directly out of StandardServer project in the Samples folder. Please check it out.
```F#
open WebFrame
open type WebFrame.Endpoints.Helpers

[<EntryPoint>]
let main argv =
    let items = [ "todo1"; "todo2"; "todo3" ]
    
    let api = AppModule "/api"
    
    // Returning items
    api.Get "/" <- fun serv ->
        serv.EndResponse items
        
    // Adding items
    // By sending an item Name as a string field in a form
    api.Post "/" <- fun serv ->
        // If a required property in user input is not found,
        // then 400 error is issued automatically
        let itemName = serv.Body.Form.Required<string> "name"
        
        // If you need to check the content type, you can try:
        
        // Is it a form? (bool property)
        // serv.Body.Form.IsPresent
        
        // Is it a json conent type? (bool property)
        // serv.Body.Json.IsJsonContentType
        
        // Is it a json (checks the content type)?
        // If yes, try validating it. (bool task method)
        // serv.Body.Json.IsPresent ()
        
        // In all other cases (string property):
        // serv.ContentType
        
        // ContentType is an empty string if the header is missing
        
        // To set a response Content-Type manually and quickly,
        // Just assign the required value to the same property
        // Reading it will still return the content type of the request
        serv.ContentType <- "text/plain"
        
        if items |> List.contains itemName then
            serv.StatusCode <- 409
            printfn $"Item {itemName} already exists"
        else
            serv.StatusCode <- 201
            printfn $"Faking a successful addition of a new item {itemName}"
        
        serv.EndResponse ()
    
    let app = App argv
    
    // Serving Static Files is disabled by default
    app.Services.StaticFiles.Enabled <- true
    
    // Optionally adding a prefix to all static files
    app.Services.StaticFiles.Route <- "/static"
    
    // Please check the LocalServer sample for more information on the static files
    
    app.Get "/" <- page "Pages/Index.html"
    app.Get "/About" <- page "Pages/About.html"
    
    app.Module "ToDoApi" <- api
    
    app.Run ()
    
    0 // exit code
```
## Documentation
### Main App
How to create, build and run a WebFrame app
```F#
open WebFrame

[<EntryPoint>]
let main argv =
    // All it takes to create an app
    let app = App ()

    // If you want to pass command line arguments
    // to the underlying ASP.NET Core server
    let app = App argv
 
    // To run an app (blocking mode)
    // If the app is not yet built
    // it will run the build step automatically
    // However, it will not rebuild the app if it is already built
    app.Run ()
    
    // You can also specify directly the connection urls in this step
    // This will override all existing 'urls' configurations
    // Furthermore, it will force the app to be rebuilt even if it is already built
    app.Run [ "http://localhost:5000" ]
    
    // To Build an app manually before running it
    // One can run this optional command
    // Once the app is built, further changes to configs or endpoints
    // will not take place untill the app is rebuilt
    app.Build ()
```
### Request Handling
How to process icoming requests
```F#
// There are two types of request handlers
// Each returning the HttpWorkload in some form
// Internally, all the handlers are converted into TaskHttpHandler
type HttpWorkload =
    | EndResponse            // ends the response processing
    | TextResponse of string // the resonse body as string
    | FileResponse of string // filename of the file to be returned
    | JsonResponse of obj    // an obj to be serialised and returned as json

// Internal handlers
type HttpHandler = HttpContext -> HttpWorkload
type TaskHttpHandler = HttpContext -> Task<HttpWorkload>

// User provided handlers, that are converted to the internal representation
type ServicedHandler = RequestServices -> HttpWorkload
type TaskServicedHandler = RequestServices -> Task<HttpWorkload>

// Use EndResponse when you plan to construct the response manually
// And do not want any further processing to be applied after that
// If no response is provided at all, a default empty bodied 200 response is returned
// If you return a workload that contradicts your manual changes to the response object
// Then normally a ServerException would be thrown

// In order to define a request handler,
// Pass a function to an indexed property named after the expected Http Method
// The provided indexer is a string that will be converted into ASP.Net Core RoutePattern
// MS Docs Reference on Route Templating Syntax
// https://docs.microsoft.com/en-us/aspnet/core/fundamentals/routing?view=aspnetcore-5.0#route-template-reference 
app.Get "/" <- fun serv -> serv.EndResponse ()
app.Post "/" <- fun serv -> serv.EndResponse ()
app.Put "/" <- fun serv -> serv.EndResponse ()
app.Patch "/" <- fun serv -> serv.EndResponse ()
app.Delete "/" <- fun serv -> serv.EndResponse ()
app.Head "/" <- fun serv -> serv.EndResponse ()
app.Options "/" <- fun serv -> serv.EndResponse ()
app.Connect "/" <- fun serv -> serv.EndResponse ()
app.Trace "/" <- fun serv -> serv.EndResponse ()

// If you need to perform an ayncronous computation
app.GetTask "/" <- fun serv -> task { return serv.EndResponse () }
app.PostTask "/" <- fun serv -> task { return serv.EndResponse () }
app.PutTask "/" <- fun serv -> task { return serv.EndResponse () }
app.PatchTask "/" <- fun serv -> task { return serv.EndResponse () }
app.DeleteTask "/" <- fun serv -> task { return serv.EndResponse () }
app.HeadTask "/" <- fun serv -> task { return serv.EndResponse () }
app.OptionsTask "/" <- fun serv -> task { return serv.EndResponse () }
app.ConnectTask "/" <- fun serv -> task { return serv.EndResponse () }
app.TraceTask "/" <- fun serv -> task { return serv.EndResponse () }

// You do not need to use EndResponse convinience method of the RequestService parameter
// Your handler can return an HttpWorkload directly
open WebFrame.Http

app.Get "/home" <- fun _ -> TextResponse "Hello World"
app.PostTask "/home" <- fun _ -> task { return TextResponse "Hello World" }

// Moreover, you do not have to use convinience indexed properties named after their methods
// You can provide a method directly
// You must provide a TaskServicedHandler in this case
open WebFrame.RouteTypes

app.[ Post "/api" ] <- fun serv -> task { return serv.EndResponse () }

// Or even like this
open WebFrame.Http

app.[ Get "/" ] <- fun _ -> task { return EndResponse }

// Each method named indexed property will internally use this property
// Therefore, if the method and the endpoint combination repeats,
// It will immediately raise a ServerException
// However, in some corner cases the duplication may slip through the checks
// In that case the ASP.Net Core will raise its own exception
// and there is no guarantee on what exception and when it will be raised
// It depends on the whims of ASP.Net Core developers
```
If an `InputException` is raised during the request handling then a `400` response with the exception message would returned. In case of `ServerException`, a `500` response is issued instead.
### Request Services
`RequestServices` object is passed to all user defined `ServicedHandler` and it is the secret sauce of this framework. It contains intuitively named properties to access different parts of the request and the response. Furthermore, it encapsulates configuration, routes and services (DI). 

It also provides different helpers for many common scenarios. You can even access the raw `HttpRequest` and its properties through it if necessary.

The main rule of thumb that Request Services are trying to follow is:
* When you read a property, you are reading from a request object
* When you write to a property, then you are writing to a response object
* The same applies to the methods (where appropriate)
  * Different `Get`s would normally extract a value from the request
  * Different `Set`s would normally set a value in the response
  * Etc.
#### Route Parts
#### Query Parts
#### Header Parts
#### Cookie Parts
#### Config Parts
#### Body Parts
##### Form
##### Json
### System Configuration
### Request Helpers
### Modules
### Testing
### Exceptions
## Changelog
