namespace BoxToTabletop.Client

open BoxToTabletop.Domain
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
        ShowError : bool
    } with
        static member Init() = {
            UnitName = ""
            ModelCount = 0
            AssembledCount = 0
            ShowError = false
        }

        member this.IsValid() = String.length this.UnitName > 0


    type Model = {
        Project : Types.Project
        PartialData : PartialData
        ColumnSettings : Types.ColumnSettings
        //ShouldClear : bool
    } with
        static member Init() = {
            Project = {
                Project.Empty() with Units = mockUnits
            }
            PartialData = PartialData.Init()
            ColumnSettings = Types.ColumnSettings.Empty()
        }

    module Model =
        let clearAndAddUnit unit model =
            { model with
                PartialData = PartialData.Init()
                Project = { model.Project with Units = unit :: model.Project.Units }
                //ShouldClear = true
            }

        let markShowErrors flag model =
            { model with PartialData = { model.PartialData with ShowError = flag } }

        let removeRowById (id : Guid) model =
            { model with Project = { model.Project with Units = model.Project.Units |> List.filter (fun x -> x.Id <> id) } }

    type Msg =
    | None
    | UpdateUnitName of newName : string
    | UpdateUnitModelCount of newCount : int
    | UpdateAssembledCount of newCount : int
    | AddUnit
    | DeleteRow of id : Guid option
    | UpdatedColumnSettings of cs : Types.ColumnSettings

    module View =
        open Fable.React
        open Fable.React.Helpers
        //https://github.com/fable-compiler/fable-react/blob/master/src/Fable.React.Props.fs
        open Fable.React.Props
        //https://github.com/fable-compiler/fable-react/blob/master/src/Fable.React.Standard.fs
        open Fable.React.Standard

        let unitRow (cs : ColumnSettings) (unit : Unit) dispatch =
            tr [] [
                td [] [ str unit.Name ]
                td [] [ str (string unit.Models) ]
                if cs.ShowAssembled then td [] [ str (string unit.Assembled) ]
                td [] [ Delete.delete [ Delete.Size IsMedium; Delete.OnClick (fun _ -> unit.Id |> Some |> DeleteRow |> dispatch ) ] [ ] ]
            ]

        let view (model : Model) (dispatch : Msg -> unit) =
            let tableHeaders =
                tr [ Class "table" ] [
                    th [] [ str "Name" ]
                    th [] [ str "Models" ]
                    if model.ColumnSettings.ShowAssembled then th [] [ str "Assembled" ]
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
                        input [ Id "modelCount" ; Type "number"; DefaultValue model.PartialData.ModelCount; OnChange (fun ev -> ev.Value |> Helpers.parseIntOrZero |> UpdateUnitModelCount |> dispatch) ]
                    ]
                let numericInput name dv action =
                    Notification.notification [ notifications ] [
                        input [ Id name ; Type "number"; DefaultValue dv; OnChange (fun ev -> ev.Value |> Helpers.parseIntOrZero |> action |> dispatch) ]
                    ]

                let mods : Modifier.IModifier list =
                       [ Modifier.TextAlignment (Screen.All, TextAlignment.Centered)]

                //let mods' = Modifiers [ mods ]
                // todo: make this not ugly
                let deleteButton = button [ OnClick (fun _ -> Msg.AddUnit |> dispatch )] [ str "Add" ]

                tr [] [
                    td [] [ nameInput ]
                    td [] [ modelCountInput ]
                    if model.ColumnSettings.ShowAssembled then td [] [ numericInput "assembled" model.PartialData.AssembledCount UpdateAssembledCount ]
                    td [] [ deleteButton ]
                ]
            let table =
                Table.table [ Table.IsBordered; Table.IsStriped ] [
                    yield tableHeaders
                    yield addRow
                    for unit in model.Project.Units do yield unitRow model.ColumnSettings unit dispatch
                ]
            table

    let update (model : Model) (msg : Msg)  =
        match msg with
        | None -> model, None
        | UpdateUnitName newName ->
            { model with PartialData = { model.PartialData with UnitName = newName } }, None
        | UpdateUnitModelCount newCount ->
            { model with PartialData = { model.PartialData with ModelCount = newCount } }, None
        | UpdateAssembledCount newCount ->
            { model with PartialData = { model.PartialData with AssembledCount = newCount } }, None
        | AddUnit ->
            let p = model.PartialData
            if p.IsValid() then
                let unit = { Types.Unit.Empty() with Name = p.UnitName; Models = p.ModelCount }
                model |> Model.clearAndAddUnit unit, None
            else
                model |> Model.markShowErrors true, None
        | DeleteRow (Some id) ->
                model |> Model.removeRowById id, None
        | DeleteRow (Option.None) ->
                printfn "Unable to delete row without ID"
                model, None
        | UpdatedColumnSettings cs ->
                { model with ColumnSettings = cs }, None

