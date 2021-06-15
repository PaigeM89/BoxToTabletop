namespace BoxToTabletop

open System
open System.Text
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
open Microsoft.IdentityModel.Tokens
open Microsoft.AspNetCore.Authentication.JwtBearer

module Webhost =
    open BoxToTabletop
    open BoxToTabletop.Routing
    open BoxToTabletop.Repository

    let configureCors (config : ApplicationConfig) (builder : CorsPolicyBuilder) =
        //let localUrls = [|"http://localhost:8090"; "http://localhost:5000"|]
        builder.WithOrigins(config.CorsOrigins).AllowAnyMethod().AllowAnyHeader() |> ignore

    let configureApp config (app : IApplicationBuilder) =
        // Add Giraffe to the ASP.NET Core pipeline
        app
            .UseCors(configureCors config)
            .UseAuthentication()
            .UseGiraffe (Routing.webApp())


    let configureJwtServices (config : ApplicationConfig) (svcs : IServiceCollection) =
        let issuer = Jwt.createIssuer config
        let validationParams = Jwt.createValidationParams config

        svcs
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, fun options ->
                options.Authority <- issuer
                options.TokenValidationParameters <- validationParams
                options.SaveToken <- true
                options.RequireHttpsMetadata <- false
            )

    let configureDependencyInjection (config : ApplicationConfig) (svcs : IServiceCollection) =
        let connFunc = config.PostgresConfig.PostgresConnectionString() |> Repository.createDbConnection
        svcs
            .AddScoped<ILoadProjects, ProjectLoader>(fun _ -> new ProjectLoader(connFunc) )
            .AddScoped<IModifyProjects, ProjectModifier>(fun _ -> new ProjectModifier(connFunc) )
            .AddScoped<ILoadUnits, UnitLoader>(fun _ -> new UnitLoader(connFunc) )
            .AddScoped<IModifyUnits, UnitModifier>(fun _ -> new UnitModifier(connFunc) )
            .AddSingleton<ApplicationConfig>(fun _ -> config)

    let configureServices (config : ApplicationConfig) (services : IServiceCollection) =
        // Add Giraffe dependencies
        services
            .AddCors()
            .AddGiraffe()
            |> configureDependencyInjection config
            |> configureJwtServices config
            |> ignore

    let buildHost (config : ApplicationConfig) =
        Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(
                fun webHostBuilder ->
                    webHostBuilder
                        .Configure(configureApp config)
                        .ConfigureServices(configureServices config)
                        |> ignore)
            .Build()
