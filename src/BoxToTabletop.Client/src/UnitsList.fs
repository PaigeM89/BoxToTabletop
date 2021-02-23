namespace BoxToTabletop.Client

open BoxToTabletop.Domain
open BoxToTabletop.Domain.Helpers
open BoxToTabletop.Domain.Types
open Browser
open Browser.Types
open Elmish
open System
open FSharp.Control
open Fulma

module UnitsList =
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

        member this.ToUnit() = {
            Unit.Empty() with
                Name = this.UnitName
                Models = this.ModelCount
                Assembled = this.AssembledCount
                Primed = this.PrimedCount
                Painted = this.PaintedCount
                Based = this.BasedCount
        }

    type AlertMessage =
    | InfoMessage of msg : string
    | ErrorMessage of msg : string

    type Model = {
        //Project : Types.Project
        ProjectId : Guid
        Units : Types.Unit list
        PartialData : PartialData
        ColumnSettings : ColumnSettings
        SaveError : AlertMessage option
        Config : Config.T
    } with
        static member Init(config : Config.T) = {
//            Project = {
//                Project.Empty() with Units = mockUnits
//            }
            ProjectId = Guid.Empty
            Units = []
            PartialData = PartialData.Init()
            ColumnSettings = ColumnSettings.Empty()
            SaveError = None //Some "Test save error"
            Config = config
        }

    module Model =
        let clearPartialData (model : Model) =
            { model with PartialData = PartialData.Init() }

        let orderUnits (model : Model) =
            { model with Units = model.Units |> List.sortBy (fun x -> x.Priority) }

        let addUnit (unit : Unit) ( model : Model) =
            let unit = { unit with ProjectId = model.ProjectId }
            let units = model.Units |> List.map (fun x -> { x with Priority = x.Priority + 1 })
            printfn "model project id when adding unit is %A" model.ProjectId
            let units = unit :: units
            unit, { model with Units = units }

        let markShowErrors flag model =
            { model with PartialData = { model.PartialData with ShowError = flag } }

        let removeRowById (id : Guid) (model : Model) =
            { model with Units = model.Units |> List.filter (fun x -> x.Id <> id) }
            //{ model with Project = { model.Project with Units = model.Project.Units |> List.filter (fun x -> x.Id <> id) } }

        let replaceUnitInPlace (unit : Unit) (model : Model) =
            let existing = model.Units |> List.tryFind (fun x -> x.Id = unit.Id)
            match existing with
            | Some e ->
                let priority = e.Priority
                let units = model.Units |> List.filter (fun x -> x.Id <> unit.Id)
                let unit = { unit with Priority = priority }
                let units = unit :: units
                unit, { model with Units = units }
            | None ->
                addUnit unit model

    type Msg =
    | Noop
    | LoadUnitsForProject of projectId : Guid
    | LoadUnitsSuccess of units : Unit list
    | LoadUnitsFailure of exn
    | UpdatePartialData of newPartial : PartialData
    | AddUnit
    | AddUnitSuccess of result : Types.Unit
    | AddUnitFailure of exn
    | UpdateUnitRow of updatedUnit : Types.Unit
    | UpdateUnitSuccess of updatedUnit : Types.Unit
    | UpdateUnitFailure of exn
    | DeleteRow of id : Guid option
    | DeleteRowError of exn
    | RemoveSaveErrorMessage
    | UpdatedColumnSettings of cols : ColumnSettings

    module View =
        open Fable.React
        open Fable.React.Helpers
        //https://github.com/fable-compiler/fable-react/blob/master/src/Fable.React.Props.fs
        open Fable.React.Props
        //https://github.com/fable-compiler/fable-react/blob/master/src/Fable.React.Standard.fs
        open Fable.FontAwesome

        let addUnitWrapper custom elements =
            div [ ClassName "Block"  ] [ Box.box' custom [
                Level.level [ Level.Level.CustomClass "no-flex-shrink" ] [
                    Level.item [ Level.Item.HasTextCentered; Level.Item.CustomClass "no-flex-shrink" ] [ div [] elements ]
                ]
            ]]

        let numericInput inputColor name (dv : int) action =
            Input.number [
                Input.Id name
                Input.Placeholder (string 0)
                inputColor
                Input.ValueOrDefault (string dv)
                Input.OnChange action
                Input.Props [ HTMLAttr.Min 0; HTMLAttr.FrameBorder "1px solid";  ]
                Input.CustomClass "numeric-input-width"
            ]

        let newUnitNumericInput inputColor name (dv : int) action =
            addUnitWrapper [] [
                Level.heading [] [ str name ]
                Field.div [ Field.HasAddonsRight] [
                    Level.item [ Level.Item.CustomClass "no-flex-shrink" ] [
                        numericInput inputColor name dv action
                    ]
                ]
            ]

        let unitNameInput inputColor partial dispatch =
            let changeHandler (ev : Event) =
                { partial with UnitName = ev.Value }
                |> UpdatePartialData
                |> dispatch

            Level.level [  Level.Level.Modifiers [  Modifier.Display (Screen.All, Fulma.Display.Option.Flex) ]; Level.Level.CustomClass "no-flex-shrink"] [
                Level.item [ Level.Item.HasTextCentered; Level.Item.CustomClass "no-flex-shrink" ] [
                    div []  [
                        Level.heading [] [ str "Unit Name" ]
                        Level.item [] [
                            Input.text [
                                inputColor
                                Input.Placeholder "Unit name"; Input.ValueOrDefault partial.UnitName; Input.OnChange changeHandler
                            ]
                        ]
                    ]
                ]
            ] |> List.singleton |> addUnitWrapper []

        let inputNewUnit cs partial dispatch =
            let func (ev : Browser.Types.Event) transform =
                let c = Parsing.parseIntOrZero ev.Value
                UpdatePartialData (transform partial c)
                |> dispatch
            let inputColor =
                if partial.ShowError then IsDanger else NoColor
                |> Input.Color
            Level.level [ ] [
                unitNameInput inputColor partial dispatch
                newUnitNumericInput inputColor "Models" partial.ModelCount (fun ev -> func ev (fun p c -> { p with ModelCount = c }))
                if cs.AssemblyVisible then newUnitNumericInput inputColor "Assembled" partial.AssembledCount (fun ev -> func ev (fun p c -> { p with AssembledCount = c }))
                if cs.PrimedVisible then newUnitNumericInput inputColor "Primed" partial.PrimedCount (fun ev -> func ev (fun p c -> { p with PrimedCount = c }))
                if cs.PaintedVisible then newUnitNumericInput inputColor "Painted" partial.PaintedCount (fun ev -> func ev (fun p c -> { p with PaintedCount = c }))
                if cs.BasedVisible then newUnitNumericInput inputColor "Based" partial.BasedCount (fun ev -> func ev (fun p c -> { p with BasedCount = c }))
                addUnitWrapper [  CustomClass "add-unit-button-box" ] [
                    Button.Input.submit [
                        Button.Props [ Value "Add" ]
                        Button.Color IsSuccess
                        Button.OnClick (fun _ -> AddUnit |> dispatch)
                    ]
                ]
            ]

        let unitRow (cs : ColumnSettings) (unit : Unit) dispatch =
            let changeHandler transform (ev : Browser.Types.Event)  =
                let i = Parsing.parseIntOrZero ev.Value
                UpdateUnitRow (transform unit i )
                |> dispatch

            let modelCountFunc (ev : Browser.Types.Event) = changeHandler (fun u i -> { u with Models = i }) ev
            let assembledFunc  (ev : Browser.Types.Event) = changeHandler (fun u i -> { u with Assembled = i }) ev
            let primedFunc  (ev : Browser.Types.Event) = changeHandler (fun u i -> { u with Primed = i }) ev
            let paintedFunc  (ev : Browser.Types.Event) = changeHandler (fun u i -> { u with Painted = i }) ev
            let basedFunc  (ev : Browser.Types.Event) = changeHandler (fun u i -> { u with Based = i }) ev

            let nc = Input.Color NoColor
            let unitId = unit.Id.ToString("N") + "-"

            let optionalColumns = [
                if cs.AssemblyVisible then td [] [numericInput nc (unitId + "assembled") unit.Assembled assembledFunc ]
                if cs.PrimedVisible then td [] [numericInput nc (unitId + "primed") unit.Primed primedFunc ]
                if cs.PaintedVisible then td [] [numericInput nc (unitId + "painted") unit.Painted paintedFunc ]
                if cs.BasedVisible then td [] [numericInput nc (unitId + "based") unit.Based basedFunc ]
            ]
            tr [] [
                td [] [
                    //todo: shrink this a little
                    Input.input [ Input.ValueOrDefault unit.Name ]
                ]
                td [] [
                    numericInput (Input.Color NoColor) (unit.Id.ToString() + "-models") unit.Models modelCountFunc
                ]
                yield! optionalColumns
                td [] [ Delete.delete [ Delete.Size IsMedium; Delete.OnClick (fun _ -> unit.Id |> Some |> DeleteRow |> dispatch ) ] [ ] ]
            ]

        let mapSaveError (model : Model) dispatch =
            let structure color header message =
                Message.message [ Message.Color color ] [
                    Message.header [] [
                        str header
                        Delete.delete [ Delete.OnClick (fun _ -> dispatch RemoveSaveErrorMessage) ] [ ]
                    ]
                    Message.body [] [
                        str message
                    ]
                ]
            match model.SaveError with
            | Some (InfoMessage msg) ->
                structure IsInfo "" msg |> Some
            | Some (ErrorMessage msg) ->
                structure IsDanger "Error" msg |> Some
            | None -> None

        let view (model : Model) (dispatch : Msg -> unit) =
            let saveError = mapSaveError model dispatch

            let optionalColumnHeaders =
                [
                    for (name, value) in model.ColumnSettings.Enumerate() ->
                        if value then th [] [ str name ] |> Some else None
                ] |> List.choose id
            let tableHeaders =
                tr [ Class "table" ] [
                    th [] [ str "Name" ]
                    th [] [ str "Models" ]
                    yield! optionalColumnHeaders
                    // add a blank header for the add/delete button column
                    th [] []
                ]

            let table =
                [
                    yield tableHeaders
                    for unit in model.Units do yield unitRow model.ColumnSettings unit dispatch
                ]
                |> Table.table [ Table.IsBordered; Table.IsStriped; Table.IsNarrow; Table.IsHoverable; Table.CustomClass "list-units-table" ]
            Section.section [] [
                if Option.isSome saveError then Option.get saveError
                Heading.h3 [ Heading.IsSubtitle  ] [ Text.p [ Modifiers [ Modifier.TextAlignment (Screen.All, TextAlignment.Centered) ] ][ str "Add a new unit" ]]
                inputNewUnit model.ColumnSettings model.PartialData dispatch
                hr []
                Columns.columns [ Columns.IsGap(Screen.All, Columns.Is1) ] [
                    Column.column [  ] [
                        table
                    ]
                    Column.column [ Column.Width(Screen.All, Column.IsNarrow) ] [ ]
                ]
            ]

    let handleCoreUpdate (update : Core.Updates) (model : Model) =
        match update with
        | Core.ColumnSettingsChange cs ->
            printfn "Updating column settings from %A to %A" model.ColumnSettings cs
            { model with ColumnSettings = cs }, Cmd.none

    module Fetching =
        open Fable.Core.JS
        open Fable.Core
        open Fable.Core.JsInterop
        open Fetch
        open Thoth.Fetch

        let getUnits (model : Model) (projectId : Guid) =
            let model = { model with ProjectId = projectId }
            printfn "Model project id is %A" model.ProjectId
            let promise projId = BoxToTabletop.Client.Promises.loadUnitsForProject model.Config projId
            let cmd = Cmd.OfPromise.either promise projectId LoadUnitsSuccess LoadUnitsFailure
            model, cmd

        let saveUnit (model : Model) (unit : Types.Unit) =
            printfn "Model project id is %A" model.ProjectId
            let unit, model = model |> Model.clearPartialData |> Model.addUnit unit
            let saveUnitFunc (unit' : Types.Unit) = BoxToTabletop.Client.Promises.createUnit model.Config unit'
            let promiseSave = Cmd.OfPromise.either saveUnitFunc unit AddUnitSuccess AddUnitFailure
            model |> Model.orderUnits, promiseSave

        /// Updates a unit in the project
        /// This function assumes Priority has not changed and a model will not change position.
        let updateUnit (model : Model) (unit : Types.Unit) =
            printfn "unit project id in update is %A" unit.ProjectId
            let unit, model = Model.replaceUnitInPlace unit model

            let updateFunc (u : Types.Unit) = Promises.updateUnit model.Config u
            let promiseUpdate = Cmd.OfPromise.either updateFunc unit UpdateUnitSuccess UpdateUnitFailure
            model, promiseUpdate

        let deleteUnit (model : Model) (unitId : Guid) =
            let model = model |> Model.removeRowById unitId |> Model.orderUnits
            let deleteFunc id = Promises.deleteUnit model.Config model.ProjectId id
            let cmd = Cmd.OfPromise.attempt deleteFunc unitId DeleteRowError
            model, cmd


    let update (model : Model) (msg : Msg) =
        match msg with
        | Noop -> model, Cmd.none
        | LoadUnitsForProject projectId ->
           Fetching.getUnits model projectId
        | LoadUnitsSuccess units ->
            { model with Units = units }, Cmd.none
        | LoadUnitsFailure e ->
            printfn "%A" e
            { model with SaveError = Some (sprintf "Error loading: %A" e.Message |> ErrorMessage) }, Cmd.none
        | UpdatePartialData newPartial ->
            { model with PartialData = newPartial }, Cmd.none
        | AddUnit ->
            let p = model.PartialData
            if p.IsValid() then
                let unit = p.ToUnit()
                let model, promiseSave = Fetching.saveUnit model unit
                model, promiseSave
            else
                model |> Model.markShowErrors true, Cmd.none
        | AddUnitSuccess _ ->
            printfn "%s" "save successful"
            { model with SaveError = None}, Cmd.none
        | AddUnitFailure err ->
            printfn "%A" err
            let addErr = sprintf "Error adding new unit: %A" err.Message |> ErrorMessage |> Some
            { model with SaveError = addErr }, Cmd.none
        | UpdateUnitRow unitToUpdate ->
            printfn "Updating unit %A" unitToUpdate
            let model, cmd = Fetching.updateUnit model unitToUpdate
            model, cmd
        | UpdateUnitSuccess _ ->
            { model with SaveError = None }, Cmd.none
        | UpdateUnitFailure err ->
            printfn "%A" err
            let updateErr = sprintf "Error updating unit: %A" err.Message |> ErrorMessage |> Some
            { model with SaveError = updateErr }, Cmd.none
        | DeleteRow (Some id) ->
            //model |> Model.removeRowById id, Cmd.OfPromise.attempt Promises.deleteUnit id DeleteRowError
            Fetching.deleteUnit model id
        | DeleteRow (None) ->
            printfn "Unable to delete row without ID"
            model, Cmd.none
        | DeleteRowError e ->
            printfn "%A" e
            { model with  SaveError = ErrorMessage "Error deleting unit" |> Some }, Cmd.none
        | RemoveSaveErrorMessage ->
            { model with SaveError = None }, Cmd.none
        | UpdatedColumnSettings cols ->
            printfn "in units list project settings updated, cols %A" cols
            { model with ColumnSettings = cols }, Cmd.none

