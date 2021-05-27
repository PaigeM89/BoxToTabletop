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

    type Model = {
        ProjectId : Guid
        PartialData : PartialData
        ColumnSettings : ColumnSettings
        Config : Config.T
    } with
        static member Init config = {
            ProjectId = Guid.Empty
            PartialData = PartialData.Init()
            ColumnSettings = ColumnSettings.Empty()
            Config = config
        }

    module Model =
        let toggleShowErrors value model =
            { model with PartialData = { model.PartialData with ShowError = value } }

        let clearInput model =
            { model with PartialData = PartialData.Init() }

        module Unit =
            let setProjectId projId unit = { unit with Unit.ProjectId = projId }

    type ExternalMsg =
    /// Raised externally. The unit was successfully added. Clear the input.
    | AddNewUnitSuccess

    type Msg =
    | UpdateColumnSettings of cs : ColumnSettings
    | UpdatePartialData of newPartial : PartialData
    /// Called when 'Add' is clicked. Does not contain a unit, as the input has not been verified.
    | TryAddNewUnit
    /// Adds a new, valid unit.
    | AddNewUnit of unit : Unit
    /// There are invalid inputs to create a new unit
    | ShowInputErrors
    | External of ExternalMsg

    module View =
        open Fable.React
        open Fable.React.Props
        open Fulma
        open Browser.Types
        open BoxToTabletop.Domain.Helpers

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

        let view model dispatch =
            let partial = model.PartialData
            let cs = model.ColumnSettings
            // printfn "column settings when drawing add unit is %A" cs
            let func (ev : Browser.Types.Event) transform =
                let c = Parsing.parseIntOrZero ev.Value
                UpdatePartialData (transform partial c)
                |> dispatch
            let inputColor =
                if partial.ShowError then IsDanger else NoColor
                |> Input.Color
            Section.section [ Section.CustomClass "no-padding-section"  ] [
                Heading.h3 [ Heading.IsSubtitle  ] [ Text.p [ Modifiers [ Modifier.TextAlignment (Screen.All, TextAlignment.Centered) ] ][ str "Add a new unit" ]]
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
                            Button.OnClick (fun _ -> TryAddNewUnit |> dispatch)
                        ]
                    ]
                ]
            ]

    open Elmish

    let update model msg =
        match msg with
        | UpdateColumnSettings cs ->
            { model with Model.ColumnSettings = cs }, Cmd.none
        | UpdatePartialData np ->
            { model with PartialData = np }, Cmd.none
        | TryAddNewUnit ->
            if model.PartialData.IsValid() then
                printfn "Setting new unit project id to %A" model.ProjectId
                let nu = model.PartialData.ToUnit() |> Model.Unit.setProjectId model.ProjectId
                model, Cmd.ofMsg (AddNewUnit nu)
            else
                Model.toggleShowErrors true model , Cmd.none
        | AddNewUnit _ ->
            model, Cmd.none
        | ShowInputErrors ->
            Model.toggleShowErrors false model, Cmd.none
        | External (AddNewUnitSuccess) ->
            { model with PartialData = PartialData.Init() }, Cmd.none

