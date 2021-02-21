namespace BoxToTabletop

open System
open BoxToTabletop.Configuration
open BoxToTabletop.Domain
open FluentMigrator.Runner
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Giraffe

module Handlers =
    open Domain.Types
    open System.Threading
    open System.Threading.Tasks
    open FSharp.Control.Tasks.Affine
    open FsToolkit.ErrorHandling

    type CreateConn = unit -> Npgsql.NpgsqlConnection
    type Dependencies = {
        //createConnection : CreateConn// unit -> Npgsql.NpgsqlConnection
        loadAllUnits : unit -> Task<Unit list>
    }

    let listUnits (loader: unit -> Task<Unit list>) next ctx = task {
        let! units = loader()
        return! json units next ctx
    }

    let webApp (deps : Dependencies) =
        choose [
            route "/" >=> GET >=> listUnits deps.loadAllUnits
            route "/" >=> GET >=> htmlFile "index.html"
        ]

module Webhost =

    let configureApp (deps : Handlers.Dependencies) (app : IApplicationBuilder) =
        // Add Giraffe to the ASP.NET Core pipeline
        app.UseGiraffe (Handlers.webApp deps)

    let configureServices (config : ApplicationConfig) (services : IServiceCollection) =
        // Add Giraffe dependencies
        services.AddGiraffe()
        |> ignore

    let buildHost (config : ApplicationConfig) (deps : Handlers.Dependencies) =
        Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(
                fun webHostBuilder ->
                    webHostBuilder
                        .Configure(configureApp deps)
                        .ConfigureServices(configureServices config)
                        |> ignore)
            .Build()
