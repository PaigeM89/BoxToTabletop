namespace BoxToTabletop.Client

open System.Data
open BoxToTabletop.Domain
open BoxToTabletop.Domain.Types
open System
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

    let buildRoute (config : Config.T) (routeEval) =
        fun x -> Routes.combine config.ServerUrl (sprintf routeEval x)

    let buildRoute2 (config : Config.T) routeEval =
        fun (x, y) -> Routes.combine config.ServerUrl (sprintf routeEval x y)

    let createUnit (config : Config.T) (unit : Types.Unit) = promise {
        let url = Project.UnitRoutes.POST() |> buildRoute config <| unit.ProjectId
        let data = Types.Unit.Encoder unit
        let _decoder = Types.Unit.Decoder
        let headers = [
            HttpRequestHeaders.Origin "*"
        ]
        return! Fetch.tryPost(url, data, decoder = _decoder, headers = headers)
    }

    let updateUnit (config : Config.T) (unit : Types.Unit) = promise {
        //let url = sprintf "http://localhost:5000/api/v1/units/%O" (unit.Id.ToString("N"))
        let url = Project.UnitRoutes.PUT() |> buildRoute2 config <| (unit.ProjectId, unit.Id)
        let data = Types.Unit.Encoder unit
        let headers = [
            HttpRequestHeaders.Origin "*"
        ]
        let decoder = Types.Unit.Decoder
        return! Fetch.tryPut(url, data, decoder= decoder, headers = headers)
    }

    let loadUnitsForProject (config : Config.T) (projectId : Guid) = promise {
        let url = Project.UnitRoutes.GETALL() |> buildRoute config <| projectId
        let decoder = Types.Unit.DecodeMany
        return! Fetch.tryGet(url, decoder = decoder)
    }

    let deleteUnit (config : Config.T) (projectId : Guid) (unitId : Guid) : Promise<unit> = promise {
        let url = Project.UnitRoutes.DELETE() |> buildRoute2 config <| (projectId, unitId )
        return! Fetch.delete(url)
    }

    let loadAllProjects (config : Config.T) : Promise<Project list> = promise {
        let url = Project.GETALL |> buildStaticRoute config
        let decoder = Types.Project.DecodeMany
        return! Fetch.get(url, decoder = decoder)
    }

    let loadProject (config : Config.T) (id : Guid) : Promise<Project> = promise {
        let url = Project.GET() |> buildRoute config <| id
        let decoder = Types.Project.Decoder
        return! Fetch.get(url, decoder = decoder)
    }

    let updateProject (config : Config.T) (project : Project) : Promise<Project> = promise {
        let url = Project.PUT() |> buildRoute config <| project.Id
        printfn "url to update project is %A" url
        let decoder = Types.Project.Decoder
        return! Fetch.put(url, project, decoder = decoder)
    }

    let updateUnitPriorities (config : Config.T) (projId : Guid) (updates : UnitPriority list) : Promise<Result<UnitPriority list, FetchError>> = promise {
        let url = Project.Priorities.PUT() |> buildRoute config <| projId
        printfn "sending updates to %A: %A" url updates
        let decoder = Types.UnitPriority.DecodeList
        let payload = UnitPriority.EncodeList updates
        return! Fetch.tryPut(url, payload, decoder = decoder)
    }

    open Fable.SimpleHttp

    let updateUnitPriorities2 (config : Config.T) (projId : Guid) (updates : UnitPriority list) = async {
        let url = Project.Priorities.PUT() |> buildRoute config <| projId
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

