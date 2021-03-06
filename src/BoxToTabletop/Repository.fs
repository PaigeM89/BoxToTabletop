namespace BoxToTabletop

module Repository =

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
        |> Task.map (List.ofSeq >> List.map (fun x -> x.ToDomainType()) >> List.sortBy (fun x -> x.Priority))

    let insertUnit (conn : CreateConn) (unit : Domain.Types.Unit) =
        let unit = DbTypes.Unit.FromDomainType unit
        insert {
            table "units"
            value unit
        }
        |> conn().InsertAsync

    let insertUnits (conn : CreateConn) (units : Domain.Types.Unit list) =
        let units = units |> List.map DbTypes.Unit.FromDomainType
        insert {
            table "units"
            values units
        }
        |> conn().InsertAsync

    let updateUnit (conn : CreateConn) (unit : Domain.Types.Unit) =
        let unit = DbTypes.Unit.FromDomainType unit
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

    let loadProjectsForUser (conn : CreateConn) (userId : string) =
        select {
            table "projects"
            where (eq "owner_id" userId)
        } |> conn().SelectAsync<Project>
        |> Task.map (List.ofSeq >> List.map (fun x -> x.ToDomainType())
        )
    let loadProject (conn : CreateConn) (id : Guid) =
        select {
            table "projects"
            where (eq "id" id)
            take 1
        } |> conn().SelectAsync<Project>
        |> Task.map (List.ofSeq >> List.map (fun x -> x.ToDomainType()) >> List.tryHead)

    let insertProject (conn : CreateConn) (project : Domain.Types.Project) =
        let project = DbTypes.Project.FromDomainType project
        insert {
            table "projects"
            value project
        }
        |> conn().InsertAsync

    let updateProject (conn : CreateConn) (project : Domain.Types.Project) =
        let project = DbTypes.Project.FromDomainType project
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
    let updatePriority (conn : CreateConn) (unitId : Guid) (priority : int) =
        update {
            table "units"
            set {| priority = priority |}
            where (eq "id"  unitId)
        } |> conn().UpdateAsync

    type ILoadProjects =
        abstract member Load : Guid -> Task<Domain.Types.Project option>
        abstract member LoadForUser : string -> Task<Domain.Types.Project list>

    type IModifyProjects =
        abstract member Save : Domain.Types.Project -> Task<int>
        abstract member Update : Domain.Types.Project -> Task<unit>
        abstract member Delete: Guid -> Task<unit>

    type ILoadUnits =
        abstract member Load : Guid -> Task<Domain.Types.Unit option>
        abstract member LoadForProject : Guid -> Task<Domain.Types.Unit list>
    
    type IModifyUnits =
        abstract member Save : Domain.Types.Unit -> Task<int>
        abstract member Update : Domain.Types.Unit -> Task<int>
        //abstract member UpdateMany : DbTypes.Unit -> Task<unit>
        abstract member Delete: Guid -> Task<int>
        abstract member SetPriority : Guid -> int -> Task<int>

    // *****************
    // IMPLEMENTATIONS
    // *****************

    type ProjectLoader(connCreator : CreateConn) =
        interface ILoadProjects with
            member this.Load id = loadProject connCreator id
            member this.LoadForUser userId = loadProjectsForUser connCreator userId

    type ProjectModifier(conn : CreateConn) =
        interface IModifyProjects with
            member this.Save project = insertProject conn project
            member this.Update project = updateProject conn project |> Task.map ignore
            member this.Delete id = deleteProject conn id |> Task.map ignore

    type UnitLoader(conn : CreateConn) =
        interface ILoadUnits with
            member this.Load id = loadUnit conn id
            member this.LoadForProject id = loadUnits conn id

    type UnitModifier(conn : CreateConn) =
        interface IModifyUnits with
            member this.Save u = insertUnit conn u
            member this.Update u = updateUnit conn u
            //member this.UpdateMany units = updateUnit
            member this.Delete id = deleteUnit conn id
            member this.SetPriority id p = updatePriority conn id p
    
