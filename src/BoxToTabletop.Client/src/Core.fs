namespace BoxToTabletop.Client

open BoxToTabletop.Domain
open BoxToTabletop.Domain.Types
open System
module Core =

    type Updates =
    /// A change in the visible columns, which needs to be propagated to multiple components.
    | ColumnSettingsChange of ColumnSettings

module Promises =
    open Fable.Core
    open Fable.Core.JsInterop
    open Fetch
    open Thoth.Fetch
    open Fetch.Types
    open Fable.Core.JS

    let createUnit (unit : Unit) : Promise<Unit> = promise {
        let url = "http://localhost:5000/units"
        let data = Unit.Encoder unit
        let decoder : Thoth.Json.Decoder<Types.Unit> = Types.Unit.Decoder
        let headers = [
            HttpRequestHeaders.Origin "*"
        ]
        return! Fetch.post(url, data, decoder = decoder, headers = headers)
    }

    let loadUnitsForProject (projectId : Guid) : Promise<Types.Unit list> = promise {
        let url = "http://localhost:5000/units"
        let decoder = Types.Unit.DecodeMany
        return! Fetch.get(url, decoder = decoder)
    }


    let deleteUnit (unitId : Guid) : Promise<unit> = promise {
        let url = (sprintf "http://localhost:5000/units/%O" unitId)
        return! Fetch.delete(url)
    }
