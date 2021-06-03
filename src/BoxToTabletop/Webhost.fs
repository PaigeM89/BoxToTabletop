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

    let configureCors (builder : CorsPolicyBuilder) =
        builder.WithOrigins([|"http://localhost:8090"; "http://localhost:5000"|]).AllowAnyMethod().AllowAnyHeader() |> ignore

    let configureApp (deps : Routing.Dependencies) (app : IApplicationBuilder) =
        // Add Giraffe to the ASP.NET Core pipeline
        app
            .UseCors(configureCors)
            .UseAuthentication()
            .UseGiraffe (Routing.webApp deps)


    let configureJwtServices (config : ApplicationConfig) (svcs : IServiceCollection) =
        let issuer = $"https://{config.Auth0Config.Domain}/"
        let validationParams =  new TokenValidationParameters()
        validationParams.ValidateIssuer <- true
        validationParams.ValidateIssuerSigningKey <- true
        validationParams.ValidateAudience <- true
        validationParams.ValidAudience <- config.Auth0Config.Audience
        validationParams.ValidIssuer <- issuer
        validationParams.IssuerSigningKey <- new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config.Auth0Config.ClientId))
        validationParams.ValidAlgorithms <- [| "RS256" |]

        svcs
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, fun options ->
                options.Authority <- issuer
                options.TokenValidationParameters <- validationParams
#if DEBUG
                options.RequireHttpsMetadata <- false
#endif
            )

    let configureServices (config : ApplicationConfig) (services : IServiceCollection) =
        // Add Giraffe dependencies
        services
            .AddCors()
            .AddGiraffe()
            |> configureJwtServices config
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
