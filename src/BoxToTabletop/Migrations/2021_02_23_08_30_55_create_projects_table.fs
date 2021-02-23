namespace BoxToTabletop.Migrations

open FluentMigrator

[<Migration(2021_02_23_08_30_55L)>]
type _2021_02_23_08_30_55_create_projects_table () =
  inherit Migration ()

  override __.Up () =
      base.Execute.Sql( """
CREATE TABLE projects (
    id uuid NOT NULL PRIMARY KEY,
    name varchar(100) NOT NULL,
    is_public bool NOT NULL,
    assembled_visible bool NOT NULL,
    primed_visible bool NOT NULL,
    painted_visible bool NOT NULL,
    based_visible bool NOT NULL
);

ALTER TABLE units
  ADD CONSTRAINT fk_projects FOREIGN KEY(project_id) REFERENCES projects(id);

""")
  override __.Down () =
      base.Execute.Sql("""
ALTER TABLE units DROP CONSTRAINT fk_projects;

DROP TABLE projects;
""")
