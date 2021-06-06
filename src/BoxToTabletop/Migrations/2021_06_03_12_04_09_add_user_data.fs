namespace BoxToTabletop.Migrations

open FluentMigrator

[<Migration(2021_06_03_12_04_09L)>]
type _2021_06_03_12_04_09_add_user_data () =
  inherit Migration ()

  override __.Up () =
    base.Execute.Sql("""
    ALTER TABLE projects
      ADD COLUMN power_visible bool NOT NULL DEFAULT false,
      ADD COLUMN points_visible bool NOT NULL DEFAULT false,
      ADD COLUMN use_checkboxes bool NOT NULL DEFAULT false,
      ADD COLUMN owner_id varchar(50) NOT NULL DEFAULT '';
    
    ALTER TABLE units
      ADD COLUMN power INT NOT NULL DEFAULT 0,
      ADD COLUMN points INT NOT NULL DEFAULT 0,
      ADD COLUMN owner_id varchar(50) NOT NULL DEFAULT '';

    """)
  override __.Down () = ()
