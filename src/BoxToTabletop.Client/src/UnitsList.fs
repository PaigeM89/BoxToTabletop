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
open Fulma
open Thoth.Elmish

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

  type UnitChange = Types.Unit
  type PrioritiesChange = UnitPriority list

  type ProjectChangeStatus = 
  | Unloading of newProject : Project
  | NoPendingChange

  type Model = {
    DragAndDrop : DragAndDropModel
    ProjectId : Guid
    ColumnSettings : ColumnSettings
    UnitMap : Map<string, Types.Unit>
    ErrorMessage : AlertMessage option
    Config : Config.T
    UnitChanges : Map<Guid, UnitChange>
    PriorityChanges : PrioritiesChange

    Debouncer : Debouncer.State
    ProjectChangeStatus : ProjectChangeStatus
  } with
    static member Init(config : Config.T, dndModel : DragAndDropModel) = {
      DragAndDrop = dndModel
      ProjectId = Guid.Empty
      ColumnSettings = ColumnSettings.Empty()
      UnitMap = Map.empty
      ErrorMessage = None
      Config = config
      UnitChanges = Map.empty
      PriorityChanges = []

      Debouncer = Debouncer.create()
      ProjectChangeStatus = NoPendingChange
    }

  let replaceUnit (unit : Types.Unit) model =
    let unitIdStr = string unit.Id
    let m = model.UnitMap |> Map.add unitIdStr unit
    { model with UnitMap = m }

  let updatePriorities model =
    let items = model.DragAndDrop.ElementIds() |> List.tryHead |> Option.defaultValue []
    let priorities =
      items
      |> List.mapi (fun index item ->
        match Map.tryFind item model.UnitMap with
        | Some x -> UnitPriority.Create x.Id index |> Some
        | None -> None
      )
      |> List.choose id
    { model with PriorityChanges = priorities }

  let setUnitChange (unit : UnitChange) model =
    let changesMap = model.UnitChanges |> Map.add unit.Id  unit
    { model with UnitChanges = changesMap } |> replaceUnit unit

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
  // | BeginProjectChange of newProject : Project

  /// Messages that handle API call responses or errors
  type ApiCallsResponseMsg =
  | LoadUnitsResponse of response : Result<Unit list, string>
  | LoadUnitsFailure of exn
  | DeleteUnitFailure of exn
  | TransferUnitResponse of response: Result<Guid, int>
  | TransferUnitFailure of exn
  | UpdateUnitSuccess of response : Result<Unit, Thoth.Fetch.FetchError>
  | UpdateUnitFailure of exn
  | UpdatePrioritiesSuccess of response: Result<int, Thoth.Fetch.FetchError>
  | UpdatePrioritiesFailure of exn
  | NoUnitsToUpdate

  /// Saving changes has to be done in a specific order, so these messages are invoked one after another
  /// If the user is changing projects, the next project Id is passed along these messages; if this is aregular save,
  /// these values are None.
  type SaveChangesMsg =
  | SaveUnitChanges of nextProjectId : Guid option
  | SaveUnitPriorities of nextProjectId : Guid option

  /// the main Messages for this component
  type Msg =
  | External of ExternalSourceMsg
  | DndMsg of DragAndDropMsg * Guid option
  | ApiCallStart of msg : ApiCallStartMsg
  | ApiCallResponse of msg : ApiCallsResponseMsg
  | AddUnitChange of change : UnitChange
  | ScrapePriorities
  | DebouncerSelfMsg of Debouncer.SelfMessage<Msg>
  | DispatchChanges
  | Saving of SaveChangesMsg
  | UnloadCompleted

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
            // Input.CustomClass "numeric-input-width"
        ]

    let drawRow model dispatch index (unit : Types.Unit) =
      let cs = model.ColumnSettings
      let changeHandler transform (ev : Browser.Types.Event) =
          let x = Parsing.parseIntOrZero ev.Value
          let unit = transform unit x
          unit |> AddUnitChange |> dispatch

      let modelCountFunc (ev : Browser.Types.Event) = changeHandler (fun u x -> { u with Models = x }) ev
      let assembledFunc  (ev : Browser.Types.Event) = changeHandler (fun u x -> { u with Assembled = x }) ev
      let primedFunc  (ev : Browser.Types.Event) = changeHandler (fun u x -> { u with Primed = x }) ev
      let paintedFunc  (ev : Browser.Types.Event) = changeHandler (fun u x -> { u with Painted = x }) ev
      let basedFunc  (ev : Browser.Types.Event) = changeHandler (fun u x -> { u with Based = x }) ev
      let powerFunc  (ev : Browser.Types.Event) = changeHandler (fun u x -> { u with Power = x }) ev
      let pointsFunc  (ev : Browser.Types.Event) = changeHandler (fun u x -> { u with Points = x }) ev
      let nc = Input.Color NoColor
      let unitIdBuilder str = unit.Id.ToString() + "-" + str
      let optionalColumns = [
          if cs.AssemblyVisible then td [] [numericInput nc (unitIdBuilder "assembled") unit.Assembled assembledFunc ]
          if cs.PrimedVisible then td [] [ numericInput nc (unitIdBuilder "primed") unit.Primed primedFunc ]
          if cs.PaintedVisible then td [] [ numericInput nc (unitIdBuilder "painted") unit.Painted paintedFunc ]
          if cs.BasedVisible then td [] [ numericInput nc (unitIdBuilder "based") unit.Based basedFunc ]
          if cs.PowerVisible then td [] [ numericInput nc (unitIdBuilder "power") unit.Power powerFunc ]
          if cs.PointsVisible then td [] [ numericInput nc (unitIdBuilder "points") unit.Points pointsFunc ]
      ]

      let dndModel = model.DragAndDrop
      let rowId = unit.Id.ToString()
      let handleStyles = if dndModel.Moving.IsSome then [] else [ Cursor "grab" ]

      let nameChangeHandler (ev : Browser.Types.Event) =
        { unit with Name = ev.Value } |> AddUnitChange |> dispatch

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
              Input.input [ Input.ValueOrDefault unit.Name; Input.OnChange nameChangeHandler; Input.CustomClass "table-unit-name" ]
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
          let unit = model.UnitMap |> Map.find unitId
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

    let updateUnits (model : Model) =
      let units = model.UnitChanges |> Map.toList |> List.map snd
      let commands = 
        [ for unit in units do
            let promise unitArg = Promises.updateUnit model.Config unitArg
            Cmd.OfPromise.either promise unit UpdateUnitSuccess UpdateUnitFailure
        ]
      if List.length commands > 0 then
        commands |> Cmd.batch
      else
        printfn "no unit changes to dispatch, returning empty command"
        Cmd.ofMsg NoUnitsToUpdate

    let updatePriorities (model : Model) =
      let promise () = Promises.updateUnitPriorities model.Config model.ProjectId model.PriorityChanges
      let cmd = Cmd.OfPromise.either promise () UpdatePrioritiesSuccess UpdatePrioritiesFailure
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
      // printfn "loaded %i units" (List.length units)
      let m = units |> List.map (fun x -> (string x.Id), x)
      let dnd = m |> List.map (fst) |> DragAndDropModel.createWithItems
      let model = { model with DragAndDrop = dnd; UnitMap = (Map.ofList m); ProjectChangeStatus = NoPendingChange }
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
    | UpdateUnitSuccess( Ok unit ) ->
      let unitMap = model.UnitMap |> Map.add (string unit.Id) unit
      let changeMap = model.UnitChanges |> Map.remove unit.Id
      let model = { model with UnitMap = unitMap; UnitChanges = changeMap }
      match model.ProjectChangeStatus with
      | NoPendingChange -> model, Cmd.none, None
      | Unloading proj ->
        if model.UnitChanges = Map.empty then
          {model with ProjectChangeStatus = NoPendingChange; ProjectId = proj.Id }, Cmd.ofMsg (LoadUnitsForProject |> ApiCallStart), None
        else
          model, Cmd.none, None
    | UpdateUnitSuccess( Error fetchError ) ->
      printfn "Fetch error when updating unit: %A" fetchError
      let raised = LiftErrorMessage ("Error Saving unit", "There was an error saving one or more units.", None)
      model, Cmd.none, Some raised
    | UpdateUnitFailure e ->
      printfn "Error updating unit: %A" e
      model, Cmd.none, None
    | UpdatePrioritiesSuccess(Ok priorities) ->
      { model with PriorityChanges = []}, Cmd.none, None
    | UpdatePrioritiesSuccess (Error e) ->
      printfn "Thoth error updating unit priorities: %A" e
      let raised = LiftErrorMessage ("Error Saving unit", "There was an error updating unit ordering.", None)
      model, Cmd.none, Some(raised)
    | UpdatePrioritiesFailure e->
      printfn "Error updating unit priorities: %A" e
      model, Cmd.none, None
    | NoUnitsToUpdate ->
      match model.ProjectChangeStatus with
      | NoPendingChange -> model, Cmd.none, None
      | Unloading proj ->
        if model.UnitChanges = Map.empty then
          {model with ProjectChangeStatus = NoPendingChange; ProjectId = proj.Id }, Cmd.ofMsg (LoadUnitsForProject |> ApiCallStart), None
        else
          model, Cmd.none, None


  let handleExternalMsg (model : Model) msg =
    match msg with
    | ProjectChange proj ->
      let cmd = Cmd.ofMsg DispatchChanges
      let model = { model with ProjectChangeStatus = Unloading proj }
      UpdateResponse.basic { model with ProjectId = proj.Id; ColumnSettings = proj.ColumnSettings } cmd
    | ColumnSettingsChange cs ->
      UpdateResponse.basic { model with ColumnSettings = cs } Cmd.none
    | AddNewUnit newUnit ->
      let newUnit = if newUnit.Id = Guid.Empty then {newUnit with Id = Guid.NewGuid()} else newUnit
      let m = model.UnitMap |> Map.add (string newUnit.Id) newUnit
      let dnd = model.DragAndDrop |> DragAndDropModel.insertNewItemAtHead 0 (string newUnit.Id)
      let cmd = AddUnitChange newUnit |> Cmd.ofMsg
      UpdateResponse.basic { model with UnitMap = m; DragAndDrop = dnd} cmd
    | TransferUnitTo(unitId, projectId) ->
      let cmd = TransferUnit (unitId, projectId) |> ApiCallStart |> Cmd.ofMsg
      UpdateResponse.basic model cmd

  let handleSavingMsg (model : Model) msg =
    match msg with
    | SaveUnitChanges (nextProjectId) ->
      let cmd = ApiCalls.updateUnits model |> Cmd.map ApiCallResponse
      let spinner = Core.SpinnerStart spinnerId
      UpdateResponse.withSpin model cmd spinner
    | SaveUnitPriorities (nextProjectId) ->
      let model, cmd = ApiCalls.updatePriorities model
      let spinner = Core.SpinnerStart spinnerId
      UpdateResponse.withSpin model (cmd |> Cmd.map ApiCallResponse) spinner

  let update (model : Model) msg =
    match msg with
    | DebouncerSelfMsg debouncerMsg ->
      let (debouncerModel, debouncerCmd) = Debouncer.update debouncerMsg model.Debouncer
      UpdateResponse.basic { model with Debouncer = debouncerModel} debouncerCmd
    | External externalMsg -> handleExternalMsg model externalMsg
    | DndMsg (DragEnd, unitIdOpt) ->
      let dnd, cmd = dragAndDropUpdate DragEnd model.DragAndDrop
      let raised = DndEnd
      let dndCmd = Cmd.map (fun m -> DndMsg(m, unitIdOpt)) cmd
      let prioritiesCmd = ScrapePriorities |> Cmd.ofMsg
      let cmd = [ dndCmd; prioritiesCmd ] |> Cmd.batch 
      let mdl = { model with DragAndDrop = dnd } |> updatePriorities
      UpdateResponse.withRaised mdl cmd raised
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
    | AddUnitChange unit ->
      let (debouncerModel, debouncerCmd) =
        model.Debouncer
        |> Debouncer.bounce (TimeSpan.FromSeconds 5.) "unit_changes" DispatchChanges
      
      let mdl = setUnitChange unit model
      let mdl = { mdl with Debouncer = debouncerModel }

      UpdateResponse.basic mdl (Cmd.map DebouncerSelfMsg debouncerCmd)
    | ScrapePriorities ->
      printfn "scraping unit priorities"
      let (debouncerModel, debouncerCmd) =
        model.Debouncer
        |> Debouncer.bounce (TimeSpan.FromSeconds 20.) "unit_changes" DispatchChanges
      let mdl = { model with Debouncer = debouncerModel}
      UpdateResponse.basic mdl (Cmd.map DebouncerSelfMsg debouncerCmd)
    | DispatchChanges ->
      printfn "Dispatching changes"
      let spin = Core.SpinnerStart spinnerId
      // let cmd = ApiCalls.updateUnits model |> Cmd.map ApiCallResponse
      let cmd1 = SaveChangesMsg.SaveUnitChanges None |> Saving |> Cmd.ofMsg
      let cmd2 = SaveChangesMsg.SaveUnitPriorities None |> Saving |> Cmd.ofMsg
      let cmd = [ cmd1; cmd2 ] |> Cmd.batch 
      UpdateResponse.withSpin model cmd spin
    | UnloadCompleted ->
      match model.ProjectChangeStatus with
      | NoPendingChange -> UpdateResponse.basic model Cmd.none
      | Unloading(newProject) ->
        let model = { model with ProjectId = newProject.Id; ProjectChangeStatus = NoPendingChange }
        let cmd = ApiCallStartMsg.LoadUnitsForProject |> ApiCallStart |> Cmd.ofMsg
        let spin = Core.SpinnerStart spinnerId
        UpdateResponse.withSpin model cmd spin
    | Saving saveMsg -> handleSavingMsg model saveMsg

