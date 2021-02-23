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


let createDbConnection (connstr : string) () : IDbConnection =
    let props : Sql.SqlProps = Sql.connect connstr
    Npgsql.FSharp.Sql.createConnection props :> IDbConnection

let loadUnit (conn : IDbConnection) (id : Guid) () =
    select {
        table "units"
        where (eq "id" id)
    } |> conn.SelectAsync<Unit>

let loadUnits (conn : IDbConnection) (projId : Guid) =
    select {
        table "units"
        where (eq "project_id" projId)
    }
    |> conn.SelectAsync<Unit>
    |> Task.map (List.ofSeq >> List.map (fun x -> x.ToDomainType()))

let insertUnit (conn : IDbConnection) (unit : Unit) =
    insert {
        table "units"
        value unit
    }
    |> conn.InsertAsync
    |> Task.map (fun r ->
        if r = 1 then Ok () else Error (sprintf "Wrong number of records inserted. Inserted %i records" r)
    )

let updateUnit (conn : IDbConnection) (unit : Unit) =
    update {
        table "units"
        set unit
        where (eq "id" unit.id)
    } |> conn.UpdateAsync

let deleteUnit (conn : IDbConnection) (projId : Guid) (id : Guid) =
    delete {
        table "units"
        where (eq "id" id)
        where (eq "project_id" projId)
    } |> conn.DeleteAsync

//todo: implement this in npgsql
let updateUnitPriority (conn : unit -> IDbConnection) (id : Guid) =
    ()

let loadAllProjects (conn : IDbConnection) =
    select {
        table "projects"
    } |> conn.SelectAsync<Project>
    |> Task.map (List.ofSeq >> List.map (fun x -> x.ToDomainType()))

let loadProject (conn : IDbConnection) (id : Guid) =
    select {
        table "projects"
        where (eq "id" id)
        take 1
    } |> conn.SelectAsync<Project>
    |> Task.map (List.ofSeq >> List.map (fun x -> x.ToDomainType()) >> List.tryHead)


