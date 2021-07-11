namespace BoxToTabletop.Client

open System
open BoxToTabletop.Domain.Types

module AddUnit =

    type PartialData = {
        UnitName : string
        ModelCount : int
        AssembledCount : int
        PrimedCount : int
        PaintedCount : int
        BasedCount : int
        Power : int
        Points : int
        ShowError : bool
    } with
        static member Init() = {
            UnitName = ""
            ModelCount = 0
            AssembledCount = 0
            PrimedCount = 0
            PaintedCount = 0
            BasedCount = 0
            Power = 0
            Points = 0
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
                Power = this.Power
                Points = this.Points
        }

    type Model = {
        ProjectId : Guid
        PartialData : PartialData
        ColumnSettings : ColumnSettings
        Config : Config.T
        IsCollapsed : bool
    } with
        static member Init config = {
            ProjectId = Guid.Empty
            PartialData = PartialData.Init()
            ColumnSettings = ColumnSettings.Empty()
            Config = config
            IsCollapsed = false
        }

        member this.SetConfig config = { this with Config = config }

    module Model =
        let toggleShowErrors value model =
            { model with PartialData = { model.PartialData with ShowError = value } }

        let clearInput model =
            { model with PartialData = PartialData.Init() }

        module Unit =
            let setProjectId projId unit = { unit with Unit.ProjectId = projId }

    /// "Messages" outside the core message loop that are exclusively handled by the parent components
    type RaisedMsg =
    | NewUnitAdded of unit : Unit
    
    /// Messages initiated externally and handled inside this component
    type ExternalMsg =
    | ColumnSettingsChange of cs : ColumnSettings
    | ProjectColumnChange of col : ProjectColumn
    /// Changes the project Id and the column settings
    | ProjectChange of project : Project

    type Msg =
    // | UpdateColumnSettings of cs : ColumnSettings
    | UpdatePartialData of newPartial : PartialData
    /// Called when 'Add' is clicked. Does not contain a unit, as the input has not been verified.
    | TryAddNewUnit
    | External of ExternalMsg
    | CollapseAddUnit
    | ExpandAddUnit

    let createColumnSettingsChangeMsg cs = ColumnSettingsChange cs |> External
    let createProjectColumnChangeMsg col = ProjectColumnChange col |> External
    let createProjectChangeMsg proj = ProjectChange proj |> External

    module View =
        open Fable.React
        open Fable.React.Props
        open Fulma
        open Browser.Types
        open BoxToTabletop.Domain.Helpers
        open Fable.FontAwesome
        open Extensions.CreativeBulma

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
                // Input.CustomClass "numeric-input-width"
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
            ] |> FSharp.Collections.List.singleton |> addUnitWrapper []

        let newUnitNumericInput inputColor name (dv : int) action =
            addUnitWrapper [] [
                Level.heading [] [ str name ]
                Field.div [ Field.HasAddonsRight] [
                    Level.item [ Level.Item.CustomClass "no-flex-shrink" ] [
                        numericInput inputColor name dv action
                    ]
                ]
            ]

        let expandedView model dispatch =
            let partial = model.PartialData
            let cs = model.ColumnSettings
            let func (ev : Browser.Types.Event) transform =
                let c = Parsing.parseIntOrZero ev.Value
                UpdatePartialData (transform partial c)
                |> dispatch
            let inputColor =
                if partial.ShowError then IsDanger else NoColor
                |> Input.Color
            //Section.section [ Section.CustomClass "no-padding-section"  ] [
            div [] [
                Heading.h3 [ Heading.IsSubtitle  ] [ Text.p [ Modifiers [ Modifier.TextAlignment (Screen.All, TextAlignment.Centered) ] ][ str "Add a new unit" ]]
                Level.level [ ] [
                    unitNameInput inputColor partial dispatch
                    newUnitNumericInput inputColor "Models" partial.ModelCount (fun ev -> func ev (fun p c -> { p with ModelCount = c }))
                    if cs.AssemblyVisible then newUnitNumericInput inputColor "Assembled" partial.AssembledCount (fun ev -> func ev (fun p c -> { p with AssembledCount = c }))
                    if cs.PrimedVisible then newUnitNumericInput inputColor "Primed" partial.PrimedCount (fun ev -> func ev (fun p c -> { p with PrimedCount = c }))
                    if cs.PaintedVisible then newUnitNumericInput inputColor "Painted" partial.PaintedCount (fun ev -> func ev (fun p c -> { p with PaintedCount = c }))
                    if cs.BasedVisible then newUnitNumericInput inputColor "Based" partial.BasedCount (fun ev -> func ev (fun p c -> { p with BasedCount = c }))
                    if cs.PowerVisible then newUnitNumericInput inputColor "Power" partial.Power (fun ev -> func ev (fun p c -> { p with Power = c }))
                    if cs.PointsVisible then newUnitNumericInput inputColor "Points" partial.Points (fun ev -> func ev (fun p c -> { p with Points = c }))
                    addUnitWrapper [  CustomClass "add-unit-button-box" ] [
                        Button.Input.submit [
                            Button.Props [ Value "Add" ]
                            Button.Color IsSuccess
                            Button.OnClick (fun _ -> TryAddNewUnit |> dispatch)
                        ]
                    ]
                ]
                Extensions.CreativeBulma.Divider.divider [
                    Divider.DividerOption.Color IsInfo
                ] [ 
                    Button.button [
                        Button.Color Color.IsInfo
                        Button.OnClick(fun _ -> CollapseAddUnit |> dispatch)
                    ] [
                        Fa.i [ Fa.Solid.AngleDoubleUp ] [] 
                    ]
                ]
            ]

        let collapsedView model dispatch = 
            div [] [
                Extensions.CreativeBulma.Divider.divider [
                    Divider.DividerOption.Color IsInfo
                ] [ 
                    Button.button [
                        Button.Color Color.IsInfo
                        Button.OnClick(fun _ -> ExpandAddUnit |> dispatch)
                    ] [
                        Fa.i [ Fa.Solid.AngleDoubleDown ] [] 
                    ]
                ]
            ]

        let view model dispatch =
            if model.IsCollapsed then
                collapsedView model dispatch
            else
                expandedView model dispatch

    open Elmish

    type UpdateResponse = Core.UpdateResponse<Model, Msg, RaisedMsg>

    let handleExternalMsg (model : Model) (msg : ExternalMsg) =
        match msg with
        | ColumnSettingsChange cs ->
            { model with ColumnSettings = cs }, Cmd.none
        | ProjectChange project ->
            { model with ProjectId = project.Id; ColumnSettings = project.ColumnSettings }, Cmd.none
        | ProjectColumnChange(col) ->
            printfn "Add Unit handling project column change: %A" col
            model, Cmd.none

    let update model msg =
        match msg with
        | External ext ->
            let mdl, cmd = handleExternalMsg model ext
            UpdateResponse.basic mdl cmd
        | UpdatePartialData np ->
            let mdl = { model with PartialData = np }
            UpdateResponse.basic mdl Cmd.none
        | TryAddNewUnit ->
            if model.PartialData.IsValid() then
                let unit = model.PartialData.ToUnit() |> Model.Unit.setProjectId model.ProjectId
                let raised = NewUnitAdded unit
                let mdl = { model with PartialData = PartialData.Init() }
                UpdateResponse.withRaised mdl Cmd.none raised
            else
                let mdl = Model.toggleShowErrors true model
                UpdateResponse.basic mdl Cmd.none
        | CollapseAddUnit ->
            let mdl = { model with IsCollapsed = true }
            UpdateResponse.basic mdl Cmd.none
        | ExpandAddUnit ->
            let mdl = { model with IsCollapsed = false }
            UpdateResponse.basic mdl Cmd.none
