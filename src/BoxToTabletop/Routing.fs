module BoxToTabletop.Routing

open System
open System.Diagnostics
open BoxToTabletop.Domain
open Domain.Types
open System.Threading
open System.Threading.Tasks
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

open Repository

//type CreateConn = unit -> System.Data.IDbConnection
//type Conn = System.Data.IDbConnection
type Loader<'a> = CreateConn -> Guid ->Task<'a option>
type LoadAll<'a> = CreateConn -> Task<'a list>
type Saver<'a> = CreateConn -> 'a -> Task<int>
type Updater<'a> = CreateConn -> 'a -> Task<int>
type Deleter = CreateConn -> Guid -> Task<int>
type Dependencies = {
    createConnection : CreateConn
    //props : Sql.SqlProps
    loadAllUnits : CreateConn -> Guid -> Task<Unit list>
    saveUnit : Saver<DbTypes.Unit> // Conn -> DbTypes.Unit -> Task<Result<unit, string>>
    updateUnit : Updater<DbTypes.Unit> //Conn -> DbTypes.Unit -> Task<int>
    deleteUnit : CreateConn -> Guid -> Guid -> Task<int>
    loadAllProjects : LoadAll<Domain.Types.Project>
    loadProject : Loader<Domain.Types.Project>
    saveProject : Saver<DbTypes.Project>
    updateProject : Updater<DbTypes.Project>
    updatePriority : CreateConn -> Guid -> Guid -> int -> Task<int>
    //updatePriorities : CreateConn ->  Guid -> Guid -> int -> Task<int>
        //(int * Guid) list -> Task<int>
    //Updater<(int * Guid) list>
}

module Units =
    open FSharp.Control.Tasks.V2

    let listUnits (conn : CreateConn) (loader: CreateConn -> Guid -> Task<Unit list>) (projId : Guid) next ctx = task {
        !! "Getting all units for project {projectId}"
        >>!- ("projectId" , projId)
        |> logger.trace
        let! units = loader conn projId
        let encoded = units |> List.map Types.Unit.Encoder
        return! Successful.OK encoded next ctx
    }

    let saveUnit (conn : CreateConn) saver projId next (ctx : HttpContext) = task {
        let! unitToSave = ctx.BindJsonAsync<Domain.Types.Unit>()
        let unitToSave = { unitToSave with ProjectId = projId }
        let! rowsAffected = saver conn (DbTypes.Unit.FromDomainType unitToSave)
        if (rowsAffected = 1) then
            let encoded = Domain.Types.Unit.Encoder unitToSave
            return! Successful.CREATED encoded next ctx
        else
            !! "Error saving unit after deserializing: incorrect number rows inserted: {rows}"
            >>!- ("rows", rowsAffected)
            |> logger.error
            return! setStatusCode 500 next ctx
    }

    let updateUnit (conn : CreateConn) (updater : CreateConn -> DbTypes.Unit -> Task<int>) (projectId : Guid) (unitId: Guid) (next : HttpFunc) (ctx : HttpContext) = task {
        let! unitToSave = ctx.BindJsonAsync<Domain.Types.Unit>()
        let unitToSave = { unitToSave with ProjectId = projectId ; Id = unitId }
        !! "Unit to save is {unit} after setting project id to {p} and id to {i}"
        >>!+ ("unit", unitToSave) >>!+ ("p", projectId) >>!+ ("i", unitId)
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

    let deleteUnit (conn : CreateConn) (deleter : CreateConn  -> Guid -> Guid -> Task<int>) projId idToDelete next ctx = task {
        let! res = deleter conn projId idToDelete
        if res = 1 then
            return! Successful.NO_CONTENT next ctx
        else
            !! "Incorrect # of rows deleted, expected 1 but got {rows}"
            >>!- ("rows", res)
            |> logger.error
            return! ServerErrors.INTERNAL_ERROR "Error deleting unit" next ctx
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
        //"Project was not found"
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

let webApp (deps : Dependencies) =
    choose [
        GET >=> routeCif (Routes.Project.UnitRoutes.GETALL()) (fun projId -> Units.listUnits deps.createConnection deps.loadAllUnits projId)
        POST >=> routeCif (Routes.Project.UnitRoutes.POST())  (fun projId -> Units.saveUnit deps.createConnection deps.saveUnit projId)
        PUT >=> routeCif (Routes.Project.UnitRoutes.PUT()) (fun (projId, unitId) -> Units.updateUnit deps.createConnection deps.updateUnit projId unitId)
        DELETE >=> routeCif (Routes.Project.UnitRoutes.DELETE()) (fun (projId, unitId) -> Units.deleteUnit deps.createConnection deps.deleteUnit projId unitId)
        GET >=> routeCi Routes.Project.GETALL >=> Projects.listAllProjects deps.createConnection deps.loadAllProjects
        GET >=> routeCif (Routes.Project.GET()) (fun projId -> Projects.loadProject deps.createConnection deps.loadProject projId)
        PUT >=> routeCif (Routes.Project.PUT()) (fun projId -> Projects.updateProject deps.createConnection deps.loadProject deps.saveProject deps.updateProject projId)
        //PUT >=> routeCi (Routes.Project.PUT2) (fun projId -> Projects.updateProject deps.createConnection deps.loadProject deps.saveProject deps.updateProject projId)
        PUT >=> routeCif (Routes.Project.Priorities.PUT()) (fun projId -> Projects.updateUnitPriorities deps.createConnection deps.updatePriority projId)
        route "/" >=> GET >=> htmlFile "index.html"
    ]
