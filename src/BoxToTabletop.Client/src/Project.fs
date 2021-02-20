namespace BoxToTabletop.Client

open BoxToTabletop.Domain
open BoxToTabletop.Domain.Helpers
open BoxToTabletop.Domain.Types
open Elmish
open System
open FSharp.Control
open Fulma
open Fulma

module Project =
    open FSharp.Control.AsyncRx
    open Fable.Reaction

    let mockUnits = [
        { Unit.Empty() with Name = "Test Unit 1"; Models = 3 }
        { Unit.Empty() with Name = "Test Unit 2"; Models = 10 }
    ]

    type PartialData = {
        UnitName : string
        ModelCount : int
        AssembledCount : int
        PrimedCount : int
        PaintedCount : int
        BasedCount : int
        ShowError : bool
    } with
        static member Init() = {
            UnitName = ""
            ModelCount = 0
            AssembledCount = 0
            PrimedCount = 0
            PaintedCount = 0
            BasedCount = 0
            ShowError = false
        }

        member this.IsValid() = String.length this.UnitName > 0


    type Model = {
        Project : Types.Project
        PartialData : PartialData
        ColumnSettings : ColumnSettings
    } with
        static member Init() = {
            Project = {
                Project.Empty() with Units = mockUnits
            }
            PartialData = PartialData.Init()
            ColumnSettings = ColumnSettings.Empty()
        }

    module Model =
        let clearAndAddUnit unit model =
            { model with
                PartialData = PartialData.Init()
                Project = { model.Project with Units = unit :: model.Project.Units }
            }

        let markShowErrors flag model =
            { model with PartialData = { model.PartialData with ShowError = flag } }

        let removeRowById (id : Guid) model =
            { model with Project = { model.Project with Units = model.Project.Units |> List.filter (fun x -> x.Id <> id) } }

    type Msg =
    | Noop
    | CoreUpdate of update : BoxToTabletop.Client.Core.Updates
    | UpdateUnitName of newName : string
    | UpdateUnitModelCount of newCount : int
    | UpdatePartialData of newPartial : PartialData
    | AddUnit
    | DeleteRow of id : Guid option

    module View =
        open Fable.React
        open Fable.React.Helpers
        //https://github.com/fable-compiler/fable-react/blob/master/src/Fable.React.Props.fs
        open Fable.React.Props
        //https://github.com/fable-compiler/fable-react/blob/master/src/Fable.React.Standard.fs

        let unitRow (cs : ColumnSettings) (unit : Unit) dispatch =
            let optionalColumns = [
                for (name, value) in Unit.enumerateColumns cs unit ->
                    td [] [ str (string value) ]
            ]
            tr [] [
                td [] [ str unit.Name ]
                td [] [ str (string unit.Models) ]
                yield! optionalColumns
                td [] [ Delete.delete [ Delete.Size IsMedium; Delete.OnClick (fun _ -> unit.Id |> Some |> DeleteRow |> dispatch ) ] [ ] ]
            ]

        let view (model : Model) (dispatch : Msg -> unit) =
            printfn "drawing project. columns are %A" model.ColumnSettings
            let optionalColumns =
                [
                    for (name, value) in model.ColumnSettings.Enumerate() ->
                        if value then td [] [ str name ] |> Some else None
                ] |> List.choose id
            let tableHeaders =
                tr [ Class "table" ] [
                    th [] [ str "Name" ]
                    th [] [ str "Models" ]
                    yield! optionalColumns
                    // add a blank header for the add/delete button column
                    th [] []
                ]
            let addRow =
                let notifications = if model.PartialData.ShowError then Notification.Color IsDanger else Notification.Color IsWhite

                let nameInput =
                    Notification.notification [ notifications ] [
                        input [ Id "nameInput"; DefaultValue model.PartialData.UnitName; OnChange (fun ev -> UpdateUnitName ev.Value |> dispatch) ]
                    ]

                let modelCountInput =
                    Notification.notification [ notifications ] [
                        input [ Id "modelCount" ; Type "number"; DefaultValue model.PartialData.ModelCount; OnChange (fun ev -> ev.Value |> Parsing.parseIntOrZero |> UpdateUnitModelCount |> dispatch) ]
                    ]

                let numericInput name dv action =
                    Notification.notification [ notifications ] [
                        input [ Id name ; Type "number"; DefaultValue dv; OnChange (action) ]
                    ]

//                let mods : Modifier.IModifier list =
//                       [ Modifier.TextAlignment (Screen.All, TextAlignment.Centered)]

                //let mods' = Modifiers [ mods ]
                // todo: make this not ugly
                let deleteButton = button [ OnClick (fun _ -> Msg.AddUnit |> dispatch )] [ str "Add" ]

                let optionalColumns cs (partial : PartialData) =
                    let func (ev : Browser.Types.Event) transform =
                        let c = Parsing.parseIntOrZero ev.Value
                        UpdatePartialData (transform partial c)
                        |> dispatch

                    [
                        if cs.AssemblyVisible then numericInput "Assembled" partial.AssembledCount (fun ev -> func ev (fun p c -> { p with AssembledCount = c }))
                        if cs.PrimedVisible then numericInput "Primed" partial.PrimedCount (fun ev -> func ev (fun p c -> { p with PrimedCount = c }))
                        if cs.PaintedVisible then numericInput "Painted" partial.PaintedCount (fun ev -> func ev (fun p c -> { p with PaintedCount = c }))
                        if cs.BasedVisible then numericInput "Based" partial.BasedCount (fun ev -> func ev (fun p c -> { p with BasedCount = c }))
                    ]

                tr [] [
                    td [] [ nameInput ]
                    td [] [ modelCountInput ]
                    yield! optionalColumns model.ColumnSettings model.PartialData
                    td [] [ deleteButton ]
                ]
            let table =
                Table.table [ Table.IsBordered; Table.IsStriped ] [
                    yield tableHeaders
                    yield addRow
                    for unit in model.Project.Units do yield unitRow model.ColumnSettings unit dispatch
                ]
            table

    let handleCoreUpdate (update : Core.Updates) (model : Model) =
        match update with
        | Core.ColumnSettingsChange cs ->
            { model with ColumnSettings = cs }, Noop


    let update (model : Model) (msg : Msg) : (Model * Msg) =
        match msg with
        | Noop -> model, Noop
        | CoreUpdate update ->
            printfn "handling core update in project"
            handleCoreUpdate update model
        | UpdateUnitName newName ->
            { model with PartialData = { model.PartialData with UnitName = newName } }, Noop
        | UpdateUnitModelCount newCount ->
            { model with PartialData = { model.PartialData with ModelCount = newCount } }, Noop
        | UpdatePartialData newPartial ->
            { model with PartialData = newPartial }, Noop
        | AddUnit ->
            let p = model.PartialData
            if p.IsValid() then
                let unit = { Types.Unit.Empty() with Name = p.UnitName; Models = p.ModelCount }
                model |> Model.clearAndAddUnit unit, Noop
            else
                model |> Model.markShowErrors true, Noop
        | DeleteRow (Some id) ->
                model |> Model.removeRowById id, Noop
        | DeleteRow (None) ->
                printfn "Unable to delete row without ID"
                model, Noop

