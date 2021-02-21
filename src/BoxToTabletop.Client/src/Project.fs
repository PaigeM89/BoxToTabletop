namespace BoxToTabletop.Client

open BoxToTabletop.Domain
open BoxToTabletop.Domain.Helpers
open BoxToTabletop.Domain.Types
open Elmish
open System
open FSharp.Control
open Fulma
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
    | AddUnitSuccess of string
    | AddUnitFailure of exn
    | DeleteRow of id : Guid option

    module View =
        open Fable.React
        open Fable.React.Helpers
        //https://github.com/fable-compiler/fable-react/blob/master/src/Fable.React.Props.fs
        open Fable.React.Props
        //https://github.com/fable-compiler/fable-react/blob/master/src/Fable.React.Standard.fs

        let addUnitWrapper custom elements =
            div [ ClassName "Block"  ] [ Box.box' custom [
                Level.level [ Level.Level.CustomClass "no-flex-shrink" ] [
                    Level.item [ Level.Item.HasTextCentered; Level.Item.CustomClass "no-flex-shrink" ] [ div [] elements ]
                ]
            ]]


        let numericInput inputColor name (dv : int) action =
            printfn "rendering numeric input for %s with value %i" name dv
            let numberInput =
                Input.number [
                    Input.Id name
                    Input.Placeholder (string 0)
                    inputColor
                    Input.ValueOrDefault (string dv)
                    Input.OnChange action
                    Input.Props [ HTMLAttr.Min 0; HTMLAttr.FrameBorder "1px solid";  ]
                    Input.CustomClass "numeric-input-width"
                ]

            addUnitWrapper [] [
                Level.heading [] [ str name ]
                Field.div [ Field.HasAddonsRight] [
                    Level.item [ Level.Item.CustomClass "no-flex-shrink" ] [
                        numberInput
                    ]
                ]
            ]

        let unitNameInput inputColor partial dispatch =
            Level.level [  Level.Level.Modifiers [  Modifier.Display (Screen.All, Fulma.Display.Option.Flex) ]; Level.Level.CustomClass "no-flex-shrink"] [
                Level.item [ Level.Item.HasTextCentered; Level.Item.CustomClass "no-flex-shrink" ] [
                    div []  [
                        Level.heading [] [ str "Unit Name" ]
                        Level.item [] [
                            Input.text [
                                inputColor
                                Input.Placeholder "Unit name"; Input.ValueOrDefault partial.UnitName; Input.OnChange (fun ev -> UpdateUnitName ev.Value |> dispatch)
                            ]
                        ]
                    ]
                ]
            ] |> List.singleton |> addUnitWrapper []

        let inputNewUnit cs partial dispatch =
            printfn "rendering new unit input with settings %A and partial %A" cs partial
            let func (ev : Browser.Types.Event) transform =
                let c = Parsing.parseIntOrZero ev.Value
                UpdatePartialData (transform partial c)
                |> dispatch
            let inputColor =
                if partial.ShowError then IsDanger else NoColor
                |> Input.Color
            Level.level [ ] [
                unitNameInput inputColor partial dispatch
                numericInput inputColor "Models" partial.ModelCount (fun ev -> func ev (fun p c -> { p with ModelCount = c }))
                if cs.AssemblyVisible then numericInput inputColor "Assembled" partial.AssembledCount (fun ev -> func ev (fun p c -> { p with AssembledCount = c }))
                if cs.PrimedVisible then numericInput inputColor "Primed" partial.PrimedCount (fun ev -> func ev (fun p c -> { p with PrimedCount = c }))
                if cs.PaintedVisible then numericInput inputColor "Painted" partial.PaintedCount (fun ev -> func ev (fun p c -> { p with PaintedCount = c }))
                if cs.BasedVisible then numericInput inputColor "Based" partial.BasedCount (fun ev -> func ev (fun p c -> { p with BasedCount = c }))
                addUnitWrapper [  CustomClass "add-unit-button-box" ] [
                    Button.Input.submit [
                        Button.Props [ Value "Add" ]
                        Button.Color IsSuccess
                        Button.OnClick (fun _ -> AddUnit |> dispatch)
                    ]
                ]
            ]

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

            //todo: make this able to edit in place
            let table =
                [
                    yield tableHeaders
                    for unit in model.Project.Units do yield unitRow model.ColumnSettings unit dispatch
                ]
                |> Table.table [ Table.IsBordered; Table.IsStriped; Table.IsNarrow; Table.IsHoverable; Table.CustomClass "list-units-table" ]
            Section.section [] [
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
            { model with ColumnSettings = cs }, Cmd.none

    let saveUnit (unit : Unit) = async {
        do! Async.Sleep 1000

        return (sprintf "database updated, saved unit %A" unit.Name)
    }

    let update (model : Model) (msg : Msg) =
        match msg with
        | Noop -> model, Cmd.none
        | CoreUpdate update ->
            printfn "handling core update in project"
            handleCoreUpdate update model
        | UpdateUnitName newName ->
            { model with PartialData = { model.PartialData with UnitName = newName } }, Cmd.none
        | UpdateUnitModelCount newCount ->
            { model with PartialData = { model.PartialData with ModelCount = newCount } }, Cmd.none
        | UpdatePartialData newPartial ->
            { model with PartialData = newPartial }, Cmd.none
        | AddUnit ->
            let p = model.PartialData
            if p.IsValid() then
                let unit = p.ToUnit()
                let cmd = Cmd.OfAsync.either saveUnit unit AddUnitSuccess AddUnitFailure
                model |> Model.clearAndAddUnit unit, cmd
            else
                model |> Model.markShowErrors true, Cmd.none
        | AddUnitSuccess successMsg ->
            printfn "%s" successMsg
            model, Cmd.none
        | AddUnitFailure err ->
            printfn "%A" err
            model, Cmd.none
        | DeleteRow (Some id) ->
                model |> Model.removeRowById id, Cmd.none
        | DeleteRow (None) ->
                printfn "Unable to delete row without ID"
                model, Cmd.none

