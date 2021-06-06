namespace BoxToTabletop.Client

open Fable.React
open Fable.React.Props
open BoxToTabletop.Domain
open BoxToTabletop.Domain.Helpers
open BoxToTabletop.Domain.Types
open Browser
open Elmish
open Elmish.DragAndDrop
open System
open FSharpx.Collections
open Fulma

module UnitsList = 
  let dndConfig = {
    DragAndDropConfig.Empty() with
      DraggedElementStyles = Some [
        Opacity 0.8
        CSSProp.Cursor "grabbing"
        Position PositionOptions.Fixed
        Background "#33aaff"
      ]
      HoverPreviewElementStyles = Some [
        CSSProp.Opacity 0.2
      ]
  }

  let spinnerId = Guid.Parse("D9D1E6D9-ACB6-4100-9818-E91FA4996839")

  type AlertMessage =
  | InfoMessage of msg : string
  | ErrorMessage of msg : string

  type Model = {
    DragAndDrop : DragAndDropModel
    ProjectId : Guid
    ColumnSettings : ColumnSettings
    UnitMap : Map<string, Types.Unit>
    ErrorMessage : AlertMessage option
    Config : Config.T
  } with
    static member Init(config : Config.T, dndModel : DragAndDropModel) = {
      DragAndDrop = dndModel
      ProjectId = Guid.Empty
      ColumnSettings = ColumnSettings.Empty()
      UnitMap = Map.empty
      ErrorMessage = None
      Config = config
    }

  let replaceUnit (unit : Types.Unit) model =
    let unitIdStr = string unit.Id
    let m = model.UnitMap |> Map.add unitIdStr unit
    { model with UnitMap = m }

  /// These are raised here, outside the Message loop, and handled in the main Program
  type RaisedMsg =
  | DndStart of dnd : DragAndDropModel * draggedUnit : Guid
  | DndEnd
  | ClearErrorMessage of messageId : Guid
  | LiftErrorMessage of title : string * msg : string * messageId : Guid option


  /// These messages are raised externally and handled here
  type ExternalSourceMsg =
  | ProjectChange of proj : Project
  | ColumnSettingsChange of cs : ColumnSettings
  | AddNewUnit of newUnit : Unit
  | TransferUnitTo of unitId : Guid * projectId : Guid

  /// Messages that initiate an API call
  type ApiCallStartMsg = 
  | LoadUnitsForProject
  | DeleteUnit of unitId : Guid
  | UpdateUnit of unit : Unit
  | TransferUnit of unitId : Guid * newProjectId : Guid

  /// Messages that handle API call responses or errors
  type ApiCallsResponseMsg =
  | LoadUnitsResponse of response : Result<Unit list, string>
  | LoadUnitsFailure of exn
  | DeleteUnitFailure of exn
  | TransferUnitResponse of response: Result<Guid, int>
  | TransferUnitFailure of exn

  /// the main Messages for this component
  type Msg =
  | External of ExternalSourceMsg
  | DndMsg of DragAndDropMsg * Guid option
  | ApiCallStart of msg : ApiCallStartMsg
  | ApiCallResponse of msg : ApiCallsResponseMsg

  let createAddUnitMsg unit = AddNewUnit unit |> External
  let createProjectChangeMsg project = ProjectChange project |> External
  let createColumnChangeMsg cs = ColumnSettingsChange cs |> External

  module View =
    open Fable.FontAwesome

    let dndDispatch dispatch = fun m -> DndMsg m |> dispatch

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

    let drawRow model dispatch index (unit : Types.Unit) =
      let cs = model.ColumnSettings
      let changeHandler transform (ev : Browser.Types.Event) =
          let x = Parsing.parseIntOrZero ev.Value
          UpdateUnit (transform unit x)
          |> ApiCallStart |> dispatch

      let modelCountFunc (ev : Browser.Types.Event) = changeHandler (fun u x -> { u with Models = x }) ev
      let assembledFunc  (ev : Browser.Types.Event) = changeHandler (fun u x -> { u with Assembled = x }) ev
      let primedFunc  (ev : Browser.Types.Event) = changeHandler (fun u x -> { u with Primed = x }) ev
      let paintedFunc  (ev : Browser.Types.Event) = changeHandler (fun u x -> { u with Painted = x }) ev
      let basedFunc  (ev : Browser.Types.Event) = changeHandler (fun u x -> { u with Based = x }) ev
      let nc = Input.Color NoColor
      let unitIdBuilder str = unit.Id.ToString() + "-" + str
      let optionalColumns = [
          if cs.AssemblyVisible then td [] [numericInput nc (unitIdBuilder "assembled") unit.Assembled assembledFunc ]
          if cs.PrimedVisible then td [] [ numericInput nc (unitIdBuilder "primed") unit.Primed primedFunc ]
          if cs.PaintedVisible then td [] [ numericInput nc (unitIdBuilder "painted") unit.Painted paintedFunc ]
          if cs.BasedVisible then td [] [ numericInput nc (unitIdBuilder "based") unit.Based basedFunc ]
      ]

      let dndModel = model.DragAndDrop
      let rowId = unit.Id.ToString()
      let handleStyles = if dndModel.Moving.IsSome then [] else [ Cursor "grab" ]

      let content = 
        [
          td [] [
            Level.level [
              Level.Level.CustomClass "row-number"
            ] [
              DragHandle.dragHandle dndModel rowId (fun m -> DndMsg(m, Some unit.Id) |> dispatch) (
                ElementGenerator.Create (unitIdBuilder "handle") handleStyles [] [
                  Level.item [] [
                    Fa.i [ Fa.Solid.EllipsisV] []
                    Level.item [] [ Label.label [ Label.CustomClass "row-number-label" ] [ str (string index) ] ]
                  ]
                ])
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
                Delete.OnClick (fun _ -> (DeleteUnit unit.Id) |> ApiCallStart |> dispatch )
                Delete.Modifiers [ Modifier.IsUnselectable ]
            ] [ ]
          ]
        ]
      
      ElementGenerator.Create rowId [] [ Id rowId ] content
      |> ElementGenerator.setTag tr
      |> Draggable.draggable model.DragAndDrop dndConfig (fun m -> DndMsg(m, Some unit.Id) |> dispatch)

    let mapSaveError (model : Model) dispatch =
        let structure color header message =
            Message.message [ Message.Color color ] [
                Message.header [] [
                    str header
                    Delete.delete [ Delete.OnClick (fun _ -> () ) ] [] //dispatch RemoveErrorMessage) ] [ ]
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

    let view model dispatch =
      //let saveError = mapSaveError model dispatch
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
        model.DragAndDrop.ElementIds()
        |> List.concat
        |> List.mapi (fun index unitId ->
          let unit= model.UnitMap |> Map.find unitId
          drawRow model dispatch index unit
        )

      let table =
        [
          thead [] [tableHeaders]
          DropArea.fromDraggables tbody [] rows
        ]
        |> Table.table [ Table.IsBordered; Table.IsStriped; Table.IsNarrow; Table.IsHoverable; Table.CustomClass "list-units-table" ]
      div [] [
        Section.section [ Section.CustomClass "no-padding-section" ] [
                //if Option.isSome saveError then Option.get saveError
                hr []
                section [
                ] [
                    Columns.columns [ Columns.IsGap(Screen.All, Columns.Is1) ] [
                        Column.column [  ] [
                            table
                        ]
                        Column.column [ Column.Width(Screen.All, Column.IsNarrow) ] [ ]
                    ]
                ]
            ]
      ]

  type UpdateResponse = Core.UpdateResponse<Model, Msg, RaisedMsg>

  module ApiCalls =
    let loadAllUnits (model : Model) =
      let promise() = Promises.loadUnitsForProject model.Config model.ProjectId
      let cmd = Cmd.OfAsync.either promise () LoadUnitsResponse LoadUnitsFailure
      model, cmd


    let deleteUnit model unitId =
      let promise = Promises.deleteUnit model.Config model.ProjectId
      let cmd = Cmd.OfPromise.attempt promise unitId DeleteUnitFailure
      let dnd = DragAndDropModel.removeItem (string unitId) model.DragAndDrop
      { model with DragAndDrop = dnd }, cmd

    let transferUnit model unitId newProjectId = 
      let asyncCmd() = Promises.transferUnit model.Config unitId newProjectId
      let cmd = Cmd.OfAsync.either asyncCmd () TransferUnitResponse TransferUnitFailure
      model, cmd


  let handleApiCallStartMsg (model : Model) msg =
    match msg with
    | LoadUnitsForProject ->
      let mdl, cmd = ApiCalls.loadAllUnits model
      mdl, Cmd.map ApiCallResponse cmd
    | DeleteUnit unitId ->
      let mdl, cmd = ApiCalls.deleteUnit model unitId
      mdl, Cmd.map ApiCallResponse cmd
    | UpdateUnit(unit) ->
      let model = replaceUnit unit model
      model, Cmd.none
    | TransferUnit (unitId, newProjectId) ->
      let mdl, cmd = ApiCalls.transferUnit model unitId newProjectId
      mdl, Cmd.map ApiCallResponse cmd

  let handleApiCallResponseMsg (model : Model) msg =
    match msg with
    | LoadUnitsResponse (Ok units) ->
      printfn "loaded %i units" (List.length units)
      let m = units |> List.map (fun x -> (string x.Id), x)
      let dnd = m |> List.map (fst) |> DragAndDropModel.createWithItems
      { model with DragAndDrop = dnd; UnitMap = (Map.ofList m) }, Cmd.none, None
    | LoadUnitsResponse (Error e) ->
      printfn "Error from API call to load units for project '%A': %A" model.ProjectId e
      let errMsg = LiftErrorMessage ("Error loading units", "There was an error loading units for the selected project. Try re-selecting the project.", None)
      model, Cmd.none, Some errMsg
    | LoadUnitsFailure e ->
      printfn "Load units error: %A" e
      let errMsg = LiftErrorMessage ("Error loading units", "There was an error loading units for the selected project. Try re-selecting the project.", None)
      model, Cmd.none, Some errMsg
    | DeleteUnitFailure e ->
      printfn "Delete unit error: %A" e
      let errMsg = LiftErrorMessage ("Error deleting unit", "There was an error deleting the unit. Try reloading the project and, if the unit is listed, deleting it again.", None)
      model, Cmd.none, Some errMsg
    | TransferUnitResponse (Ok unitId) ->
      let m = model.UnitMap |> Map.remove (string unitId)
      let dnd = model.DragAndDrop |> DragAndDropModel.removeItem (string unitId)
      { model with UnitMap = m; DragAndDrop = dnd}, Cmd.none, None
    | TransferUnitResponse (Error msg) ->
      printfn "Unable to transfer unit: %A" msg
      let raised = LiftErrorMessage ("Error transferring unit", "There was an error transferring the unit. If the unit is still listed, try transferring it again.", None)
      model, Cmd.none, Some raised
    | TransferUnitFailure e ->
      Fable.Core.JS.console.error("Error transferring unit:", e)
      let raised = LiftErrorMessage ("Error transferring unit", "There was an error transferring the unit. If the unit is still listed, try transferring it again.", None)
      model, Cmd.none, Some raised


  let handleExternalMsg (model : Model) msg =
    match msg with
    | ProjectChange proj ->
      let cmd = ApiCallStartMsg.LoadUnitsForProject |> ApiCallStart |> Cmd.ofMsg
      UpdateResponse.basic { model with ProjectId = proj.Id; ColumnSettings = proj.ColumnSettings } cmd
    | ColumnSettingsChange cs ->
      UpdateResponse.basic { model with ColumnSettings = cs } Cmd.none
    | AddNewUnit newUnit ->
      let newUnit = if newUnit.Id = Guid.Empty then {newUnit with Id = Guid.NewGuid()} else newUnit
      let m = model.UnitMap |> Map.add (string newUnit.Id) newUnit
      let dnd = model.DragAndDrop |> DragAndDropModel.insertNewItemAtHead 0 (string newUnit.Id)
      UpdateResponse.basic { model with UnitMap = m; DragAndDrop = dnd} Cmd.none
    | TransferUnitTo(unitId, projectId) ->
      let cmd = TransferUnit (unitId, projectId) |> ApiCallStart |> Cmd.ofMsg
      UpdateResponse.basic model cmd

  let update (model : Model) msg =
    match msg with
    | External externalMsg -> handleExternalMsg model externalMsg
    | DndMsg (dndMsg, Some unitId) ->
      let dnd, cmd = dragAndDropUpdate dndMsg model.DragAndDrop 
      let raised = DndStart (dnd, unitId)
      let dndCmd = Cmd.map (fun m -> DndMsg(m, Some unitId)) cmd
      UpdateResponse.withRaised { model with DragAndDrop = dnd} dndCmd raised
    | DndMsg (dndMsg, None) ->
      let dnd, cmd = dragAndDropUpdate dndMsg model.DragAndDrop 
      let raised = DndStart (dnd, Guid.Empty)
      let dndCmd = Cmd.map (fun m -> DndMsg(m, None)) cmd
      UpdateResponse.withRaised { model with DragAndDrop = dnd} dndCmd raised
    | ApiCallStart apiMsg ->
      let spin = Core.SpinnerStart spinnerId
      let mdl, apiCmd = handleApiCallStartMsg model apiMsg
      UpdateResponse.withSpin mdl apiCmd spin
    | ApiCallResponse apiResponseMsg ->
      let spinner = Core.SpinnerEnd spinnerId
      let mdl, apiCmd, raised = handleApiCallResponseMsg model apiResponseMsg
      UpdateResponse.create mdl apiCmd (Some spinner) raised