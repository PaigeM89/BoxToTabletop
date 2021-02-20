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
        PartialCounts : Types.ModelCount list
        ShowError : bool
    } with
        static member Init() = {
            UnitName = ""
            ModelCount = 0
            PartialCounts = []
            ShowError = false
        }

        member this.IsValid() = String.length this.UnitName > 0


    type Model = {
        Project : Types.Project
        PartialData : PartialData
        Columns : Types.ModelCountCategory list
    } with
        static member Init() = {
            Project = {
                Project.Empty() with Units = mockUnits
            }
            PartialData = PartialData.Init()
            Columns = []
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
    | UpdateUnitCountCategoryValue of category : Types.ModelCountCategory * newValue : int
    | AddUnit
    | DeleteRow of id : Guid option

    module View =
        open Fable.React
        open Fable.React.Helpers
        //https://github.com/fable-compiler/fable-react/blob/master/src/Fable.React.Props.fs
        open Fable.React.Props
        //https://github.com/fable-compiler/fable-react/blob/master/src/Fable.React.Standard.fs

        let unitRow (unit : Unit) dispatch =
            let categoryValues =
                [
                    for count in unit.ModelCounts ->
                        if count.Category.Enabled then td [] [ str count.Category.Name ] |> Some else Option.None
                ] |> List.choose id
            tr [] [
                td [] [ str unit.Name ]
                td [] [ str (string unit.Models) ]
                yield! categoryValues
                td [] [ Delete.delete [ Delete.Size IsMedium; Delete.OnClick (fun _ -> unit.Id |> Some |> DeleteRow |> dispatch ) ] [ ] ]
            ]

        let view (model : Model) (dispatch : Msg -> unit) =
            printfn "drawing project. columns are %A" model.Columns
            let categoryHeaders =
                [
                    for cat in model.Columns ->
                        if cat.Enabled then td [] [ str cat.Name ] |> Some else Option.None
                ] |> List.choose id
            let tableHeaders =
                tr [ Class "table" ] [
                    th [] [ str "Name" ]
                    th [] [ str "Models" ]
                    yield! categoryHeaders
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
                        input [ Id "modelCount" ; Type "number"; DefaultValue model.PartialData.ModelCount; OnChange (fun ev -> ev.Value |> Helpers.parseIntOrZero |> UpdateUnitModelCount |> dispatch) ]
                    ]
                let numericInput name dv action =
                    Notification.notification [ notifications ] [
                        input [ Id name ; Type "number"; DefaultValue dv; OnChange (action) ]
                    ]

                let mods : Modifier.IModifier list =
                       [ Modifier.TextAlignment (Screen.All, TextAlignment.Centered)]

                //let mods' = Modifiers [ mods ]
                // todo: make this not ugly
                let deleteButton = button [ OnClick (fun _ -> Msg.AddUnit |> dispatch )] [ str "Add" ]

                let countColumns =
                    [
                        for col in model.Columns ->
                            if col.Enabled then
                                let maybeCount = Types.getCountColumn col model.PartialData.PartialCounts
                                                 |> Option.map (fun o -> o.Count)
                                let count = Option.defaultValue 0 maybeCount
                                let func (ev : Browser.Types.Event) : unit =
                                        let count = Helpers.parseIntOrZero ev.Value
                                        UpdateUnitCountCategoryValue (col, count)
                                        |> dispatch
                                numericInput col.Name count func |> Some
                            else
                                Option.None
                    ] |> List.choose id

                tr [] [
                    td [] [ nameInput ]
                    td [] [ modelCountInput ]
                    yield! countColumns
                    td [] [ deleteButton ]
                ]
            let table =
                Table.table [ Table.IsBordered; Table.IsStriped ] [
                    yield tableHeaders
                    yield addRow
                    for unit in model.Project.Units do yield unitRow unit dispatch
                ]
            table

    let handleCoreUpdate (update : Core.Updates) (model : Model) =
        match update with
        | Core.MCCVisibilityChange mcc ->
            printfn "Hnadling visibility update"
            let removed = model.Columns |> List.filter (fun x -> Helpers.stringsEqualCI mcc.Name x.Name)
            { model with Columns = (mcc :: removed) }, Noop



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
        | UpdateUnitCountCategoryValue (mcc, newValue) ->
            let existing = model.PartialData.PartialCounts |> List.tryFind (fun x -> Helpers.stringsEqualCI mcc.Name x.Category.Name)
            match existing with
            | Some e ->
                    let newMC = { e with Count = newValue }
                    let filteredMccs = model.PartialData.PartialCounts |> List.filter(fun x -> not (Helpers.stringsEqualCI mcc.Name x.Category.Name))
                    let newPartial = { model.PartialData with PartialCounts = (newMC :: filteredMccs) }
                    {model with PartialData = newPartial}, Noop
            | None ->
                    let newMC = { Types.ModelCount.Empty() with Count = newValue; Category = mcc }
                    let filteredMccs = model.PartialData.PartialCounts |> List.filter(fun x -> not (Helpers.stringsEqualCI mcc.Name x.Category.Name))
                    let newPartial = { model.PartialData with PartialCounts = (newMC :: filteredMccs) }
                    {model with PartialData = newPartial}, Noop
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

