module BoxToTabletop.Routing

open System
open BoxToTabletop.Domain
open Domain.Types
open System.Threading
open System.Threading.Tasks
open FSharp.Control.Tasks.Affine
//open FsToolkit.ErrorHandling
open System.Text
open Giraffe
open Microsoft.Extensions.Primitives
open Microsoft.AspNetCore.Http
open Microsoft.Net.Http.Headers

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



type CreateConn = unit -> System.Data.IDbConnection
type Conn = System.Data.IDbConnection
type Loader<'a> = Conn -> Guid ->Task<'a option>
type LoadAll<'a> = Conn -> Task<'a list>
type Saver<'a> = Conn -> 'a -> Task<int>
type Updater<'a> = Conn -> 'a -> Task<int>
type Dependencies = {
    createConnection : CreateConn
    loadAllUnits : Conn -> Guid -> Task<Unit list>
    saveUnit : Conn -> DbTypes.Unit -> Task<Result<unit, string>>
    updateUnit : Conn -> DbTypes.Unit -> Task<int>
    deleteUnit : Conn -> Guid -> Guid -> Task<int>
    loadAllProjects : LoadAll<Domain.Types.Project>
    //Conn -> Task<Project list>
    loadProject : Loader<Domain.Types.Project>
    //Conn -> Guid -> Task<Project option>
    saveProject : Saver<DbTypes.Project>
    //Conn -> DbTypes.Project -> Task<int>
    updateProject : Updater<DbTypes.Project>
    //17Conn -> DbTypes.Project -> Task<int>
}

module Units =

    let listUnits (createConn : CreateConn) (loader: Conn -> Guid -> Task<Unit list>) (projId : Guid) next ctx = task {
        !! "Getting all units for project {projectId}"
        >>!- ("projectId" , projId)
        |> logger.trace
        let conn = createConn()
        let! units = loader conn projId
        let encoded = units |> List.map Types.Unit.Encoder
        return! Successful.OK encoded next ctx
    }

    let saveUnit (createConn : CreateConn) (saver : Conn -> DbTypes.Unit -> Task<Result<unit, string>>) projId next (ctx : HttpContext) = task {
        let conn = createConn()
        let! unitToSave = ctx.BindJsonAsync<Domain.Types.Unit>()
        let unitToSave = { unitToSave with ProjectId = projId }
        let! rowsAffected = saver conn (DbTypes.Unit.FromDomainType unitToSave)
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

    let updateUnit (createConn : CreateConn) (updater : Conn -> DbTypes.Unit -> Task<int>) (projectId : Guid) (unitId: Guid) (next : HttpFunc) (ctx : HttpContext) = task {
        let conn = createConn()
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

    let deleteUnit (createConn : CreateConn) (deleter : Conn -> Guid -> Guid -> Task<int>) projId idToDelete next ctx = task {
        let conn = createConn()
        let! res = deleter conn projId idToDelete
        if res = 1 then
            return! Successful.NO_CONTENT next ctx
        else
            return! ServerErrors.INTERNAL_ERROR "Error deleting unit" next ctx
    }

module Projects =
    open FSharp.Control.Tasks.V2

    let listAllProjects (createConn : CreateConn) loader next ctx = task {
        let conn = createConn()
        let! projects = loader conn
        let encoded = projects |> List.map Domain.Types.Project.Encoder
        return! Successful.OK encoded next ctx
    }

    let loadProject (createConn : CreateConn) loader projId next ctx = task {
        let conn = createConn()
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

    let updateProject (createConn : CreateConn) loader saver updater projId next (ctx : HttpContext) = task {
        let conn = createConn()
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
        route "/" >=> GET >=> htmlFile "index.html"
    ]
