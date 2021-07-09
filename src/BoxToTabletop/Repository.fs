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

    module Units =

        let loadUnit (conn : CreateConn) (id : Guid) =
            select {
                table "units"
                where (eq "id" id)
            } |> conn().SelectAsync<Unit>
            |> Task.map Seq.tryHead

        let loadUnits (conn : CreateConn) (projId : Guid) =
            select {
                table "units"
                where (eq "project_id" projId)
                orderBy "priority" Asc
            }
            |> conn().SelectAsync<Unit>
            |> Task.map (List.ofSeq)

        let insertUnit (conn : CreateConn) (unit : Unit) =
            insert {
                table "units"
                value unit
            }
            |> conn().InsertAsync

        let insertUnits (conn : CreateConn) (units : Unit list) =
            insert {
                table "units"
                values units
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

    module Projects =

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
            |> Task.map (List.ofSeq)

        let loadProject (conn : CreateConn) (id : Guid) =
            select {
                table "projects"
                where (eq "id" id)
                take 1
            } |> conn().SelectAsync<Project>
            |> Task.map (List.ofSeq >> List.tryHead)

        let insertProject (conn : CreateConn) (project : Project) =
            insert {
                table "projects"
                value project
            }
            |> conn().InsertAsync

        let updateProject (conn : CreateConn) (project : Project) =
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

    module ProjectColumns =
        let loadAllForProject (conn : CreateConn) (projectId : Guid) =
            select {
                table "columns"
                leftJoin "project_columns" "column_id" "columns.id"
                where (eq "project_columns.project_id" projectId)
            } |> conn().SelectAsync<Column, ProjectColumn>

        let load (conn : CreateConn) (columnId : Guid) (projectId : Guid) =
            select {
                table "project_columns"
                where (eq "column_id" columnId + eq "project_id" projectId)
            } |> conn().SelectAsync<ProjectColumn> |> Task.map (Seq.tryHead)

        let insertProjectColumn (conn : CreateConn) (cr : DbTypes.ProjectColumn) =
            insert {
                table "project_columns"
                value cr
            }
            |> conn().InsertAsync

        let updateProjectColumn (conn : CreateConn) (pc : DbTypes.ProjectColumn) =
            update {
                table "project_columns"
                set pc
                where (eq "project_id" pc.project_id + eq "column_id" pc.column_id)
            } |> conn().UpdateAsync

        let deleteProjectColumn (conn : CreateConn) (projectId : Guid) (columnId : Guid) =
            delete {
                table "project_columns"
                where (eq "project_id"projectId)
                where (eq "column_id" columnId)
            } |> conn().DeleteAsync

    module UnitColumns =
        // let loadAllForProject (conn : CreateConn) (projectId : Guid) =
        //     select {
        //         table "columns"
        //         leftJoin "unit_columns" "column_id" "columns.id"
        //         where (eq "project_id" projectId)
        //     } |> conn().SelectAsync<Column, UnitColumn>

        let loadAllForUnit (conn : CreateConn) (unitId : Guid) =
            select {
                table "columns"
                leftJoin "unit_columns" "column_id" "columns.id"
                where (eq "unit_columns.unit_id" unitId)
            } |> conn().SelectAsync<Column, UnitColumn>

        let insertUnitColumn (conn : CreateConn) (uc : DbTypes.UnitColumn) =
            insert {
                table "unit_columns"
                value uc
            } |> conn().InsertAsync

        let updateUnitColumn (conn : CreateConn) (uc : DbTypes.UnitColumn) =
            update {
                table "unit_columns"
                set uc
                where (eq "unit_id" uc.unit_id)
                where (eq "column_id" uc.column_id)
            } |> conn().UpdateAsync

        let deleteUnitColumn (conn : CreateConn) (unitId: Guid) (columnId : Guid) =
            delete {
                table "unit_columns"
                where (eq "unit_id" unitId)
                where (eq "column_id" columnId)
            } |> conn().DeleteAsync

    type ILoadProjects =
        abstract member Load : Guid -> Task<DbTypes.Project option>
        abstract member LoadForUser : string -> Task<DbTypes.Project list>
        abstract member LoadColumnsForProject : Guid -> Task<(DbTypes.Column * DbTypes.ProjectColumn) seq>
        abstract member LoadColumn : Guid -> Guid -> Task<DbTypes.ProjectColumn option>

    type IModifyProjects =
        abstract member Save : Project -> Task<int>
        abstract member Update : Project -> Task<unit>
        abstract member Delete: Guid -> Task<unit>
        abstract member SaveNewColumn : DbTypes.ProjectColumn -> Task<int>
        abstract member UpdateColumn : DbTypes.ProjectColumn -> Task<int>

    type ILoadUnits =
        abstract member Load : Guid -> Task<Unit option>
        abstract member LoadForProject : Guid -> Task<Unit list>
    
    type IModifyUnits =
        abstract member Save : Unit -> Task<int>
        abstract member Update : Unit -> Task<int>
        //abstract member UpdateMany : DbTypes.Unit -> Task<unit>
        abstract member Delete: Guid -> Task<int>
        abstract member SetPriority : Guid -> int -> Task<int>

    // *****************
    // IMPLEMENTATIONS
    // *****************

    type ProjectLoader(connCreator : CreateConn) =
        interface ILoadProjects with
            member this.Load id = Projects.loadProject connCreator id
            member this.LoadForUser userId = Projects.loadProjectsForUser connCreator userId
            member this.LoadColumnsForProject projectId = ProjectColumns.loadAllForProject connCreator projectId
            member this.LoadColumn columnId projectId = ProjectColumns.load connCreator columnId projectId

    type ProjectModifier(conn : CreateConn) =
        interface IModifyProjects with
            member this.Save project = Projects.insertProject conn project
            member this.Update project = Projects.updateProject conn project |> Task.map ignore
            member this.Delete id = Projects.deleteProject conn id |> Task.map ignore
            member this.SaveNewColumn col = ProjectColumns.insertProjectColumn conn col
            member this.UpdateColumn col = ProjectColumns.updateProjectColumn conn col

    type UnitLoader(conn : CreateConn) =
        interface ILoadUnits with
            member this.Load id = Units.loadUnit conn id
            member this.LoadForProject id = Units.loadUnits conn id

    type UnitModifier(conn : CreateConn) =
        interface IModifyUnits with
            member this.Save u = Units.insertUnit conn u
            member this.Update u = Units.updateUnit conn u
            //member this.UpdateMany units = updateUnit
            member this.Delete id = Units.deleteUnit conn id
            member this.SetPriority id p = Projects.updatePriority conn id p
    
