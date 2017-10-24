module test_giraffe.App

open System
open System.IO
open System.Collections.Generic

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Microsoft.Data.Sqlite

open Giraffe
open Giraffe.HttpHandlers
open Giraffe.Middleware
open Giraffe.Razor.HttpHandlers
open Giraffe.Razor.Middleware
open Giraffe.HttpContextExtensions
open test_giraffe.Models

open System.Data.SqlClient
open System.Dynamic
open Dapper


// Add handler to Dapper to map to Option types
type OptionHandler<'T>() =
    inherit SqlMapper.TypeHandler<option<'T>>()

    override __.SetValue(param, value) = 
        let valueOrNull = 
            match value with
            | Some x -> box x
            | None -> null

        param.Value <- valueOrNull    

    override __.Parse value =
        if isNull value || value = box DBNull.Value 
        then None
        else Some (value :?> 'T)

SqlMapper.AddTypeHandler (OptionHandler<string>())

let connString = "Filename=" + Path.Combine(Directory.GetCurrentDirectory(), "Sample.db")

let dapperQuery<'Result> (query:string) (connection:SqliteConnection) =
    connection.Query<'Result>(query)
    
let dapperParametrizedQuery<'Result> (query:string) (param:obj) (connection:SqliteConnection) : 'Result seq =
    connection.Query<'Result>(query, param)

let dapperMapParametrizedQuery<'Result> (query:string) (param : Map<string,_>) (connection:SqliteConnection) : 'Result seq =
    let expando = ExpandoObject()
    let expandoDictionary = expando :> IDictionary<string,obj>
    for paramValue in param do
        expandoDictionary.Add(paramValue.Key, paramValue.Value :> obj)

    connection |> dapperParametrizedQuery query expando


type QueryPart = { where: string; parameter: Option<obj>; parameterName: string }
type SelectQuery = { query: string; parameters: Map<string, obj> }


let where<'a> column operator parameterName (value : Option<'a>) =
    match value with
    | Some x -> { where = sprintf "%s %s %s" column operator parameterName; parameter = Some (box x); parameterName = parameterName; }
    | None -> { where = ""; parameter = None; parameterName = ""; }


let combineAnd (x: QueryPart) (sq: SelectQuery) =
    match (x.parameter, sq.query) with
    | (Some xpart, "") -> { query = x.where; parameters = Map [x.parameterName, Option.get x.parameter] }
    | (Some xpart, _) -> { query = sq.query + " and " + x.where; parameters = sq.parameters.Add(x.parameterName, Option.get x.parameter) }
    | (None, _) -> { query = sq.query; parameters = sq.parameters } 


let combineQueryParts<'T> (query: string) (sq: SelectQuery) =
    let mutable q = query
    if (sq.query.Length > 0) then
        q <- query + " where " + sq.query
    { query = q; parameters = sq.parameters; }


type User = { Id: int64; Name: Option<string> }


// Optional parameters that can be filtered on when searching for a user
[<CLIMutable>]
type UserFilter = { Id: Option<int64>; Name: Option<string> }


let getUserQuery filter =
    let query = "select id, name from user"

    let idParam = where "id" "=" "@id" filter.Id
    let nameParam = where "name" "=" "@name" filter.Name

    let sq = { query = ""; parameters = Map []}
    sq
    |> combineAnd idParam
    |> combineAnd nameParam
    |> combineQueryParts query


let getUsers connection =
    connection
    |> dapperQuery<User> "select id, name from user;"

let getUser id connection =
    connection
    |> dapperMapParametrizedQuery<User> "select id, name from user where id = @id" (Map ["id", id])
    |> Seq.head

let getUser' id name connection =
    connection
    |> dapperParametrizedQuery<User> "select id, name from user where id = @Id or name = @Name" { Id=id; Name=name }
    |> Seq.head

let getUser'' filter connection =
    let query = getUserQuery filter

    connection
    |> dapperMapParametrizedQuery<User> query.query query.parameters
    |> Seq.head



let handleGetUser =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        use conn = new SqliteConnection(connString)
        let filter = ctx.BindQueryString<UserFilter>()

        // let id = Option.get filter.Id
        // let name = Option.get filter.Name
        let users = getUser'' filter conn
        json users next ctx


// ---------------------------------
// Web app
// ---------------------------------

let webApp =
    choose [
        GET >=>
            choose [
                route "/user" >=> handleGetUser
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