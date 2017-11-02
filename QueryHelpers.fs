module QueryHelpers

open System
open System.IO
open System.Collections.Generic
open Microsoft.Data.Sqlite

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


let combineQueryParts<'T> (initialQuery: string) (sq: SelectQuery) = 
    (match sq.query.Length with
        | 0 -> ""
        | _ -> sprintf " where %s" sq.query) |> fun clause -> sprintf "%s%s" initialQuery clause