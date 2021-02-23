namespace BoxToTabletop

open System
open BoxToTabletop.Configuration
open BoxToTabletop.Domain
open BoxToTabletop.Logging
open BoxToTabletop.LogHelpers.Operators
open FluentMigrator.Runner
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Giraffe

module Webhost =
    open BoxToTabletop
    open BoxToTabletop.Routing

    let configureCors (builder : CorsPolicyBuilder) =
        builder.WithOrigins("http://localhost:8090").AllowAnyMethod().AllowAnyHeader() |> ignore

    let configureApp (deps : Routing.Dependencies) (app : IApplicationBuilder) =
        // Add Giraffe to the ASP.NET Core pipeline
        app.UseCors configureCors |> ignore
        app.UseGiraffe (Routing.webApp deps)

    let configureServices (config : ApplicationConfig) (services : IServiceCollection) =
        // Add Giraffe dependencies
        services
            .AddCors()
            .AddGiraffe()
            |> ignore

    let buildHost (config : ApplicationConfig) (deps : Routing.Dependencies) =
        Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(
                fun webHostBuilder ->
                    webHostBuilder
                        .Configure(configureApp deps)
                        .ConfigureServices(configureServices config)
                        |> ignore)
            .Build()
