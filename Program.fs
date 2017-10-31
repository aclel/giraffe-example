module test_giraffe.App

open System
open System.IO

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection

open Giraffe
open Giraffe.HttpHandlers
open Giraffe.Middleware
open Giraffe.Razor.HttpHandlers
open Giraffe.Razor.Middleware
open Giraffe.HttpContextExtensions

open Microsoft.FSharpLu.Json

open DataAccess

let bindJson (ctx : HttpContext) =
    task {
        let! body = ctx.ReadBodyFromRequest()
        return Compact.deserialize body
    }

let handleAddBasin =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        task {
            let! x = bindJson(ctx)
            addBasin x |> ignore
            return! text (sprintf "%d" x.Id) next ctx
        }
        
let handleGetUser =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        let filter = ctx.BindQueryString<UserFilter>()
        let users = getUser filter
        json users next ctx


//let handleAddUser =
//    fun (next: HttpFunc) (ctx: HttpContext) ->
//        task {
//            //let! user = ctx.BindJson<User>() // Newtonsoft json deserialiser
//            let! x = bindJson(ctx) // Microsoft.FSharpLu.Json json deserialiser
//            addUser x
//            return! text (sprintf "Added %d to the users" x.Id) next ctx
//        }

// ---------------------------------
// Web app
// ---------------------------------

let webApp =
    choose [
        GET >=>
            choose [
                route "/user" >=> handleGetUser
            ]
        POST >=>
            choose [
                route "/basin/add" >=> handleAddBasin
            ]
        setStatusCode 404 >=> text "Not Found" ]

// ---------------------------------
// Error handler
// ---------------------------------

let errorHandler (ex : Exception) (logger : ILogger) =
    logger.LogError(EventId(), ex, "An unhandled exception has occurred while executing the request.")
    clearResponse >=> setStatusCode 500 >=> text ex.Message

// ---------------------------------
// Config and Main
// ---------------------------------

let configureCors (builder : CorsPolicyBuilder) =
    builder.WithOrigins("http://localhost:8080").AllowAnyMethod().AllowAnyHeader() |> ignore
    
let configureApp (app : IApplicationBuilder) =
    app.UseCors configureCors |> ignore
    app.UseGiraffeErrorHandler errorHandler
    app.UseStaticFiles() |> ignore
    app.UseGiraffe webApp

let configureServices (services : IServiceCollection) =
    let sp  = services.BuildServiceProvider()
    let env = sp.GetService<IHostingEnvironment>()
    let viewsFolderPath = Path.Combine(env.ContentRootPath, "Views")
    services.AddRazorEngine viewsFolderPath |> ignore
    services.AddCors |> ignore

let configureLogging (builder : ILoggingBuilder) =
    let filter (l : LogLevel) = l.Equals LogLevel.Error
    builder.AddFilter(filter).AddConsole().AddDebug() |> ignore

[<EntryPoint>]
let main argv =
    let contentRoot = Directory.GetCurrentDirectory()
    let webRoot     = Path.Combine(contentRoot, "WebRoot")
    WebHostBuilder()
        .UseKestrel()
        .UseContentRoot(contentRoot)
        .UseIISIntegration()
        .UseWebRoot(webRoot)
        .Configure(Action<IApplicationBuilder> configureApp)
        .ConfigureServices(configureServices)
        .ConfigureLogging(configureLogging)
        .Build()
        .Run()
    0