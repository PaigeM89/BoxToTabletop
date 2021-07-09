namespace BoxToTabletop


module Routing =

    open System
    open System.Diagnostics
    open BoxToTabletop.Domain
    open BoxToTabletop.Repository
    open Domain.Types
    open System.Threading
    open System.Threading.Tasks
    open FsToolkit.ErrorHandling
    open FSharp.Control.Tasks.Affine
    open System.Text
    open Giraffe
    open Microsoft.Extensions.Primitives
    open Microsoft.AspNetCore.Http
    open Microsoft.Net.Http.Headers
    open Npgsql.FSharp

    open BoxToTabletop.Logging
    open BoxToTabletop.LogHelpers.Operators

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

    let createUnauthorized realm msg =
        RequestErrors.UNAUTHORIZED
            Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme
            realm
            msg

    let getToken (ctx : HttpContext) =
        match ctx.GetRequestHeader("Authorization") with
        | Ok value -> value.Replace("Bearer ", "") 
        | Error e ->
            printfn "Error getting authorization header: %A" e
            ""

    let getUserId (ctx : HttpContext) = getToken ctx |> Jwt.readToken |> Jwt.getUserId

    module Units =

        let listUnits next (ctx : HttpContext) = task {
            match getUserId ctx with
            | Some userId ->
                let unitDomain = ctx.GetService<Domain.Unit.IUnitDomain>()
                let projId =
                    ctx.Request.Query.TryGetValue("projectId")
                    |> snd
                match Guid.TryParse (projId.Item 0) with
                | true, projectId ->
                    let! units =
                        unitDomain.GetAllUnitsForProject userId projectId
                    let encoded = units |> List.map Types.Unit.Encoder
                    return! Successful.OK encoded next ctx
                | false, _ ->
                    return! RequestErrors.BAD_REQUEST "No parsable project Id found" next ctx
            | None ->
                return! createUnauthorized "Load Units For Project" "User information was not found to load units for a project" next ctx
        }

        let saveNewUnit next (ctx : HttpContext) = task {
            match getUserId ctx with
            | Some userId ->
                let unitDomain = ctx.GetService<Domain.Unit.IUnitDomain>()
                let! unit = ctx.BindJsonAsync<Domain.Types.Unit>()
                match! unitDomain.SaveNewUnit userId unit with
                | Some (rowCount, unit) when rowCount = 1 ->
                    let encoded = Domain.Types.Unit.Encoder unit
                    return! Successful.CREATED encoded next ctx
                | Some (rowCount, unit) ->
                        !! "Error saving unit after deserializing: incorrect number rows inserted: {rows}"
                        >>!- ("rows", rowCount)
                        |> logger.error
                        return! ServerErrors.INTERNAL_ERROR "Error saving unit" next ctx
                | None ->
                    return! RequestErrors.BAD_REQUEST "Unit already exists" next ctx
            | None ->
                return! createUnauthorized "Save New Unit" "No user information found to save new unit" next ctx
        }

        let updateUnit (unitId : Guid) next (ctx : HttpContext) = task {
            match getUserId ctx with
            | Some userId ->
                let! unit = ctx.BindJsonAsync<Domain.Types.Unit>()
                let unitDomain = ctx.GetService<Domain.Unit.IUnitDomain>()
                match! unitDomain.UpdateUnit userId unit with
                | Some (wasNew, unit) when wasNew ->
                    let encoded = unit |> Domain.Types.Unit.Encoder
                    return! Successful.CREATED encoded next ctx
                | Some (_, unit) ->
                    let encoded = unit |> Domain.Types.Unit.Encoder
                    return! Successful.OK encoded next ctx
                | None ->
                    return! RequestErrors.NOT_FOUND $"Unit {unitId} not found to update" next ctx
            | None ->
                return! createUnauthorized "Update Unit" "No user information found to update unit" next ctx
        }

        let updateManyUnits next (ctx : HttpContext) = task {
            match getUserId ctx with
            | Some userId ->
                let! body = ctx.ReadBodyFromRequestAsync()
                let units = Thoth.Json.Net.Decode.fromString Types.Unit.DecodeMany body
                match units with
                | Ok units ->
                    let unitDomain = ctx.GetService<Domain.Unit.IUnitDomain>()
                    let! updated = unitDomain.UpdateUnits userId units
                    if (List.length updated) <> List.length units then
                        !! "User {userId} attempted to update {total} units but only owned {count}."
                        >>!+ ("userId", userId) >>!- ("total", (List.length units))
                        >>!- ("count", (List.length updated))
                        |> logger.warn
                    let updatedIds = updated |> List.map (fun (_, u) -> u.Id)
                    return! Successful.OK updatedIds next ctx
                | Error e ->
                    !! "Error decoding units: {err}" >>!+ ("err", e) |> logger.error
                    return! ServerErrors.INTERNAL_ERROR "Error updating units" next ctx
            | None ->
                return! createUnauthorized "Update units" "Unable to find user information to update units" next ctx
        }

        let deleteUnit idToDelete next (ctx : HttpContext) = task {
            let deleter = ctx.GetService<IModifyUnits>()
            let! res = deleter.Delete idToDelete
            if res = 1 then
                return! Successful.NO_CONTENT next ctx
            else
                !! "Incorrect # of rows deleted, expected 1 but got {rows}"
                >>!- ("rows", res)
                |> logger.error
                return! ServerErrors.INTERNAL_ERROR "Error deleting unit" next ctx
        }

        let transferUnit unitId next (ctx : HttpContext) = task {
            match getUserId ctx with
            | Some userId ->
                let unitDomain = ctx.GetService<Domain.Unit.IUnitDomain>()
                let! newProjectId = ctx.BindJsonAsync<Guid>()
                match! unitDomain.LoadUnit userId unitId with
                | Some unit ->
                    let! updatedUnit = unitDomain.TransferUnit userId unit newProjectId
                    let encoded = updatedUnit |> Domain.Types.Unit.Encoder
                    return! Successful.OK encoded next ctx
                | None ->
                    return! RequestErrors.NOT_FOUND unitId next ctx
            | None ->
                return! createUnauthorized "Transfer Unit" "No user information found to transfer unit" next ctx
        }


    module Projects =
        open Microsoft.IdentityModel.Tokens
        open Microsoft.AspNetCore.Authentication.JwtBearer

        let listAllProjects next (ctx : HttpContext) = task {
            match getUserId ctx with
            | Some userId ->
                let projectDomain = ctx.GetService<Domain.Project.IProjectDomain>()
                let! projects = projectDomain.GetAllProjectsForUser userId
                let encoded = projects |> List.map Domain.Types.Project.Encoder
                return! Successful.OK encoded next ctx
            | None -> return! createUnauthorized "Load Projects For user" "User information was not found to load projects" next ctx
        }

        let loadProject projId next (ctx : HttpContext) = task {
            match getUserId ctx with
            | Some userId ->
                let projectDomain = ctx.GetService<Domain.Project.IProjectDomain>()
                match! projectDomain.LoadProject userId projId with
                | Some proj ->
                    let encoded = Domain.Types.Project.Encoder proj
                    return! Successful.OK proj next ctx
                | None ->
                    return! Successful.NO_CONTENT next ctx
            | None ->
                return! createUnauthorized "Load project" "User information was not found to load project" next ctx
        }

        let loadProjectColumns projectId next (ctx : HttpContext) = task {
            match getUserId ctx with
            | Some userId ->
                let projectDomain = ctx.GetService<Domain.Project.IProjectDomain>()
                match! projectDomain.LoadProject userId projectId with
                | Some proj ->
                    let encoded = proj.EncodeColumns()
                    return! Successful.OK encoded next ctx
                | None ->
                    return! Successful.NO_CONTENT next ctx
            | None ->
                return! createUnauthorized "Load project columns" "User information was not found to load project columns" next ctx
        }

        let updateProjectColumn (projectId : Guid, columnId : Guid) next (ctx : HttpContext) = task {
            match getUserId ctx with
            | Some userId ->
                let projectDomain = ctx.GetService<Project.IProjectDomain>()
                let! column = ctx.BindJsonAsync<Types.ProjectColumn>()
                let column = { column with ProjectId = projectId; ColumnId = columnId }
                match! projectDomain.SaveProjectColumn column with
                | Some _ -> return! Successful.OK column next ctx
                | None ->
                    !! "Error saving project column. Impossible state reached."
                    |> logger.error
                    return! ServerErrors.INTERNAL_ERROR "Error saving column" next ctx
            | None  ->
                return! createUnauthorized "Update Project Column" "User information was not found to update project column" next ctx
        }

        let deleteProject projectId next (ctx : HttpContext) = task {
            let deleter = ctx.GetService<IModifyProjects>()
            do! deleter.Delete projectId
            return! Successful.NO_CONTENT next ctx
        }

        let saveProject next ctx = task {
            match getUserId ctx with
            | Some userId ->
                let projectDomain = ctx.GetService<Domain.Project.IProjectDomain>()
                let! project = ctx.BindJsonAsync<Domain.Types.Project>()
                match! projectDomain.SaveNewProject userId project with
                | Some (count, saved) ->
                    let encoded = saved |> Domain.Types.Project.Encoder
                    return! Successful.CREATED encoded next ctx
                | None ->
                    return! RequestErrors.CONFLICT "Project already exists" next ctx
            | None ->
                return! createUnauthorized "Create Project" "Unable to find user Id to create new project" next ctx
        }

        let updateProject projId next (ctx : HttpContext) = task {
            match getUserId ctx with
            | Some userId ->
                let! projectToSave = ctx.BindJsonAsync<Domain.Types.Project>()
                if projectToSave.Id = projId then
                    return! RequestErrors.BAD_REQUEST "Project Id in URL does not match project Id in payload" next ctx
                else
                    let projectDomain = ctx.GetService<Domain.Project.IProjectDomain>()
                    let! maybeSave = projectDomain.UpdateProject userId projectToSave
                    match maybeSave with
                    | Some (wasNew, project) when wasNew ->
                        let encoded = Project.Encoder project
                        return! Successful.CREATED encoded next ctx
                    | Some (wasNew, project) ->
                        let encoded = Project.Encoder project
                        return! Successful.OK encoded next ctx
                    | None ->
                        return! ServerErrors.INTERNAL_ERROR "Unable to update project" next ctx
            | None ->
                return! createUnauthorized "Update Project" "User does not have access to this project" next ctx
        }

    let getAuth0Config next (ctx : HttpContext) = task {
        let config = ctx.GetService<Configuration.ApplicationConfig>()
        let asJsonObject = Auth0ConfigJson.Create config.Auth0Config.Domain config.Auth0Config.ClientId config.Auth0Config.Audience
        let encoded = asJsonObject.Encode()
        return! Successful.OK encoded next ctx
    }

    let parsingErrorHandler (err : string) next ctx =
        !! "Error parsing json from request. Error: {err}"
        >>!- ("err", err)
        |> logger.error
        RequestErrors.badRequest (json "Unable to deserialize json") next ctx

    let failedAuthHandler =
        RequestErrors.UNAUTHORIZED
            Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme
            "Box To Tabletop"
            "You must be logged in"

    let authenticated = requiresAuthentication failedAuthHandler

    let webApp() =
        choose [
            routeCi Routes.Auth0Config >=> GET >=> getAuth0Config
            routeStartsWithCi Routes.Root >=> authenticated >=> choose [
                // units
                POST >=> routeCif (Routes.UnitRoutes.Transfer.POST()) Units.transferUnit
                GET >=> routeCi Routes.UnitRoutes.Root >=> Units.listUnits
                POST >=> routeCi (Routes.UnitRoutes.Root) >=> Units.saveNewUnit
                PUT >=> routeCif (Routes.UnitRoutes.PUT()) Units.updateUnit
                PUT >=> routeCi (Routes.UnitRoutes.PUTCollection) >=> Units.updateManyUnits
                DELETE >=> routeCif (Routes.UnitRoutes.DELETE()) Units.deleteUnit

                // projects
                GET >=> routeCif (Routes.ProjectRoutes.Columns.GET()) Projects.loadProjectColumns
                PUT >=> routeCif (Routes.ProjectRoutes.Columns.PUT()) Projects.updateProjectColumn
                GET >=> routeCi Routes.ProjectRoutes.Root >=> Projects.listAllProjects 
                POST >=> routeCi Routes.ProjectRoutes.Root >=> Projects.saveProject
                DELETE >=> routeCif (Routes.ProjectRoutes.DELETE()) Projects.deleteProject
                GET >=> routeCif (Routes.ProjectRoutes.GET()) Projects.loadProject
                PUT >=> routeCif (Routes.ProjectRoutes.PUT()) Projects.updateProject
            ]
            route "/" >=> GET >=> htmlFile "index.html"
        ]
