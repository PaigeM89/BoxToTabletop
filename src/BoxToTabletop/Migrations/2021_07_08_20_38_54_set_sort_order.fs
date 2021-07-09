namespace BoxToTabletop.Migrations

open FluentMigrator
open System
open BoxToTabletop
open BoxToTabletop.DbTypes
open Dapper.FSharp
open Dapper.FSharp.PostgreSQL

[<Migration(2021_07_08_20_38_54L)>]
type _2021_07_08_20_38_54_set_sort_order () =
  inherit Migration ()

  let modelColumnId = Guid.Parse("49ACB90F-559D-4C93-9919-F908F28192E8")
  let assembledColumnId = Guid.Parse("b777f9ef-c985-4c0e-8b99-a367ed888c2f")
  let primedColumnId = Guid.Parse("2879ce8b-8dc4-4ea3-8dc2-4d8f0be3278a")
  let paintedColumnId = Guid.Parse("27c27bff-7715-43dd-9563-ea9d26bcdd35")
  let basedColumnId = Guid.Parse("ab3a65bb-514a-4b5c-94a5-95b7f4a0c50f")
  let pointsColumnId = Guid.Parse("991cc651-a59f-456e-bb4d-89ea70824242")
  let powerColumnId = Guid.Parse("DF9C8212-6DA5-4F12-9851-091A4AC3B227")


  override __.Up () =

    let connStr = base.ConnectionString
    let conn = Repository.createDbConnection connStr

    let projectColumns =
      select {
        table "project_columns"
      }
      |> conn().SelectAsync<DbTypes.ProjectColumn>
      |> Async.AwaitTask
      |> Async.RunSynchronously

    let updatedColumns = 
      projectColumns
      |> Seq.map(fun pc ->
        if pc.column_id = modelColumnId then
          { pc with sort_order = 0 }
        elif pc.column_id = assembledColumnId then
          { pc with sort_order = 1 }
        elif pc.column_id = primedColumnId then
          { pc with sort_order = 2 }
        elif pc.column_id = paintedColumnId then
          { pc with sort_order = 3 }
        elif pc.column_id = basedColumnId then
          { pc with sort_order = 4 }
        elif pc.column_id = pointsColumnId then
          { pc with sort_order = 5 }
        elif pc.column_id = powerColumnId then
          { pc with sort_order = 6 }
        else
          { pc with sort_order = 10 }
      )

    updatedColumns
    |> Seq.sumBy (fun pc ->
      Repository.ProjectColumns.updateProjectColumn conn pc
      |> Async.AwaitTask
      |> Async.RunSynchronously
    )
    |> printfn "Updated %i project columns"
  override __.Down () = ()
