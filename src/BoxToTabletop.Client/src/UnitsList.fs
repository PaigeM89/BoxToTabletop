namespace BoxToTabletop.Client

open BoxToTabletop.Domain
open BoxToTabletop.Domain.Helpers
open BoxToTabletop.Domain.Routes.Project
open BoxToTabletop.Domain.Types
open Browser
open Browser.Types
open Elmish
open System
open FSharp.Control
open FSharpx.Collections
open Fulma

module UnitsList =
    open FSharp.Control.AsyncRx
    open Fable.Reaction
    open BTT.Collections

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
        ProjectId : Guid
        PartialData : PartialData
        ColumnSettings : ColumnSettings
        Units : ResizeArray<Types.Unit>
        ErrorMessage : AlertMessage option
        Config : Config.T
    } with
        static member Init(config : Config.T) = {
            ProjectId = Guid.Empty
            PartialData = PartialData.Init()
            ColumnSettings = ColumnSettings.Empty()
            Units = ResizeArray()
            ErrorMessage = None
            Config = config
        }

    module Model =

        module Units =
            let setProjectId projectId _unit = { _unit with Unit.ProjectId = projectId }

        let setErrorMessage msg model = { model with ErrorMessage = Some (ErrorMessage msg) }
        let setInfoMessage msg model = { model with ErrorMessage = Some (InfoMessage msg) }
        let removeErrorMessage model = { model with ErrorMessage = None }

//        let createUnitFromPartial model =
//            model.PartialData.ToUnit() |> Units.setProjectId model.ProjectId

        let addNewUnit (unit : Unit) model =
            let unit  = { unit with Priority = 0 }
            printfn "Inserting new unit: %A" unit
            let ra = model.Units |> ResizeArray.map (fun x -> { x with Priority = x.Priority + 1 } )
            printfn "bumped array is %A" ra
            ra.Insert(0, unit)
            { model with Units = ra }

        let replaceUnit (unit : Types.Unit) model =
            let ra =
                model.Units
                |> ResizeArray.filter (fun x -> x.Id <> unit.Id)
            ra.Insert(unit.Priority, unit)
            ResizeArray.sortBy (fun x -> x.Priority) ra

            { model with Units = ra }



        module Updates =
            let setUnits units model = { model with Model.Units = units }
            let setUnitsFromList (units : Unit list) model =
                let ra = ResizeArray()
                units |> List.iter ra.Add
                ResizeArray.sortBy (fun x -> x.Priority) ra
                { model with Model.Units = ra }, Cmd.none


    type Msg =
    | UpdatePartialData of newPartial : PartialData
    | RemoveErrorMessage
    | UpdatedColumnSettings of cols : ColumnSettings
    | LoadUnitsForProject of projectId : Guid
    | LoadUnitsResponse of response : Result<Unit list, Thoth.Fetch.FetchError>
    | LoadUnitsFailure of exn
    | TryAddNewUnit of unit : Unit
    | AddNewUnitResponse of response : Result<Unit, Thoth.Fetch.FetchError>
    | AddNewUnitFailure of exn
    | TryUpdateRow of unit: Unit
    | UpdateRowResponse of Result<Unit, Thoth.Fetch.FetchError>
    | UpdateRowFailure of exn
    /// If a unit is added or moved, all unit priorities get updated.
    | TryUpdatePriorities
    //| UpdatePrioritiesResponse of Result<UnitPriority list, Thoth.Fetch.FetchError>
    | UpdatePrioritiesResponse2 of Result<unit, int>
    | UpdatePrioritiesFailure of exn
    | TryDeleteRow of unitId : Guid
    | DeleteRowFailure of exn

    module View =
        open Fable.React
        open Fable.React.Helpers
        //https://github.com/fable-compiler/fable-react/blob/master/src/Fable.React.Props.fs
        open Fable.React.Props
        //https://github.com/fable-compiler/fable-react/blob/master/src/Fable.React.Standard.fs
        open Fable.FontAwesome

//        let addUnitWrapper custom elements =
//            div [ ClassName "Block"  ] [ Box.box' custom [
//                Level.level [ Level.Level.CustomClass "no-flex-shrink" ] [
//                    Level.item [ Level.Item.HasTextCentered; Level.Item.CustomClass "no-flex-shrink" ] [ div [] elements ]
//                ]
//            ]]

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

//        let newUnitNumericInput inputColor name (dv : int) action =
//            addUnitWrapper [] [
//                Level.heading [] [ str name ]
//                Field.div [ Field.HasAddonsRight] [
//                    Level.item [ Level.Item.CustomClass "no-flex-shrink" ] [
//                        numericInput inputColor name dv action
//                    ]
//                ]
//            ]
//
//        let unitNameInput inputColor partial dispatch =
//            let changeHandler (ev : Event) =
//                { partial with UnitName = ev.Value }
//                |> UpdatePartialData
//                |> dispatch
//
//            Level.level [  Level.Level.Modifiers [  Modifier.Display (Screen.All, Fulma.Display.Option.Flex) ]; Level.Level.CustomClass "no-flex-shrink"] [
//                Level.item [ Level.Item.HasTextCentered; Level.Item.CustomClass "no-flex-shrink" ] [
//                    div []  [
//                        Level.heading [] [ str "Unit Name" ]
//                        Level.item [] [
//                            Input.text [
//                                inputColor
//                                Input.Placeholder "Unit name"; Input.ValueOrDefault partial.UnitName; Input.OnChange changeHandler
//                            ]
//                        ]
//                    ]
//                ]
//            ] |> FSharp.Collections.List.singleton |> addUnitWrapper []

//        let inputNewUnit cs partial dispatch =
//            let func (ev : Browser.Types.Event) transform =
//                let c = Parsing.parseIntOrZero ev.Value
//                UpdatePartialData (transform partial c)
//                |> dispatch
//            let inputColor =
//                if partial.ShowError then IsDanger else NoColor
//                |> Input.Color
//            Level.level [ ] [
//                unitNameInput inputColor partial dispatch
//                newUnitNumericInput inputColor "Models" partial.ModelCount (fun ev -> func ev (fun p c -> { p with ModelCount = c }))
//                if cs.AssemblyVisible then newUnitNumericInput inputColor "Assembled" partial.AssembledCount (fun ev -> func ev (fun p c -> { p with AssembledCount = c }))
//                if cs.PrimedVisible then newUnitNumericInput inputColor "Primed" partial.PrimedCount (fun ev -> func ev (fun p c -> { p with PrimedCount = c }))
//                if cs.PaintedVisible then newUnitNumericInput inputColor "Painted" partial.PaintedCount (fun ev -> func ev (fun p c -> { p with PaintedCount = c }))
//                if cs.BasedVisible then newUnitNumericInput inputColor "Based" partial.BasedCount (fun ev -> func ev (fun p c -> { p with BasedCount = c }))
//                addUnitWrapper [  CustomClass "add-unit-button-box" ] [
//                    Button.Input.submit [
//                        Button.Props [ Value "Add" ]
//                        Button.Color IsSuccess
//                        Button.OnClick (fun _ -> TryAddNewUnit |> dispatch)
//                    ]
//                ]
//            ]

//        let unitRow (cs : ColumnSettings) (unit : Unit) dispatch =
//            let changeHandler transform (ev : Browser.Types.Event)  =
//                let i = Parsing.parseIntOrZero ev.Value
//                ()
//
//            let modelCountFunc (ev : Browser.Types.Event) = changeHandler (fun (u : Unit) i -> u.Priority, { u with Models = i }) ev
//            let assembledFunc  (ev : Browser.Types.Event) = changeHandler (fun (u : Unit) i -> u.Priority, { u with Assembled = i }) ev
//            let primedFunc  (ev : Browser.Types.Event) = changeHandler (fun (u : Unit) i -> u.Priority, { u with Primed = i }) ev
//            let paintedFunc  (ev : Browser.Types.Event) = changeHandler (fun (u : Unit) i -> (u.Priority, { u with Painted = i })) ev
//            let basedFunc  (ev : Browser.Types.Event) = changeHandler (fun (u : Unit) i -> (u.Priority, { u with Based = i })) ev
//
//            let nc = Input.Color NoColor
//            let unitId = unit.Id.ToString("N") + "-"
//
//            let optionalColumns = [
//                if cs.AssemblyVisible then td [] [numericInput nc (unitId + "assembled") unit.Assembled assembledFunc ]
//                if cs.PrimedVisible then td [] [numericInput nc (unitId + "primed") unit.Primed primedFunc ]
//                if cs.PaintedVisible then td [] [numericInput nc (unitId + "painted") unit.Painted paintedFunc ]
//                if cs.BasedVisible then td [] [numericInput nc (unitId + "based") unit.Based basedFunc ]
//            ]
//            tr [] [
//                td [] [ Label.label [] [ str (string unit.Priority) ] ]
//                td [] [
//                    //todo: shrink this a little
//                    Input.input [ Input.ValueOrDefault unit.Name ]
//                ]
//                td [] [
//                    numericInput (Input.Color NoColor) (unit.Id.ToString() + "-models") unit.Models modelCountFunc
//                ]
//                yield! optionalColumns
//                td [] [ Delete.delete [ Delete.Size IsMedium; Delete.OnClick (fun _ -> RemoveErrorMessage |> dispatch ) ] [ ] ]
//            ]

        let unitRow2 (cs : ColumnSettings) dispatch (unit : Unit)  =
            let changeHandler transform (ev : Browser.Types.Event)  =
                let x = Parsing.parseIntOrZero ev.Value
                TryUpdateRow (transform unit x)
                |> dispatch

            let modelCountFunc (ev : Browser.Types.Event) = changeHandler (fun u x -> { u with Models = x }) ev
            let assembledFunc  (ev : Browser.Types.Event) = changeHandler (fun u x -> { u with Assembled = x }) ev
            let primedFunc  (ev : Browser.Types.Event) = changeHandler (fun u x -> { u with Primed = x }) ev
            let paintedFunc  (ev : Browser.Types.Event) = changeHandler (fun u x -> { u with Painted = x }) ev
            let basedFunc  (ev : Browser.Types.Event) = changeHandler (fun u x -> { u with Based = x }) ev

            let nc = Input.Color NoColor
            let unitId = unit.Id.ToString("N") + "-"

            let optionalColumns = [
                if cs.AssemblyVisible then td [] [numericInput nc (unitId + "assembled") unit.Assembled assembledFunc ]
                if cs.PrimedVisible then td [] [numericInput nc (unitId + "primed") unit.Primed primedFunc ]
                if cs.PaintedVisible then td [] [numericInput nc (unitId + "painted") unit.Painted paintedFunc ]
                if cs.BasedVisible then td [] [numericInput nc (unitId + "based") unit.Based basedFunc ]
            ]
            tr [] [
                td [] [ Label.label [] [ str (string unit.Priority) ] ]
                td [] [
                    //todo: shrink this a little
                    Input.input [ Input.ValueOrDefault unit.Name ]
                ]
                td [] [
                    numericInput (Input.Color NoColor) (unit.Id.ToString() + "-models") unit.Models modelCountFunc
                ]
                yield! optionalColumns
                td [] [ Delete.delete [
                    Delete.Size IsMedium
                    Delete.OnClick (fun _ -> (TryDeleteRow unit.Id) |> dispatch )
                    Delete.Modifiers [ Modifier.IsUnselectable ]
                ] [ ] ]
            ]

        let mapSaveError (model : Model) dispatch =
            let structure color header message =
                Message.message [ Message.Color color ] [
                    Message.header [] [
                        str header
                        Delete.delete [ Delete.OnClick (fun _ -> dispatch RemoveErrorMessage) ] [ ]
                    ]
                    Message.body [] [
                        str message
                    ]
                ]
            match model.ErrorMessage with
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
                    // blank column for unit priority / #
                    th [] [ ]
                    th [] [ str "Name" ]
                    th [] [ str "Models" ]
                    yield! optionalColumnHeaders
                    // add a blank header for the add/delete button column
                    th [] []
                ]

            let model = model
            let rows = model.Units |> ResizeArray.map (unitRow2 model.ColumnSettings dispatch)
            let table =
                [
                    yield tableHeaders
                    yield! rows
                ]
                |> Table.table [ Table.IsBordered; Table.IsStriped; Table.IsNarrow; Table.IsHoverable; Table.CustomClass "list-units-table" ]
            Section.section [] [
                if Option.isSome saveError then Option.get saveError
                //Heading.h3 [ Heading.IsSubtitle  ] [ Text.p [ Modifiers [ Modifier.TextAlignment (Screen.All, TextAlignment.Centered) ] ][ str "Add a new unit" ]]
                //inputNewUnit model.ColumnSettings model.PartialData dispatch
                hr []
                Columns.columns [ Columns.IsGap(Screen.All, Columns.Is1) ] [
                    Column.column [  ] [
                        table
                    ]
                    Column.column [ Column.Width(Screen.All, Column.IsNarrow) ] [ ]
                ]
            ]

    module ApiCalls =
        open Fable.Core.JS
        open Fable.Core
        open Fable.Core.JsInterop
        open Fetch
        open Thoth.Fetch

        let loadAllUnits (model : Model) =
            let promise() = Promises.loadUnitsForProject model.Config model.ProjectId
            let cmd = Cmd.OfPromise.either promise () LoadUnitsResponse LoadUnitsFailure
            model, cmd

        let tryAddNewUnit newUnit (model : Model) =
            //if model.PartialData.IsValid() then
                //let newUnit = Model.createUnitFromPartial model
            printfn "Unit to be added is %A" newUnit
            let promise = Promises.createUnit model.Config
            model, Cmd.OfPromise.either promise newUnit AddNewUnitResponse AddNewUnitFailure

        let tryUpdateUnit (u : Unit) model =
            printfn "Unit to be updated is %A" u
            let promise x = Promises.updateUnit model.Config x
            model, Cmd.OfPromise.either promise u UpdateRowResponse UpdateRowFailure

        let updateUnitPriorities (model : Model) =
            let priorities =
                model.Units
                |> ResizeArray.map (fun x -> { UnitPriority.UnitId = x.Id ; UnitPriority = x.Priority })
            printfn "saving new priorities of %A" priorities
            let promise priorities = Promises.updateUnitPriorites2 model.Config model.ProjectId priorities
            model, Cmd.OfAsync.either promise (ResizeArray.to_list priorities) UpdatePrioritiesResponse2 UpdatePrioritiesFailure

        let tryDeleteRow unitId model =
            let removed = model.Units |> ResizeArray.filter (fun x -> x.Id <> unitId)
            let model = { model with Units = removed }
            printfn "Removing unit with id %A" unitId
            let promise id = Promises.deleteUnit model.Config model.ProjectId id
            model, Cmd.OfPromise.attempt promise unitId DeleteRowFailure



    module ResponseHandlers =
        let private setErrorMsg e model =
            let msg = Promises.printFetchError e
            let mdl = Model.setErrorMessage msg model
            mdl, Cmd.none

        let setExceptionMsg e model =
            printfn "exn: %A" e
            { model with ErrorMessage = Some (ErrorMessage "Unknown error") }, Cmd.none

        let loadAllUnits (response) (model : Model) =
            match response with
            | Ok units ->
                model
                |> Model.removeErrorMessage
                |> Model.Updates.setUnitsFromList units
            | Error e -> setErrorMsg e model

        let addNewUnit (response) (model : Model) =
            match response with
            | Ok unit ->
                let mdl = model |> Model.addNewUnit unit |> Model.removeErrorMessage
                mdl, Cmd.ofMsg TryUpdatePriorities
            | Error e -> setErrorMsg e model

        let updateUnit (response) model =
            match response with
            | Ok unit ->
                let mdl = model |> Model.removeErrorMessage |> Model.replaceUnit unit
                mdl, Cmd.none
            | Error e ->
                setErrorMsg e model

//        let updatePriorities (response) (model) =
//            match response with
//            | Ok priorities ->
//                model |> Model.removeErrorMessage, Cmd.none
//            | Error e -> setErrorMsg e model

        let updatePriorities2 (response) (model) =
            match response with
            | Ok () ->
                model |> Model.removeErrorMessage, Cmd.none
            | Error e ->
                printfn "Error code %i" e
                model, Cmd.none

    let update (model : Model) (msg : Msg) =
        match msg with
        | UpdatedColumnSettings cs ->
            { model with ColumnSettings = cs }, Cmd.none
        | UpdatePartialData newPartial ->
            { model with PartialData = newPartial }, Cmd.none
        | RemoveErrorMessage ->
            { model with ErrorMessage = None }, Cmd.none
        | LoadUnitsForProject projectId ->
           let model = { model with ProjectId = projectId }
           ApiCalls.loadAllUnits model
        | LoadUnitsResponse response -> ResponseHandlers.loadAllUnits response model
        | LoadUnitsFailure e ->
            printfn "%A" e
            { model with ErrorMessage = Some (sprintf "Error loading: %A" e.Message |> ErrorMessage) }, Cmd.none
        | TryAddNewUnit unit ->
            ApiCalls.tryAddNewUnit unit model
        | AddNewUnitResponse response ->
            ResponseHandlers.addNewUnit response model
        | AddNewUnitFailure e -> ResponseHandlers.setExceptionMsg e model
        | TryUpdateRow unit -> ApiCalls.tryUpdateUnit unit model
        | UpdateRowResponse res -> ResponseHandlers.updateUnit res model
        | UpdateRowFailure e -> ResponseHandlers.setExceptionMsg e model
        | TryUpdatePriorities -> ApiCalls.updateUnitPriorities model
        //| UpdatePrioritiesResponse res -> ResponseHandlers.updatePriorities res model
        | UpdatePrioritiesResponse2 res ->
            ResponseHandlers.updatePriorities2 res model
        | UpdatePrioritiesFailure e -> ResponseHandlers.setExceptionMsg e model
        | TryDeleteRow unitId -> ApiCalls.tryDeleteRow unitId model
        | DeleteRowFailure e -> ResponseHandlers.setExceptionMsg e model




