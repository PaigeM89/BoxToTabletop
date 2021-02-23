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

    let buildStaticRoute (config : Config.T) route =
        Routes.combine config.ServerUrl route

    let buildRoute (config : Config.T) (routeEval) =
        fun x -> Routes.combine config.ServerUrl (sprintf routeEval x)

    let buildRoute2 (config : Config.T) routeEval =
        fun (x, y) -> Routes.combine config.ServerUrl (sprintf routeEval x y)

    let createUnit (config : Config.T) (unit : Types.Unit) : Promise<Types.Unit> = promise {
        let url = Project.UnitRoutes.POST() |> buildRoute config <| unit.ProjectId
        let data = Types.Unit.Encoder unit
        let _decoder = Types.Unit.Decoder
        let headers = [
            HttpRequestHeaders.Origin "*"
        ]
        return! Fetch.post(url, data, decoder = _decoder, headers = headers)
    }

    let updateUnit (config : Config.T) (unit : Types.Unit) : Promise<Types.Unit> = promise {
        //let url = sprintf "http://localhost:5000/api/v1/units/%O" (unit.Id.ToString("N"))
        let url = Project.UnitRoutes.PUT() |> buildRoute config <| unit.ProjectId
        let data = Types.Unit.Encoder unit
        let headers = [
            HttpRequestHeaders.Origin "*"
        ]
        let decoder = Types.Unit.Decoder
        return! Fetch.put(url, data, decoder= decoder, headers = headers)
    }

    let loadUnitsForProject (config : Config.T) (projectId : Guid) : Promise<Types.Unit list> = promise {
//        let url = "http://localhost:5000/units"
        let url = Project.UnitRoutes.GETALL() |> buildRoute config <| projectId
        let decoder = Types.Unit.DecodeMany
        return! Fetch.get(url, decoder = decoder)
    }

    let deleteUnit (config : Config.T) (projectId : Guid) (unitId : Guid) : Promise<unit> = promise {
        //let url = (sprintf "http://localhost:5000/units/%O" unitId)
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
        let url = Project.PUT() |> buildRoute config <| project
        let decoder = Types.Project.Decoder
        return! Fetch.put(url, project, decoder = decoder)
    }
