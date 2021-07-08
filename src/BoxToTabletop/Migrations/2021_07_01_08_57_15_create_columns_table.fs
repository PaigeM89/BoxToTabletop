namespace BoxToTabletop.Migrations

open FluentMigrator

[<Migration(2021_07_01_08_57_15L)>]
type _2021_07_01_08_57_15_create_columns_table () =
  inherit Migration ()

  (*
    Columns - table of possible columns to use for a project
              This is setting us up to have dynamic columns in the future
    project_columns - Table of project <-> columns relations
                      This both the relation between a project and the columns it has configured; "removing" a column
                      should hide it, not delete it, so we can restore values if a user later re-adds that column.
    unit_columns - Table of unit <-> columns relations, which stores the values a unit has associated with a column.


    Things worth noting:
      * A column can be configured to be switchable, or never switchable (so a column can be configured to always require numbers)
      * A project column can be configured to be switchable (assuming that column is switchable), so this can be tweaked per project
  *)

  override __.Up () =
    base.Execute.Sql("""
      CREATE TABLE columns (
        id uuid NOT NULL PRIMARY KEY,
        name varchar(100) NOT NULL,
        description varchar(200),
        can_switch bool NOT NULL DEFAULT false,
        owner_id uuid NOT NULL
      );

      CREATE TABLE project_columns (
        project_id uuid NOT NULL,
        column_id uuid NOT NULL,
        is_visible bool NOT NULL DEFAULT false,
        is_switch bool NOT NULL DEFAULT false,

        PRIMARY KEY(project_id, column_id),
        CONSTRAINT fk_project_columns_column
          FOREIGN KEY(column_id) REFERENCES columns(id),
        CONSTRAINT fk_project_columns_project
          FOREIGN KEY(project_id) REFERENCES projects(id)
      );

      INSERT INTO columns
      VALUES
      ('49ACB90F-559D-4C93-9919-F908F28192E8', 'Models', 'The number of models in the unit.', false, '00000000-0000-0000-0000-000000000000'),
      ('A0493E27-1AEF-4DB5-8EA9-6B692818B8B6', 'Priority', 'The rank of the unit within a project.', false, '00000000-0000-0000-0000-000000000000'),
      ('f7480a42-ede5-449b-aeff-b2803caa168d', 'Purchased', 'The unit has been purchased.', true, '00000000-0000-0000-0000-000000000000'),
      ('38201612-c012-4741-ad6f-0ca519e1fef7', 'Cost', 'The cost of the unit, in whatever currency you want.', false, '00000000-0000-0000-0000-000000000000'),
      ('b777f9ef-c985-4c0e-8b99-a367ed888c2f', 'Assembled', 'The unit has been assembled.', true, '00000000-0000-0000-0000-000000000000'),
      ('2879ce8b-8dc4-4ea3-8dc2-4d8f0be3278a', 'Primed', 'The unit has been primed.', true, '00000000-0000-0000-0000-000000000000'),
      ('27c27bff-7715-43dd-9563-ea9d26bcdd35', 'Painted', 'The unit has been painted.', true, '00000000-0000-0000-0000-000000000000'),
      ('af493751-1e57-47be-8953-6139040fc878', 'Base Coated', 'The unit has had base colors painted.', true, '00000000-0000-0000-0000-000000000000'),
      ('93391a71-23a7-4960-bea3-737d3b9c1ae7', 'Detailed', 'The unit has had details painted.', true, '00000000-0000-0000-0000-000000000000'),
      ('ab3a65bb-514a-4b5c-94a5-95b7f4a0c50f', 'Based', 'The unit has been based.', true, '00000000-0000-0000-0000-000000000000'),
      ('92ff87c1-ed13-4af9-a0a3-91edae24a31c', 'Varnished', 'The unit has been varnished.', true, '00000000-0000-0000-0000-000000000000'),
      ('991cc651-a59f-456e-bb4d-89ea70824242', 'Points', 'The unit''s value in points.', false, '00000000-0000-0000-0000-000000000000'),
      ('DF9C8212-6DA5-4F12-9851-091A4AC3B227', 'Power', 'The unit''s value in power.', false, '00000000-0000-0000-0000-000000000000'),
      ('B6C7068D-5075-4DE0-A862-ABF01E07345C', 'Start Date', 'The date the unit was started.', false, '00000000-0000-0000-0000-000000000000'),
      ('7F5508E0-E3BF-4F59-A18B-E4C6C63FB317', 'End Date', 'The date the unit was finished.', false,'00000000-0000-0000-0000-000000000000'),
      ('0DE5F7B2-FE0F-4E53-9E15-5363A2ECCC63', 'Total Hours', 'The total number of hours spent on the unit.', false,'00000000-0000-0000-0000-000000000000');

      CREATE TABLE unit_columns (
        unit_id uuid NOT NULL,
        column_id uuid NOT NULL,
        value int NOT NULL DEFAULT 0,

        PRIMARY KEY(unit_id, column_id),
        CONSTRAINT fk_unit_columns_column
          FOREIGN KEY(column_id) REFERENCES columns(id),
        CONSTRAINT fk_unit_columns_unit
          FOREIGN KEY(unit_id) REFERENCES units(id)
      );
    """)

  override __.Down () = ()
