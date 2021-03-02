namespace BoxToTabletop.Client

open BoxToTabletop.Domain
open BoxToTabletop.Domain.Helpers
open BoxToTabletop.Domain.Routes.Project
open BoxToTabletop.Domain.Types
open BoxToTabletop.ReactDND
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

    let dndConfig = {
        DragAndDrop.BeforeUpdate = (fun dragIndex dropIndex li -> li)
        DragAndDrop.Movement = DragAndDrop.Movement.Free
        DragAndDrop.Listen = DragAndDrop.Listen.OnDrag
        DragAndDrop.Operation = DragAndDrop.Operation.Rotate
    }

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

//    type DraggedRow = {
//        X : float
//        Y : float
//        UnitId : Guid
//    }

    type Model = {
        DragAndDrop : DragAndDrop.Model
        ProjectId : Guid
        PartialData : PartialData
        ColumnSettings : ColumnSettings
        Units : ResizeArray<Types.Unit>
        //DraggedRow : DraggedRow option
        ErrorMessage : AlertMessage option
        Config : Config.T

    } with
        static member Init(config : Config.T) = {
            DragAndDrop = None
            ProjectId = Guid.Empty
            PartialData = PartialData.Init()
            ColumnSettings = ColumnSettings.Empty()
            Units = ResizeArray()
            //DraggedRow = None
            ErrorMessage = None
            Config = config
        }

    module Model =

        module Units =
            let setProjectId projectId _unit = { _unit with Unit.ProjectId = projectId }

            let denseRank units =
                units |> ResizeArray.sortBy (fun x -> x.Priority)
                units |> ResizeArray.mapi (fun i x -> { x with Priority = i })


        let setErrorMessage msg model = { model with ErrorMessage = Some (ErrorMessage msg) }
        let setInfoMessage msg model = { model with ErrorMessage = Some (InfoMessage msg) }
        let removeErrorMessage model = { model with ErrorMessage = None }

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

    type ExternalMsg =
    | ProjectChange of proj : Project
    | ColumnSettingsChange of cs : ColumnSettings

    type Msg =
    | External of ExternalMsg
    | DNDMsg of DragAndDrop.Msg
    | UpdatePartialData of newPartial : PartialData
    | RemoveErrorMessage
    //| UpdatedColumnSettings of cols : ColumnSettings

    // todo: make this external and/or only call this internally.
    | LoadUnitsForProject of projectId : Guid
    //| LoadUnitsResponse of response : Result<Unit list, Thoth.Fetch.FetchError>
    | LoadUnitsResponse of response : Result<Unit list, string>
    | LoadUnitsFailure of exn
    | TryAddNewUnit of unit : Unit
    | AddNewUnitResponse of response : Result<Unit, Thoth.Fetch.FetchError>
    | AddNewUnitFailure of exn
    | UnitAddSuccess
    | TryUpdateRow of unit: Unit
    | UpdateRowResponse of Result<Unit, Thoth.Fetch.FetchError>
    | UpdateRowFailure of exn
    /// If a unit is added or moved, all unit priorities get updated.
    | TryUpdatePriorities
    | UpdatePrioritiesResponse of Result<UnitPriority list, Thoth.Fetch.FetchError>
    | UpdatePrioritiesResponse2 of Result<unit, int>
    | UpdatePrioritiesFailure of exn
    | TryDeleteRow of unitId : Guid
    | DeleteRowFailure of exn
//    | StartDragging of DraggedRow
//    | Dragging of DraggedRow
//    | EndDragging of DraggedRow

    module View =
        open Fable.React
        open Fable.React.Helpers
        //https://github.com/fable-compiler/fable-react/blob/master/src/Fable.React.Props.fs
        open Fable.React.Props
        //https://github.com/fable-compiler/fable-react/blob/master/src/Fable.React.Standard.fs
        open Fable.FontAwesome

        let DNDDispatch dispatch = fun (m : DragAndDrop.Msg) -> DNDMsg m |> dispatch

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



        let drawRow (cs : ColumnSettings) dispatch unit =
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
                if cs.AssemblyVisible then Box.box' [] [numericInput nc (unitId + "assembled") unit.Assembled assembledFunc ]
                if cs.PrimedVisible then Box.box' [] [numericInput nc (unitId + "primed") unit.Primed primedFunc ]
                if cs.PaintedVisible then Box.box' [] [numericInput nc (unitId + "painted") unit.Painted paintedFunc ]
                if cs.BasedVisible then Box.box' [] [numericInput nc (unitId + "based") unit.Based basedFunc ]
            ]

            printfn "in row draw for li"

//            let dragButton =
//                button [
//                    OnMouseDown (fun ev ->
//                        ev.preventDefault()
//                        ev.stopPropagation()
//                        match ev.button with
//                        | 0. ->
//                            printfn "starting drag"
//                            StartDragging  { X = ev.pageX; Y = ev.pageY; UnitId = unit.Id }
//                            |> dispatch
//                        | _ ->
//                            printfn "ev.button in mouse down is %A" ev.button
//                            ()
//                    )
//                    OnMouseUp (fun ev ->
//                        ev.preventDefault()
//                        ev.stopPropagation()
//                        EndDragging { X = ev.pageX; Y = ev.pageY; UnitId = unit.Id }
//                        |> dispatch
//                    )
//                    Class "button"
//                    Style [
//                        //ZIndex 2
//                        //Opacity 0.7
//                        //PointerEvents "none"
//                        Cursor "move"
//                    ]
//                ] [
//                    Level.level [
//                        Level.Level.CustomClass "row-number"
//                    ] [
//                        Level.item [] [ Fa.i [ Fa.Solid.EllipsisV ] [] ]
//                        Level.item [] [ Label.label [ Label.CustomClass "row-number-label"  ] [ str (string unit.Priority) ] ]
//                    ]
//                ] //end button

            let unitNameInput = Box.box' [] [
                Input.input [ Input.ValueOrDefault unit.Name ]
            ]

            //return value here
            li [] [
                Level.level [] [
                    //Level.item [] [ dragButton ]
                    Level.item [] [ unitNameInput ]
                    Box.box' [] [
                        numericInput (Input.Color NoColor) (unit.Id.ToString() + "-models") unit.Models modelCountFunc
                    ]
                    yield! optionalColumns
                    Box.box' [] [
                        Delete.delete [
                            Delete.Size IsMedium
                            Delete.OnClick (fun _ -> (TryDeleteRow unit.Id) |> dispatch )
                            Delete.Modifiers [ Modifier.IsUnselectable ]
                        ] []
                    ]
                ]
            ]

        let ghostView (dnd : DragAndDrop.Model) (items : 'a list) =
            let maybeDragItem =
                dnd |> Option.map(fun { DragIndex = dragIndex } -> items.[dragIndex])
            match maybeDragItem with
            | Some item ->
                tr [ yield! DragAndDrop.ghostStyles dndConfig.Movement dnd ] [ item ]
            | None ->
                br []

        let unitRow (model : Model) dispatch index (unit : Unit) =
            let changeHandler transform (ev : Browser.Types.Event) =
                let x = Parsing.parseIntOrZero ev.Value
                TryUpdateRow (transform unit x)
                |> dispatch

            let modelCountFunc (ev : Browser.Types.Event) = changeHandler (fun u x -> { u with Models = x }) ev
            let assembledFunc  (ev : Browser.Types.Event) = changeHandler (fun u x -> { u with Assembled = x }) ev
            let primedFunc  (ev : Browser.Types.Event) = changeHandler (fun u x -> { u with Primed = x }) ev
            let paintedFunc  (ev : Browser.Types.Event) = changeHandler (fun u x -> { u with Painted = x }) ev
            let basedFunc  (ev : Browser.Types.Event) = changeHandler (fun u x -> { u with Based = x }) ev

            let nc = Input.Color NoColor

            let cs = model.ColumnSettings
            let unitId = unit.Id.ToString("N")
            let id : IHTMLProp = HTMLAttr.Id unitId :> IHTMLProp

            let optionalColumns = [
                if cs.AssemblyVisible then td [] [numericInput nc (unitId + "assembled") unit.Assembled assembledFunc ]
                if cs.PrimedVisible then td [] [numericInput nc (unitId + "primed") unit.Primed primedFunc ]
                if cs.PaintedVisible then td [] [numericInput nc (unitId + "painted") unit.Painted paintedFunc ]
                if cs.BasedVisible then td [] [numericInput nc (unitId + "based") unit.Based basedFunc ]
            ]

            printfn "in row draw"

            let drawRow props = tr [] [
                td props
//                    button [
//                        OnMouseDown (fun ev ->
//                            ev.preventDefault()
//                            ev.stopPropagation()
//                            match ev.button with
//                            | 0. ->
//                                printfn "starting drag"
//                                StartDragging  { X = ev.pageX; Y = ev.pageY; UnitId = unit.Id }
//                                |> dispatch
//                            | _ ->
//                                printfn "ev.button in mouse down is %A" ev.button
//                                ()
//                        )
//                        OnMouseUp (fun ev ->
//                            ev.preventDefault()
//                            ev.stopPropagation()
//                            EndDragging { X = ev.pageX; Y = ev.pageY; UnitId = unit.Id }
//                            |> dispatch
//                        )
//                        Class "button"
//                        Style [
//                            //ZIndex 2
//                            //Opacity 0.7
//                            //PointerEvents "none"
//                            Cursor "move"
//                        ]
//                    ] [
//                        Level.level [
//                            Level.Level.CustomClass "row-number"
//                        ] [
//                            Level.item [] [ Fa.i [ Fa.Solid.EllipsisV ] [] ]
//                            Level.item [] [ Label.label [ Label.CustomClass "row-number-label"  ] [ str (string unit.Priority) ] ]
//                        ]
//                    ]
//                ]
                 [
                    Level.level [
                        Level.Level.CustomClass "row-number"
                    ] [
                        Level.item [] [ Fa.i [ Fa.Solid.EllipsisV ] [] ]
                        Level.item [] [ Label.label [ Label.CustomClass "row-number-label" ] [ str (string unit.Priority) ] ]
                    ]
                ]
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

            let placeholderRow() =
                tr [] [
                    td [] [
                        Level.level [
                            Level.Level.CustomClass "row-number"
                        ] [
                            Level.item [] [ Fa.i [ Fa.Solid.EllipsisV ] [] ]
                            Level.item [] [ Label.label [ Label.CustomClass "row-number-label" ] [ str (string index) ] ]
                        ]
                    ]
                    td [] [
                        //todo: shrink this a little
                        Input.input [ Input.ValueOrDefault "------" ]
                    ]
                    td [] [
                        numericInput (Input.Color NoColor) ("placeholder-models") 0 (fun ev -> ())
                    ]
                    yield! optionalColumns
                    td [] [ Delete.delete [
                        Delete.Size IsMedium
                        Delete.OnClick (fun _ -> ())
                        Delete.Modifiers [ Modifier.IsUnselectable ]
                    ] [ ] ]
                ]

            match model.DragAndDrop with
            | Some dragState ->
                if dragState.DragIndex <> index then
                    let dropEvents = DragAndDrop.dropEvents (fun x -> x) (DNDDispatch dispatch) index unitId
                    let props = id :: dropEvents
                    drawRow props
                else placeholderRow()
            | None ->
                let dragEvents = DragAndDrop.dragEvents (fun x -> x) (DNDDispatch dispatch) index unitId
                let id : IHTMLProp = HTMLAttr.Id unitId :> IHTMLProp
                drawRow (id :: dragEvents)

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

            let rows =
                model.Units
                |> ResizeArray.mapi (fun i u -> unitRow model dispatch i u)
                //|> ResizeArray.map (fun x -> tr [] x)
//            let draggableRows =
//                model.Units
//                |> ResizeArray.map( fun unit ->
//                    draggableRow
//                        {|
//                           unitRow = {
//                               Settings = model.ColumnSettings
//                               Unit = unit
//                               Dispatch = dispatch
//                           }
//                        |}
//                )
            let table =
                [
                    yield tableHeaders
                    yield! rows
                ]
                |> Table.table [ Table.IsBordered; Table.IsStriped; Table.IsNarrow; Table.IsHoverable; Table.CustomClass "list-units-table" ]

            let dndListeners = DragAndDrop.mouseListener (DNDDispatch dispatch) model.DragAndDrop

            Section.section [ Section.CustomClass "no-padding-section"  ] [
                if Option.isSome saveError then Option.get saveError
                hr []
//                div [] [
//                    dndProvider [ DndProviderProps.Backend html5Backend ] [
//                        yield! draggableRows
//                    ]
//                ]
                section [
                    yield! dndListeners
                ] [
                    Columns.columns [ Columns.IsGap(Screen.All, Columns.Is1) ] [
                        Column.column [  ] [
                            table
                        ]
                        Column.column [ Column.Width(Screen.All, Column.IsNarrow) ] [ ]
                    ]
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
            let cmd = Cmd.OfAsync.either promise () LoadUnitsResponse LoadUnitsFailure
            model, cmd

        let tryAddNewUnit newUnit (model : Model) =
            let promise = Promises.createUnit model.Config
            model, Cmd.OfPromise.either promise newUnit AddNewUnitResponse AddNewUnitFailure

        let tryUpdateUnit (u : Unit) model =
            let promise x = Promises.updateUnit model.Config x
            model, Cmd.OfPromise.either promise u UpdateRowResponse UpdateRowFailure

        let updateUnitPriorities (model : Model) =
            let units =
                model.Units
                |> Model.Units.denseRank
            let model = { model with Units = units }
            let priorities = units |> ResizeArray.map (fun x -> { UnitPriority.UnitId = x.Id ; UnitPriority = x.Priority })
            let promise priorities = Promises.updateUnitPriorities2 model.Config model.ProjectId priorities
            model, Cmd.OfAsync.either promise (ResizeArray.to_list priorities) UpdatePrioritiesResponse2 UpdatePrioritiesFailure

        let tryDeleteRow unitId model =
            let removed = model.Units |> ResizeArray.filter (fun x -> x.Id <> unitId) |> Model.Units.denseRank
            let model = { model with Units = removed }
            let promise id = Promises.deleteUnit model.Config model.ProjectId id
            model, Cmd.OfPromise.attempt promise unitId DeleteRowFailure

    module ResponseHandlers =
        let private setErrorMsg e model =
            let msg = Promises.printFetchError e
            let mdl = Model.setErrorMessage msg model
            mdl, Cmd.none

        let setErrorMessageFromStr str model =
            let mdl = Model.setErrorMessage str model
            mdl, Cmd.none

        let setExceptionMsg e model =
            { model with ErrorMessage = Some (ErrorMessage "Unknown error") }, Cmd.none

        let loadAllUnits (response) (model : Model) =
            match response with
            | Ok units ->
                model
                |> Model.removeErrorMessage
                |> Model.Updates.setUnitsFromList units
            //| Error e -> setErrorMsg e model
            | Error e -> setErrorMessageFromStr e model


        let addNewUnit (response) (model : Model) =
            match response with
            | Ok unit ->
                let mdl = model |> Model.addNewUnit unit |> Model.removeErrorMessage
                //mdl, Cmd.ofMsg TryUpdatePriorities
                mdl, Cmd.batch [
                    Cmd.ofMsg TryUpdatePriorities
                    Cmd.ofMsg UnitAddSuccess
                ]
            | Error e -> setErrorMsg e model

        let updateUnit (response) model =
            match response with
            | Ok unit ->
                let mdl = model |> Model.removeErrorMessage |> Model.replaceUnit unit
                mdl, Cmd.none
            | Error e ->
                setErrorMsg e model

        let updatePriorities (response) (model) =
            match response with
            | Ok priorities ->
                // by the time we get here, the model unit priorities have already been updated
                model |> Model.removeErrorMessage, Cmd.none
            | Error e -> setErrorMsg e model

        let updatePriorities2 (response) (model) =
            match response with
            | Ok () ->
                model |> Model.removeErrorMessage, Cmd.none
            | Error e ->
                printfn "Error code %i" e
                model, Cmd.none

    let update (model : Model) (msg : Msg) =
        match msg with
        | Msg.DNDMsg dragMsg ->
            let dnd, sortedItems = DragAndDrop.update dndConfig dragMsg model.DragAndDrop (model.Units.ToArray() |> Array.toList)
            let cmd = DragAndDrop.commands (fun x -> x) dnd |> Cmd.map Msg.DNDMsg
            let ra = ResizeArray()
            sortedItems |> List.iter (fun x -> ra.Add x)
            { model with DragAndDrop = dnd; Units = ra }, cmd
        | External (ExternalMsg.ProjectChange proj) ->
            { model with ProjectId = proj.Id; ColumnSettings = proj.ColumnSettings }, Cmd.none
        | External (ExternalMsg.ColumnSettingsChange cs) ->
            { model with ColumnSettings = cs }, Cmd.none
//        | UpdatedColumnSettings cs ->
//            { model with ColumnSettings = cs }, Cmd.none
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
        | UnitAddSuccess -> model, Cmd.none
        | TryUpdateRow unit -> ApiCalls.tryUpdateUnit unit model
        | UpdateRowResponse res -> ResponseHandlers.updateUnit res model
        | UpdateRowFailure e -> ResponseHandlers.setExceptionMsg e model
        | TryUpdatePriorities -> ApiCalls.updateUnitPriorities model
        | UpdatePrioritiesResponse res -> ResponseHandlers.updatePriorities res model
        | UpdatePrioritiesResponse2 res ->
            ResponseHandlers.updatePriorities2 res model
        | UpdatePrioritiesFailure e -> ResponseHandlers.setExceptionMsg e model
        | TryDeleteRow unitId -> ApiCalls.tryDeleteRow unitId model
        | DeleteRowFailure e -> ResponseHandlers.setExceptionMsg e model
//        | StartDragging draggedRow ->
//            printfn "started dragging"
//            { model with DraggedRow = Some draggedRow }, Cmd.none
//        | Dragging draggedRow ->
//            printfn "currently dragging"
//            model, Cmd.none
//        | EndDragging draggedRow ->
//            printfn "ended dragging"
//            { model with DraggedRow = None }, Cmd.none




