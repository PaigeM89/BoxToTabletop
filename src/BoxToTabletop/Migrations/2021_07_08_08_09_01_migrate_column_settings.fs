namespace BoxToTabletop.Migrations

open FluentMigrator
open BoxToTabletop
open System
open System.Threading.Tasks

[<Migration(2021_07_08_08_09_01L)>]
type _2021_07_08_08_09_01_migrate_column_settings () =
  inherit Migration ()

  override __.Up () =

    let connStr = base.ConnectionString
    let conn = Repository.createDbConnection connStr
    let projects = Repository.Projects.loadAllProjects conn |> Async.AwaitTask |> Async.RunSynchronously
    let modelRelation (project :DbTypes.Project) = {
      DbTypes.ProjectColumn.project_id = project.id
      DbTypes.ProjectColumn.column_id = Guid.Parse("49ACB90F-559D-4C93-9919-F908F28192E8")
      DbTypes.ProjectColumn.is_visible = true
      DbTypes.ProjectColumn.is_switch = false
    }
    let assembledRelation (project : DbTypes.Project) = { 
        DbTypes.ProjectColumn.project_id = project.id
        DbTypes.ProjectColumn.column_id = Guid.Parse("b777f9ef-c985-4c0e-8b99-a367ed888c2f")
        DbTypes.ProjectColumn.is_visible = project.assembled_visible
        DbTypes.ProjectColumn.is_switch = false
      }
    let primedRelation (project :DbTypes.Project) = {
      DbTypes.ProjectColumn.project_id = project.id
      DbTypes.ProjectColumn.column_id = Guid.Parse("2879ce8b-8dc4-4ea3-8dc2-4d8f0be3278a")
      DbTypes.ProjectColumn.is_visible = project.primed_visible
      DbTypes.ProjectColumn.is_switch = false
    }
    let paintedRelation (project :DbTypes.Project) = {
      DbTypes.ProjectColumn.project_id = project.id
      DbTypes.ProjectColumn.column_id = Guid.Parse("27c27bff-7715-43dd-9563-ea9d26bcdd35")
      DbTypes.ProjectColumn.is_visible = project.painted_visible
      DbTypes.ProjectColumn.is_switch = false
    }
    let basedRelation (project :DbTypes.Project) = {
      DbTypes.ProjectColumn.project_id = project.id
      DbTypes.ProjectColumn.column_id = Guid.Parse("ab3a65bb-514a-4b5c-94a5-95b7f4a0c50f")
      DbTypes.ProjectColumn.is_visible = project.based_visible
      DbTypes.ProjectColumn.is_switch = false
    }
    let pointsRelation (project :DbTypes.Project) = {
      DbTypes.ProjectColumn.project_id = project.id
      DbTypes.ProjectColumn.column_id = Guid.Parse("991cc651-a59f-456e-bb4d-89ea70824242")
      DbTypes.ProjectColumn.is_visible = project.points_visible
      DbTypes.ProjectColumn.is_switch = false
    }
    let powerRelation (project :DbTypes.Project) = {
      DbTypes.ProjectColumn.project_id = project.id
      DbTypes.ProjectColumn.column_id = Guid.Parse("DF9C8212-6DA5-4F12-9851-091A4AC3B227")
      DbTypes.ProjectColumn.is_visible = project.power_visible
      DbTypes.ProjectColumn.is_switch = false
    }
    
    let projectColumns =
      projects
      |> List.collect (fun project ->
        let project = DbTypes.Project.FromDomainType project
        [
          modelRelation project
          assembledRelation project
          primedRelation project
          paintedRelation project
          basedRelation project
          pointsRelation project
          powerRelation project
        ]
      )

    let inserts = 
      projectColumns
      |> List.filter (fun pc ->
        let existing = Repository.ProjectColumns.loadAllForProject conn pc.project_id |> Async.AwaitTask |> Async.RunSynchronously
        existing |> List.ofSeq |> List.exists (fun (col, projcol) -> projcol.column_id = pc.column_id) |> not
      )
      |> List.map(fun pc ->
        Repository.ProjectColumns.insertProjectColumn conn pc
      )
      |> Task.WhenAll
      |> Async.AwaitTask
      |> Async.RunSynchronously

    let unitColumn unitId columnId count = {
      DbTypes.UnitColumn.unit_id = unitId
      DbTypes.UnitColumn.column_id = columnId
      DbTypes.UnitColumn.value = count
    }

    let units =
      projects
      |> List.collect (fun project ->
        Repository.Units.loadUnits conn project.Id |> Async.AwaitTask |> Async.RunSynchronously
      )

    let unitColumns = 
      units
      |> List.collect(fun unit ->
        let models = unitColumn unit.id (Guid.Parse("49ACB90F-559D-4C93-9919-F908F28192E8")) unit.models
        let priority = unitColumn unit.id (Guid.Parse("A0493E27-1AEF-4DB5-8EA9-6B692818B8B6")) unit.priority
        let assembled = unitColumn unit.id (Guid.Parse("b777f9ef-c985-4c0e-8b99-a367ed888c2f")) unit.assembled
        let primed = unitColumn unit.id (Guid.Parse("2879ce8b-8dc4-4ea3-8dc2-4d8f0be3278a")) unit.primed
        let painted = unitColumn unit.id (Guid.Parse("27c27bff-7715-43dd-9563-ea9d26bcdd35")) unit.painted
        let based = unitColumn unit.id (Guid.Parse("ab3a65bb-514a-4b5c-94a5-95b7f4a0c50f")) unit.based
        let power = unitColumn unit.id (Guid.Parse("DF9C8212-6DA5-4F12-9851-091A4AC3B227")) unit.power
        let points = unitColumn unit.id (Guid.Parse("991cc651-a59f-456e-bb4d-89ea70824242")) unit.points
        [
          models
          priority
          assembled
          primed
          painted
          based
          power
          points
        ]
      )

    let ucInserts =
      unitColumns
      |> List.filter (fun uc ->
        let existing = Repository.UnitColumns.loadAllForUnit conn uc.unit_id |> Async.AwaitTask |> Async.RunSynchronously
        existing |> List.ofSeq |> List.exists (fun (_, unitCol) -> unitCol.column_id = uc.column_id) |> not
      )
      |> List.map(fun uc -> Repository.UnitColumns.insertUnitColumn conn uc)
      |> Task.WhenAll
      |> Async.AwaitTask
      |> Async.RunSynchronously

    printfn $"Project column inserts: %i{Array.sum inserts}. Unit column inserts: %i{Array.sum ucInserts}"
  override __.Down () = ()
