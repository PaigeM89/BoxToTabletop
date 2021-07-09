namespace BoxToTabletop.Migrations

open FluentMigrator
open System
open BoxToTabletop
open BoxToTabletop.DbTypes
open Dapper.FSharp
open Dapper.FSharp.PostgreSQL

[<Migration(2021_07_08_20_03_58L)>]
type _2021_07_08_20_03_58_add_column_sort_order () =
  inherit Migration ()

  let modelColumnId = Guid.Parse("49ACB90F-559D-4C93-9919-F908F28192E8")
  let assembledColumnId = Guid.Parse("b777f9ef-c985-4c0e-8b99-a367ed888c2f")
  let primedColumnId = Guid.Parse("2879ce8b-8dc4-4ea3-8dc2-4d8f0be3278a")
  let paintedColumnId = Guid.Parse("27c27bff-7715-43dd-9563-ea9d26bcdd35")
  let basedColumnId = Guid.Parse("ab3a65bb-514a-4b5c-94a5-95b7f4a0c50f")
  let pointsColumnId = Guid.Parse("991cc651-a59f-456e-bb4d-89ea70824242")
  let powerColumnId = Guid.Parse("DF9C8212-6DA5-4F12-9851-091A4AC3B227")


  override __.Up () =
    base.Execute.Sql("""
      ALTER TABLE project_columns 
        ADD sort_order INT NOT NULL DEFAULT 0
    """)


  override __.Down () = ()
