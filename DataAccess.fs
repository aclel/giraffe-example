module DataAccess

open System.IO
open Microsoft.Data.Sqlite
open FSharp.Data.Sql

open QueryHelpers

open NPoco

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

    let sq = { query = ""; parameters = Map []}
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


// ORACLE
// Insert connection string here
let [<Literal>] connectionString = "User Id=waters_dev;Password=waters_dev;Data Source=(DESCRIPTION=(ADDRESS_LIST=(ADDRESS=(PROTOCOL=TCP)(HOST=wqscidev)(PORT=1521)))(CONNECT_DATA=(SERVICE_NAME=orcl)));"
let [<Literal>] resolutionPath =  __SOURCE_DIRECTORY__ + "/temp"


type sql = SqlDataProvider<Common.DatabaseProviderTypes.ORACLE, connectionString, ResolutionPath = resolutionPath>


[<CLIMutable>]
type Basin = { Id: int64; Code: string; Name: string }

let addBasin (basin: Basin) =
    let ctx = sql.GetDataContext()
    ctx.WatersDev.Basin.``Create(CODE, NAME)``(basin.Code, basin.Name) |> ignore
    ctx.SubmitUpdatesAsync() |> Async.StartAsTask |> ignore
