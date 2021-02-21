module BoxToTabletop.MigrationRunner

open Npgsql
open Npgsql.FSharp
open FluentMigrator.Runner
open Microsoft.AspNetCore.Identity
open Microsoft.Extensions.DependencyInjection
open System
open System.Linq
open System.Threading.Tasks
open BoxToTabletop.Logging
open BoxToTabletop.LogHelpers.Operators

let logger = LogProvider.getLoggerByName("BoxToTabletop.MigrationRunner")

let createDatabaseIfNotExist (bldr : NpgsqlConnectionStringBuilder) =
    let executeReader (connection:NpgsqlConnection) query =
        connection.Open ()
        use command = new NpgsqlCommand(query, connection)
        let reader = command.ExecuteReader()
        let readerResult = reader.Read ()
        connection.Close ()
        readerResult

    let executeNonQuery (connection:NpgsqlConnection) query =
        connection.Open ()
        use command = new NpgsqlCommand(query, connection)
        command.ExecuteNonQuery () |> ignore<int>
        connection.Close ()

    let databaseCreate (connection:NpgsqlConnection) databaseName =
        !! "Creating database {db} because it does not exist" >>!- ("db", databaseName) |> logger.warn
        databaseName
         |> sprintf "Create database \"%s\" ENCODING = 'UTF8'"
         |> executeNonQuery connection

    let databaseExists (connection:NpgsqlConnection) databaseName =
        databaseName
         |> sprintf "SELECT datname FROM pg_catalog.pg_database WHERE lower(datname) = lower('%s');"
         |> executeReader connection

    let databaseName = bldr.Database
    // can't have a database set when there might not be a database created
    // connect to the administrative database to prevent this failure case
    bldr.Database <- "postgres"
    use connection = new NpgsqlConnection(bldr |> string)
    match databaseExists connection databaseName with
    | true -> ()
    | false -> databaseCreate connection databaseName
    // reset the name when done
    bldr.Database <- databaseName

let configure (connStr : string) (svcs: IServiceCollection) =
    !! "Configuring migration runner" |> logger.trace
    svcs
      .AddLogging(fun lb -> lb.AddFluentMigratorConsole() |> ignore)
      .AddSingleton<Sql.SqlProps>(Sql.connect connStr)
      |> fun svcs ->
        svcs
          .AddFluentMigratorCore()
          .ConfigureRunner(fun (builder: IMigrationRunnerBuilder) ->
            builder
              .AddPostgres()
              .WithGlobalConnectionString(connStr)
              .ScanIn(System.Reflection.Assembly.GetExecutingAssembly()).For.All()
              |> ignore
          )

let run (connStr : string): unit =
  !! "Running migrations"
  #if DEBUG
  >>! connStr
  #endif
  |> logger.info

  let services = ServiceCollection() |> configure connStr |> fun s -> s.BuildServiceProvider()

  // execute fluent migrations
  try
      let migrator = services.GetRequiredService<IMigrationRunner>()
      let builder = Npgsql.NpgsqlConnectionStringBuilder connStr
#if DEBUG
      logger.info (Log.setMessage "running migrations with connection string {connString}" >> Log.addContextDestructured "connString" (string builder))
#endif
      createDatabaseIfNotExist builder
      logger.info (Log.setMessage "Created database {name}" >> Log.addContextDestructured "name" builder.Database)
      migrator.ListMigrations()
      migrator.MigrateUp ()
  with
  | :? Npgsql.NpgsqlException as ne ->
      let outerMessage =  "Npgsql Exception During Migration"
      logger.error(
          Log.setMessage outerMessage
          >> Log.addContextDestructured "message" ne.Message
          >> Log.addContextDestructured "stack" ne.StackTrace
      )
      reraise ()
  | :? System.TypeInitializationException as tie ->
      logger.error (Log.setMessage <| sprintf "Type initialization failure for type %s: %s" tie.TypeName tie.StackTrace)
      reraise ()
  | ex ->
      let outerMessage = "General Exception During Database Migration:\n{message}\n{stack}"
      logger.error(
          Log.setMessage outerMessage
          >> Log.addContextDestructured "message" ex.Message
          >> Log.addContextDestructured "stack" ex.StackTrace
      )
      reraise ()
