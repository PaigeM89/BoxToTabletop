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

module Handlers =
    open Domain.Types
    open System.Threading
    open System.Threading.Tasks
    open FSharp.Control.Tasks.Affine
    open FsToolkit.ErrorHandling
    open System.Text
    open Giraffe
    open Microsoft.Extensions.Primitives
    open Microsoft.AspNetCore.Http
    open Microsoft.Net.Http.Headers

    let rec logger = LogProvider.getLoggerByQuotation <@ logger @>

    let private runTaskAndCatch (operation: unit -> Task<'t>) = task {
        try
          let! result = operation ()
          return Ok result
        with e -> return Error (sprintf "%s:\n:%s" e.Message e.StackTrace)
    }

    let tryBindModelAsync<'T>
            (parsingErrorHandler : string -> HttpHandler) (successhandler: 'T -> HttpHandler) : HttpHandler =
        fun (next : HttpFunc) ctx -> task {

            let method = ctx.Request.Method

            let! result = task {
                if method.Equals "POST" || method.Equals "PUT" || method.Equals "PATCH" || method.Equals "DELETE" then
                    let original = StringSegment(ctx.Request.ContentType)
                    let parsed   = ref (MediaTypeHeaderValue(StringSegment("*/*")))
                    match MediaTypeHeaderValue.TryParse(original, parsed) with
                    | false -> return Core.Error (sprintf "Could not parse Content-Type HTTP header value '%s'" original.Value)
                    | true  ->
                      match parsed.Value.MediaType.Value with
                      | "application/json"                  -> return! runTaskAndCatch ctx.BindJsonAsync<'T>
                      | "application/xml"                   -> return! runTaskAndCatch ctx.BindXmlAsync<'T>
                      | _ -> return Core.Error (sprintf "Cannot bind model from Content-Type '%s'" original.Value)
                else return ctx.TryBindQueryString<'T>()
            }

            match result with
            | Core.Error msg -> return! parsingErrorHandler msg next ctx
            | Core.Ok model  -> return! successhandler model next ctx
    }

    type CreateConn = unit -> Npgsql.NpgsqlConnection
    type Dependencies = {
        loadAllUnits : unit -> Task<Unit list>
        saveUnit : DbTypes.Unit -> Task<Result<unit, string>>
    }

    let listUnits (loader: unit -> Task<Unit list>) next ctx = task {
        let! units = loader()
        return! json units next ctx
    }

    let saveUnit (saver : DbTypes.Unit -> Task<Result<unit, string>>) unitToSave next ctx = task {
        let! rowsAffected = saver (DbTypes.Unit.FromDomainType Guid.Empty unitToSave)
        match rowsAffected with
        | Ok _ ->
            let encoded = Domain.Types.Unit.Encoder unitToSave
            return! Successful.CREATED encoded next ctx
        | Error e ->
            !! "Error saving unit after deserializing: {err}"
            >>!- ("err", e)
            |> logger.error
            return! setStatusCode 500 next ctx
    }

    let parsingErrorHandler (err : string) next ctx =
        !! "Error parsing json from request. Error: {err}"
        >>!- ("err", err)
        |> logger.error
        RequestErrors.badRequest (json "Unable to deserialize json") next ctx

    let webApp (deps : Dependencies) =
        choose [
            route "/units" >=> GET >=> listUnits deps.loadAllUnits
            route "/units" >=> POST >=> tryBindModelAsync<Domain.Types.Unit> parsingErrorHandler (saveUnit deps.saveUnit)
            route "/" >=> GET >=> htmlFile "index.html"
        ]

module Webhost =

    let configureCors (builder : CorsPolicyBuilder) =
        builder.WithOrigins("http://localhost:8090").AllowAnyMethod().AllowAnyHeader() |> ignore

    let configureApp (deps : Handlers.Dependencies) (app : IApplicationBuilder) =
        // Add Giraffe to the ASP.NET Core pipeline
        app.UseCors configureCors |> ignore
        app.UseGiraffe (Handlers.webApp deps)

    let configureServices (config : ApplicationConfig) (services : IServiceCollection) =
        // Add Giraffe dependencies
        services
            .AddCors()
            .AddGiraffe()
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
