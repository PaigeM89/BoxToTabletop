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

type CreateConn = unit -> System.Data.IDbConnection

let loadUnit (conn : CreateConn) (id : Guid) =
    select {
        table "units"
        where (eq "id" id)
    } |> conn().SelectAsync<Unit>
    |> Task.map (Seq.map (fun x -> x.ToDomainType()) >> Seq.tryHead)

let loadUnits (conn : CreateConn) (projId : Guid) =
    select {
        table "units"
        where (eq "project_id" projId)
    }
    |> conn().SelectAsync<Unit>
    |> Task.map (List.ofSeq >> List.map (fun x -> x.ToDomainType()))

let insertUnit (conn : CreateConn) (unit : Unit) =
    insert {
        table "units"
        value unit
    }
    |> conn().InsertAsync

let updateUnit (conn : CreateConn) (unit : Unit) =
    update {
        table "units"
        set unit
        where (eq "id" unit.id)
    } |> conn().UpdateAsync

let deleteUnit (conn : CreateConn) (id : Guid) =
    delete {
        table "units"
        where (eq "id" id)
    } |> conn().DeleteAsync

let loadAllProjects (conn : CreateConn) =
    select {
        table "projects"
    } |> conn().SelectAsync<Project>
    |> Task.map (List.ofSeq >> List.map (fun x -> x.ToDomainType()))

let loadProject (conn : CreateConn) (id : Guid) =
    select {
        table "projects"
        where (eq "id" id)
        take 1
    } |> conn().SelectAsync<Project>
    |> Task.map (List.ofSeq >> List.map (fun x -> x.ToDomainType()) >> List.tryHead)

let saveProject (conn : CreateConn) (project : DbTypes.Project) =
    insert {
        table "projects"
        value project
    }
    |> conn().InsertAsync

let updateProject (conn : CreateConn) (project : DbTypes.Project) =
    update {
        table "projects"
        set project
        where (eq "id" project.id)
    } |> conn().UpdateAsync

let deleteProject (conn : CreateConn) (projectId : Guid) =
    delete {
        table "projects"
        where (eq "id" projectId)
    } |> conn().DeleteAsync

/// Updates the priority value for a single unit.
let updatePriority (conn : CreateConn) (projectId : Guid) (unitId : Guid) (priority : int) =
    update {
        table "units"
        set {| priority = priority |}
        where (eq "id"  unitId)
    } |> conn().UpdateAsync

//let updatePriorities (props: Sql.SqlProps) (priorities : (int * Guid) list) =
//    props
//    |> Sql.executeNonQueryAsync """
//        UPDATE unit_priorities
//            SET
//
//"""
