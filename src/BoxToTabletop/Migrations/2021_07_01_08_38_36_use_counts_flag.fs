namespace BoxToTabletop.Migrations

open FluentMigrator

[<Migration(2021_07_01_08_38_36L)>]
type _2021_07_01_08_38_36_use_counts_flag () =
  inherit Migration ()

  override __.Up () =
    base.Execute.Sql("""
      ALTER TABLE projects
        ADD COLUMN use_counts bool NOT NULL DEFAULT false;
    """)
  override __.Down () = ()
