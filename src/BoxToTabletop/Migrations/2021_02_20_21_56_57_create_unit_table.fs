namespace BoxToTabletop.Migrations

open FluentMigrator
open BoxToTabletop.DbTypes
open BoxToTabletop.Repository

[<Migration(2021_02_20_21_56_57L)>]
type _2021_02_20_21_56_57_create_unit_table () =
  inherit Migration ()

  override __.Up () =
      base.Execute.Sql("""
        CREATE TABLE units (
            id uuid NOT NULL PRIMARY KEY,
            project_id uuid NOT NULL,
            name varchar(100) NOT NULL,
            models INT NOT NULL,
            assembled INT,
            primed INT,
            painted INT,
            based INT
        );
    """)

  override __.Down () = ()
