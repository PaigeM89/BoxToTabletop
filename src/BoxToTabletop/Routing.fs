module BoxToTabletop.Routing

open System
open System.Diagnostics
open BoxToTabletop.Domain
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

open BoxToTabletop.Repository

//type CreateConn = unit -> System.Data.IDbConnection
//type Conn = System.Data.IDbConnection
type Loader<'a> = CreateConn -> Guid ->Task<'a option>
type LoadAll<'a> = CreateConn -> Task<'a list>
type Saver<'a> = CreateConn -> 'a -> Task<int>
type Updater<'a> = CreateConn -> 'a -> Task<int>
type Deleter = CreateConn -> Guid -> Task<int>
type Dependencies = {
    createConnection : CreateConn
    loadAllUnits : CreateConn -> Guid -> Task<Unit list>
    loadUnit : Loader<Unit>
    saveUnit : Saver<DbTypes.Unit>
    updateUnit : Updater<DbTypes.Unit>
    deleteUnit : CreateConn -> Guid -> Task<int>
    loadAllProjects : LoadAll<Domain.Types.Project>
    loadProject : Loader<Domain.Types.Project>
    saveProject : Saver<DbTypes.Project>
    updateProject : Updater<DbTypes.Project>
    updatePriority : CreateConn -> Guid -> Guid -> int -> Task<int>
}

module Units =
    open FSharp.Control.Tasks.V2

    let listUnits (conn : CreateConn) (loader: CreateConn -> Guid -> Task<Unit list>) next (ctx : HttpContext) = task {
        let projId = ctx.Request.Query.TryGetValue("projectId")
        match projId with
        | true, projId when projId.Count = 1 ->
            let projId = projId.Item 0 |> Guid.Parse
            !! "Getting all units for project {projectId}"
            >>!- ("projectId" , projId)
            |> logger.trace
            let! units = loader conn projId
            let encoded = units |> List.map Types.Unit.Encoder
            return! Successful.OK encoded next ctx
        | true, _ ->
            !! "Requesting unit lists for multiple projects: {projId}"
            >>!+ ("projId", projId)
            |> logger.warn
            return! RequestErrors.BAD_REQUEST "Can only request units for a single project." next ctx
        | false, _ ->
            return! RequestErrors.BAD_REQUEST "No Project Id found" next ctx
    }

    let saveUnit (conn : CreateConn) saver next (ctx : HttpContext) = task {
        let! unitToSave = ctx.BindJsonAsync<Domain.Types.Unit>()
        let! rowsAffected = saver conn (DbTypes.Unit.FromDomainType unitToSave)
        if (rowsAffected = 1) then
            let encoded = Domain.Types.Unit.Encoder unitToSave
            return! Successful.CREATED encoded next ctx
        else
            !! "Error saving unit after deserializing: incorrect number rows inserted: {rows}"
            >>!- ("rows", rowsAffected)
            |> logger.error
            return! ServerErrors.INTERNAL_ERROR "Error saving unit" next ctx
    }

    let updateUnit (conn : CreateConn) (updater : CreateConn -> DbTypes.Unit -> Task<int>) (unitId: Guid) (next : HttpFunc) (ctx : HttpContext) = task {
        let! unitToSave = ctx.BindJsonAsync<Domain.Types.Unit>()
        let unitToSave = { unitToSave with Id = unitId }
        !! "Unit to save is {unit} after setting id to {i}"
        >>!+ ("unit", unitToSave) >>!+ ("i", unitId)
        |> logger.info
        let! rowsAffected = updater conn (DbTypes.Unit.FromDomainType unitToSave)
        if rowsAffected >= 1 then
            !! "Updated {count} rows when saving unit {unit}"
            >>!- ("count", rowsAffected) >>!+ ("unit", unitToSave)
            |> logger.trace
            let encoded = Domain.Types.Unit.Encoder unitToSave
            return! Successful.OK encoded next ctx
        else
            !! "Did not update any records when updating unit {unit}"
            >>!- ("unit", unitToSave)
            |> logger.warn
            // todo: do a save here, to comply with PUT
            return! ServerErrors.INTERNAL_ERROR ($"Unable to update unit {unitToSave.Name}") next ctx
    }

    let deleteUnit (conn : CreateConn) (deleter : CreateConn  -> Guid -> Task<int>) idToDelete next ctx = task {
        let! res = deleter conn idToDelete
        if res = 1 then
            return! Successful.NO_CONTENT next ctx
        else
            !! "Incorrect # of rows deleted, expected 1 but got {rows}"
            >>!- ("rows", res)
            |> logger.error
            return! ServerErrors.INTERNAL_ERROR "Error deleting unit" next ctx
    }

    let transferUnit (conn : CreateConn) (loader: Loader<Unit>) (updater : Updater<DbTypes.Unit>) unitId (next : HttpFunc) (ctx: HttpContext) = task {
        let! newProjectId = ctx.BindJsonAsync<Guid>()
        !! "Transferring unit with Id '{unitId}'  to project '{newProjectId}'"
        >>!+ ("unitId", unitId)
        >>!+ ("newProjectId", newProjectId)
        |> logger.info
        let! unit = loader conn unitId
        match unit with
        | Some unit ->
            let unitToSave = { unit with ProjectId = newProjectId }
            let! rowsAffected = unitToSave |> DbTypes.Unit.FromDomainType |> updater conn
            if rowsAffected >= 1 then
                !! "Updated {count} rows when saving unit {unit}"
                >>!- ("count", rowsAffected)
                >>!+ ("unit", unitToSave)
                |> logger.trace
                let encoded = unitToSave |> Domain.Types.Unit.Encoder
                return! Successful.OK encoded next ctx
            else
                !! "Did not update any records when updating unit {unit}"
                >>!+ ("unit", unitToSave)
                |> logger.warn
                return! ServerErrors.INTERNAL_ERROR ($"Unable to update unit {unitToSave.Name}") next ctx
        | None ->
            !! "Did not find unit with id '{unitId}' to transfer to project '{newProjectId}'"
            >>!+ ("unitId", unitId)
            >>!+ ("newProjectId", newProjectId)
            |> logger.info
            return! RequestErrors.NOT_FOUND unitId next ctx
    }

module Projects =
    open FSharp.Control.Tasks.V2


    let listAllProjects (conn : CreateConn) loader next ctx = task {
        let! projects = loader conn
        let encoded = projects |> List.map Domain.Types.Project.Encoder
        return! Successful.OK encoded next ctx
    }

    let loadProject (conn : CreateConn) loader projId next ctx = task {
        let! projectOpt = loader conn projId
        match projectOpt with
        | Some p ->
            let encoded = Domain.Types.Project.Encoder p
            return! Successful.OK encoded next ctx
        | None ->
            !! "project was not found for id {id}" >>!+ ("id", projId) |> logger.info
            return! Successful.NO_CONTENT next ctx
    }

    let updateProject (conn : CreateConn) loader saver updater projId next (ctx : HttpContext) = task {
        let! projectToSave = ctx.BindJsonAsync<Domain.Types.Project>()
        let encoded = Domain.Types.Project.Encoder projectToSave
        let decoded = DbTypes.Project.FromDomainType projectToSave
        let! existing = loader conn projId
        match existing with
        | Some p ->
            !! "Project {proj} is being updated to {newproj}"
            >>!+ ("proj", p)
            >>!+ ("newproj", projectToSave)
            |> logger.info
            let! _ = updater conn decoded
            let encoded = Domain.Types.Project.Encoder projectToSave
            return! Successful.OK encoded next ctx
        | None ->
            !! "Project {proj} is being saved as new in the PUT endpoint"
            >>!+ ("proj", projectToSave)
            |> logger.info
            let! _ = saver conn decoded
            return! Successful.CREATED encoded next ctx
    }

    let updateUnitPriorities (conn : CreateConn) (updater) (projectId : Guid) next (ctx : HttpContext) = task {
        let sw = Stopwatch.StartNew()
        let! s = ctx.ReadBodyFromRequestAsync()
        let decoded =
            match Thoth.Json.Net.Decode.fromString Types.UnitPriority.DecodeList s with
            | Ok x ->
                !! "Decoded {value} from htpt body" >>!+ ("value", x) |> logger.info
                x
            | Error e ->
                !! "Decode error {e} from input {input}" >>!+ ("e", e) >>!+ ("input", s) |> logger.error
                []
        let updateTasks : Task<int> list = decoded |> List.map (fun up -> updater conn projectId up.UnitId up.UnitPriority)
        let! updatedRows = Task.WhenAll updateTasks
        let rowsAffected = Array.sum updatedRows
        let expected = List.length decoded
        sw.Stop()
        let log = !! "Took {time} ms to update all unit priorities" >>!+ ("time", sw.ElapsedMilliseconds)
        if sw.ElapsedMilliseconds > 500L then logger.warn log else logger.info log
        !! "updated priorites on proj {proj} to {updates}" >>!+("proj", projectId) >>!+ ("updates", decoded) |> logger.info
        if rowsAffected <> expected then
            !! "Expected to update {exp} rows, but updated {count} instead"
            >>!- ("exp", expected) >>!+ ("count", rowsAffected)
            |> logger.error
            let msg = $"Updated {rowsAffected} items when given {expected} items to update"
            return! ServerErrors.INTERNAL_ERROR msg next ctx
        else
            let encoded = Domain.Types.UnitPriority.EncodeList decoded
            !! "Successfully updated priorties, returning {x}" >>!+ ("x", encoded) |> logger.info
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
        "no idea what Realm is"
        "You must be logged in"

let authenticated = requiresAuthentication failedAuthHandler

let webApp (deps : Dependencies) =
    choose [
        authenticated >=> choose [
            POST >=> routeCif (Routes.UnitRoutes.Transfer.POST()) (fun unitId -> Units.transferUnit deps.createConnection deps.loadUnit deps.updateUnit unitId)
            GET >=> routeCi Routes.UnitRoutes.Root >=> Units.listUnits deps.createConnection deps.loadAllUnits
            POST >=> routeCi (Routes.UnitRoutes.Root) >=> (Units.saveUnit deps.createConnection deps.saveUnit)
            PUT >=> routeCif (Routes.UnitRoutes.PUT()) (fun unitId -> Units.updateUnit deps.createConnection deps.updateUnit unitId)
            DELETE >=> routeCif (Routes.UnitRoutes.DELETE()) (fun unitId -> Units.deleteUnit deps.createConnection deps.deleteUnit unitId)
            GET >=> routeCi Routes.ProjectRoutes.GETALL >=> Projects.listAllProjects deps.createConnection deps.loadAllProjects
            GET >=> routeCif (Routes.ProjectRoutes.GET()) (fun projId -> Projects.loadProject deps.createConnection deps.loadProject projId)
            PUT >=> routeCif (Routes.ProjectRoutes.PUT()) (fun projId -> Projects.updateProject deps.createConnection deps.loadProject deps.saveProject deps.updateProject projId)
            PUT >=> routeCif (Routes.ProjectRoutes.Priorities.PUT()) (fun projId -> Projects.updateUnitPriorities deps.createConnection deps.updatePriority projId)
        ]
        route "/" >=> GET >=> htmlFile "index.html"
    ]
