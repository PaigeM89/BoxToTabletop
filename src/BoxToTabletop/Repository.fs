module BoxToTabletop.Repository

open System
open System.Data
open Dapper.FSharp
open Dapper.FSharp.PostgreSQL
open DbTypes
open System.Threading
open System.Threading.Tasks
open FsToolkit.ErrorHandling
open Npgsql.FSharp

let createDbConnection (connstr : string) () =
    let props : Sql.SqlProps = Sql.connect connstr
    Npgsql.FSharp.Sql.createConnection props

let loadUnit (conn : IDbConnection) (id : Guid) () =
    select {
        table "units"
        where (eq "id" id)
    } |> conn.SelectAsync<Unit>

let loadUnits (conn : IDbConnection) () =
    select {
        table "units"
    }
    |> conn.SelectAsync<Unit>
    |> Task.map (List.ofSeq >> List.map (fun x -> x.ToDomainType()))

let insertUnit (conn : IDbConnection) (unit : Unit) =
    insert {
        table "units"
        value unit
    } |> conn.InsertAsync

let updateUnit (conn : IDbConnection) (unit : Unit) =
    update {
        table "units"
        set unit
        where (eq "Id" unit.id)
    } |> conn.UpdateAsync
