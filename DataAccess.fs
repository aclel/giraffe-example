module DataAccess

open System.IO
open Microsoft.Data.Sqlite

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
