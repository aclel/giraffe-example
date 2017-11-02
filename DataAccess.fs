module DataAccess

open System.IO
open Microsoft.Data.Sqlite

open QueryHelpers

open NPoco
open NPoco.FluentMappings

open Oracle.ManagedDataAccess.Client


[<CLIMutable>]
type User = { Id: int64; Name: Option<string> }


// Optional parameters that can be filtered on when searching for a user
[<CLIMutable>]
type UserFilter = { Id: Option<int64>; Name: Option<string> }

// SQLite3
let connString = "Filename=" + Path.Combine(Directory.GetCurrentDirectory(), "Sample.db")

let getUserQuery filter =
    let query = "select id, name from user"

    let idParam = where "id" "=" "@id" filter.Id
    let nameParam = where "name" "=" "@name" filter.Name

    let sq = { query = ""; parameters = Collections.Map []}
    sq
    |> combineAnd idParam
    |> combineAnd nameParam
    |> combineQueryParts query

// Dapper
let getUser filter =
    let query = getUserQuery filter
    use connection = new SqliteConnection(connString)

    connection
    |> dapperMapParametrizedQuery<User> query.query query.parameters
    |> Seq.head


 // NPoco
let addUser (user : User) =
    use conn = new SqliteConnection(connString)
    conn.Open()
    use db = new Database(conn)
    db.Insert(user) |> ignore


type Basin = { Id: int64; Code: string; Name: string }

type BasinMapping() = 
    inherit Map<Basin>()

    do base.PrimaryKey(fun x -> x.Id :> obj) |> ignore
    do base.TableName("BASIN") |> ignore
    do 
        base.Columns(fun x -> 
            x.Column(fun y -> y.Name) |> ignore
            x.Column(fun y -> y.Code) |> ignore
        ) |> ignore


type Mapper() =
    inherit DefaultMapper()

let dbFactory = 
    let fluentConfig = FluentMappingConfiguration.Configure(new BasinMapping())
    let connString = "User Id=waters_dev;Password=waters_dev;Data Source=(DESCRIPTION=(ADDRESS_LIST=(ADDRESS=(PROTOCOL=TCP)(HOST=wqscidev)(PORT=1521)))(CONNECT_DATA=(SERVICE_NAME=orcl)));"
    use conn = new OracleConnection()
    conn.ConnectionString <- connString
    conn.Open()

    DatabaseFactory.Config(fun x ->
        x.UsingDatabase(fun () -> new Database(conn)) |> ignore
        x.WithFluentConfig(fluentConfig) |> ignore
        x.WithMapper(new Mapper()) |> ignore
    )

let addBasin (basin : Basin) =
    let fluentConfig = FluentMappingConfiguration.Configure(new BasinMapping())
    let connString = "User Id=waters_dev;Password=waters_dev;Data Source=(DESCRIPTION=(ADDRESS_LIST=(ADDRESS=(PROTOCOL=TCP)(HOST=wqscidev)(PORT=1521)))(CONNECT_DATA=(SERVICE_NAME=orcl)));"
    use conn = new OracleConnection()
    conn.ConnectionString <- connString
    conn.Open()

    let f = DatabaseFactory.Config(fun x ->
        x.UsingDatabase(fun () -> new Database(conn)) |> ignore
        x.WithFluentConfig(fluentConfig) |> ignore
        x.WithMapper(new Mapper()) |> ignore
    )
    let db = f.GetDatabase()
    db.Insert(basin) |> ignore

[<CLIMutable>]
type BasinFilter = { Id: Option<int64>; Code: Option<string>; Name: Option<string> }


let getBasinQuery filter =
    let query = "select id, code, name from basin"

    let idParam = where "id" "=" "@id" filter.Id
    let codeParam = where "code" "=" "@code" filter.Code
    let nameParam = where "name" "=" "@name" filter.Name

    let sq = { query = ""; parameters = Collections.Map []}
    sq
    |> combineAnd idParam
    |> combineAnd codeParam
    |> combineAnd nameParam
    |> combineQueryParts query

let getBasin filter = 
    let query = getBasinQuery filter
    let connString = "User Id=waters_dev;Password=waters_dev;Data Source=(DESCRIPTION=(ADDRESS_LIST=(ADDRESS=(PROTOCOL=TCP)(HOST=wqscidev)(PORT=1521)))(CONNECT_DATA=(SERVICE_NAME=orcl)));"
    use conn = new OracleConnection()
    conn.ConnectionString <- connString
    conn.Open()

    conn
    |> dapperMapParametrizedQueryOracle<Basin> query.query query.parameters
    |> Seq.head