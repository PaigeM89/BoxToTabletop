namespace BoxToTabletop

open System.Data
open System.Reflection
open BoxToTabletop.Logging
open BoxToTabletop.LogHelpers.Operators

module DatabaseInitialization =
    open Npgsql
    open Npgsql.FSharp

    let devDockerConnectionString() =
        Sql.host "localhost"
        |> Sql.database "boxtotabletop"
        |> Sql.username "postgres"
        |> Sql.password "postgres"
        |> Sql.port 5432

    let createDbConnection (bldr : NpgsqlConnectionStringBuilder) =
        Sql.connect (bldr.ConnectionString)
        |> Sql.createConnection

module AssemblyInfo =

    let metaDataValue (mda: AssemblyMetadataAttribute) = mda.Value

    let getMetaDataAttribute (assembly: Assembly) key =
        assembly.GetCustomAttributes(typedefof<AssemblyMetadataAttribute>)
        |> Seq.cast<AssemblyMetadataAttribute>
        |> Seq.find (fun x -> x.Key = key)

    let getReleaseDate assembly =
        "ReleaseDate"
        |> getMetaDataAttribute assembly
        |> metaDataValue

    let getGitHash assembly =
        "GitHash"
        |> getMetaDataAttribute assembly
        |> metaDataValue

    let getVersion assembly =
        "AssemblyVersion"
        |> getMetaDataAttribute assembly
        |> metaDataValue

    let assembly = lazy (Assembly.GetEntryAssembly())

    let printVersion() =
        let version = assembly.Force().GetName().Version
        printfn "%A" version

    let printInfo() =
        let assembly = assembly.Force()
        let name = assembly.GetName()
        let version = assembly.GetName().Version
        let releaseDate = getReleaseDate assembly
        let githash = getGitHash assembly
        printfn "%s - %A - %s - %s" name.Name version releaseDate githash

module Main =
    open Argu
    open Microsoft.AspNetCore.Builder
    open Microsoft.AspNetCore.Hosting
    open Microsoft.Extensions.Hosting
    open Microsoft.Extensions.DependencyInjection
    open Giraffe
    open BoxToTabletop
    open BoxToTabletop.Configuration
    open Serilog

    open Serilog.Core
    open Serilog.Events

    type ThreadIdEnricher() =
      interface ILogEventEnricher with
        member this.Enrich(logEvent : LogEvent, propertyFactory: ILogEventPropertyFactory) =
          logEvent.AddPropertyIfAbsent(
            propertyFactory.CreateProperty(
              "ThreadId",
              System.Threading.Thread.CurrentThread.ManagedThreadId
           )
          )

    let log =
         LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.FromLogContext()
            .Enrich.With(new ThreadIdEnricher())
            .WriteTo.File("log.txt",
              outputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:W3}] ({ThreadId}) {Message}{NewLine}{Exception}"
            )
            .WriteTo.Console(
              outputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:W3}] ({ThreadId}) {Message}{NewLine}{Exception}"
            )
            .CreateLogger()

    LogProvider.setLoggerProvider (Providers.SerilogProvider.create ())
    Serilog.Log.Logger <- log


    type CreateConn = unit -> System.Data.IDbConnection
    let createDependencies (createConnection : unit -> System.Data.IDbConnection) =
        {
            Routing.Dependencies.createConnection = createConnection
            Routing.Dependencies.loadAllUnits = Repository.loadUnits
            Routing.Dependencies.loadUnit = Repository.loadUnit
            Routing.Dependencies.saveUnit = Repository.insertUnit
            Routing.Dependencies.updateUnit = Repository.updateUnit
            Routing.Dependencies.deleteUnit = Repository.deleteUnit
            Routing.Dependencies.loadAllProjects = Repository.loadAllProjects
            Routing.Dependencies.loadProject = Repository.loadProject
            Routing.Dependencies.saveProject = Repository.saveProject
            Routing.Dependencies.deleteProject = Repository.deleteUnit
            Routing.Dependencies.updateProject = Repository.updateProject
            Routing.Dependencies.updatePriority = Repository.updatePriority
        }

    let parseAndStart (results : ParseResults<CLIArguments>) =
        let logger = LogProvider.getLoggerByFunc()
        match tryParseAuth0Config results with
        | Ok auth0Config ->
            let postgresHost = results.GetResult (PostgresHost, defaultValue = "localhost")
            let postgresConf = { PostgresConfig.Default() with PostgresHost = postgresHost} 
            let config = ApplicationConfig.Create postgresConf auth0Config
            !! "Config is {config}" >>!+ ("config", config) |> logger.info
            let connstr = config.PostgresConfig.PostgresConnectionString()
            MigrationRunner.run connstr

            let deps = Repository.createDbConnection connstr |> createDependencies
            let host = BoxToTabletop.Webhost.buildHost config deps
            !! "Running web host..." |> logger.info
            host.Run()
        | Error e ->
            !! "Unable to start application: {e}"
            >>!+ ("e", e)
            |> logger.warn
            printUsage() |> printfn "%s"

    [<EntryPoint>]
    let main (argv: string array) =
        let logger = LogProvider.getLoggerByFunc()
        logger.info (!! "Launching application.")

        let results = parseArgs argv
        
        if results.Contains Version then AssemblyInfo.printVersion()
        if results.Contains Info then AssemblyInfo.printInfo()
        
        if results.Contains Usage |> not then
            parseAndStart results
        else
            printUsage() |> printfn "%s"
        0
