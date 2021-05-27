namespace BoxToTabletop.Client

open System.Data
open BoxToTabletop.Domain
open BoxToTabletop.Domain.Types
open System
open Fable.SimpleHttp

module Core =

    type Updates =
    /// A change in the visible columns, which needs to be propagated to multiple components.
    | ColumnSettingsChange of ColumnSettings

module Config =
    type T = {
        ServerUrl : string
    } with
        static member Default() = {
            ServerUrl =  "http://localhost:5000"
        }

    let withServerUrl (serverUrl : string) (t : T) =
        { t with ServerUrl = serverUrl }

module Promises =
    open Fetch
    open Thoth.Fetch
    open Fable.Core.JS
    open BoxToTabletop.Domain.Routes
    open Routes
    open Core

    let printFetchError (fe : FetchError) =
        match fe with
        | PreparingRequestFailed exn ->
            printfn "Error preparing request: %A" exn
            "Error creating request"
        | DecodingFailed s ->
            printfn "Unable to decode %s to object" s
            "Error reading response"
        | FetchFailed response ->
            printfn "Error fetching, response is %A" response
            sprintf "Received code %i from server" response.Status
        | NetworkError exn ->
            printfn "Network error: %A" exn
            "Error reaching server"

    let buildStaticRoute (config : Config.T) route =
        Routes.combine config.ServerUrl route

    let buildRouteSimple (config : Config.T) route = Routes.combine config.ServerUrl route

    let buildRoute (config : Config.T) (routeEval) =
        fun x -> Routes.combine config.ServerUrl (sprintf routeEval x)

    let buildRoute2 (config : Config.T) routeEval =
        fun (x, y) -> Routes.combine config.ServerUrl (sprintf routeEval x y)

    let addQueryParam qparam qvalue route = route + (sprintf "?%s=%s" qparam qvalue)

    let createUnit (config : Config.T) (unit : Types.Unit) = promise {
        let url = UnitRoutes.Root |> buildRouteSimple config
        let data = Types.Unit.Encoder unit
        let _decoder = Types.Unit.Decoder
        let headers = [
            HttpRequestHeaders.Origin "*"
        ]
        return! Fetch.tryPost(url, data, decoder = _decoder, headers = headers)
    }

    let updateUnit (config : Config.T) (unit : Types.Unit) = promise {
        let url = UnitRoutes.PUT() |> buildRoute config <| unit.Id
        let data = Types.Unit.Encoder unit
        let headers = [
            HttpRequestHeaders.Origin "*"
        ]
        let decoder = Types.Unit.Decoder
        return! Fetch.tryPut(url, data, decoder= decoder, headers = headers)
    }

    let loadUnitsForProject (config : Config.T) (projectId : Guid) = async {
        let url = UnitRoutes.Root |> buildRouteSimple config |> addQueryParam "projectId" (string projectId)
        let! response =
            Http.request url
            |> Http.method GET
            |> Http.header (Headers.accept "application/json")
            |> Http.send

        if response.statusCode = 200 then
            let decoder : Thoth.Json.Decoder<Types.Unit list> = Types.Unit.DecodeMany
            let body = response.responseText
            let decoded = Thoth.Json.Decode.fromString decoder body
            return decoded
        elif response.statusCode = 204 then
            return Ok []
        else
            return Error (sprintf "Get All Units returned code %i" response.statusCode)
    }

    let deleteUnit (config : Config.T) (projectId : Guid) (unitId : Guid) : Promise<unit> = promise {
        let url = UnitRoutes.DELETE() |> buildRoute config <| unitId
        return! Fetch.delete(url)
    }

    let transferUnit (config : Config.T) unitId newProjectId = async {
        let url = UnitRoutes.Transfer.POST() |> buildRoute config <| unitId
        let payload = Thoth.Json.Encode.guid newProjectId |> Thoth.Json.Encode.toString 0
        let! response =
            Http.request url
            |> Http.method POST
            |> Http.content (BodyContent.Text payload)
            |> Http.send

        printfn "Sending unit transfer request to url %A" url

        if response.statusCode = 200 then
            return Ok unitId
        else
            return Error response.statusCode
    }

    let loadAllProjects (config : Config.T) : Promise<Project list> = promise {
        let url = ProjectRoutes.GETALL |> buildStaticRoute config
        let decoder = Types.Project.DecodeMany
        return! Fetch.get(url, decoder = decoder)
    }

    let loadProject (config : Config.T) (id : Guid) = promise {
        let url = ProjectRoutes.GET() |> buildRoute config <| id
        let decoder = Types.Project.Decoder
        return! Fetch.tryGet(url, decoder = decoder)
    }

    let updateProject (config : Config.T) (project : Project) : Promise<Project> = promise {
        let url = ProjectRoutes.PUT() |> buildRoute config <| project.Id
        printfn "url to update project is %A" url
        let decoder = Types.Project.Decoder
        return! Fetch.put(url, project, decoder = decoder)
    }

    let updateUnitPriorities (config : Config.T) (projId : Guid) (updates : UnitPriority list) : Promise<Result<UnitPriority list, FetchError>> = promise {
        let url = ProjectRoutes.Priorities.PUT() |> buildRoute config <| projId
        printfn "sending updates to %A: %A" url updates
        let decoder = Types.UnitPriority.DecodeList
        let payload = UnitPriority.EncodeList updates
        return! Fetch.tryPut(url, payload, decoder = decoder)
    }

    let updateUnitPriorities2 (config : Config.T) (projId : Guid) (updates : UnitPriority list) = async {
        let url = ProjectRoutes.Priorities.PUT() |> buildRoute config <| projId
        let encoded = UnitPriority.EncodeList updates
        let payload = Thoth.Json.Encode.toString 0 encoded
        let! response =
            Http.request url
            |> Http.method PUT
            |> Http.content (BodyContent.Text payload)
            |> Http.header (Headers.contentType "application/json")
            |> Http.send

        printfn "Status: %d" response.statusCode
        printfn "Content: %s" response.responseText
        if response.statusCode >= 200 && response.statusCode < 300 then
            return Ok ()
        else
            return Error response.statusCode
    }

