namespace BoxToTabletop.Client

open BoxToTabletop.Domain.Types
open Fulma

open BoxToTabletop.Domain

module ProjectSettings =
    open FSharp.Control.AsyncRx
    open Fable.Reaction

    type Model = {
        Name : string
        ColumnSettings : ColumnSettings
    } with
        static member Init() = {
            Name = ""
            ColumnSettings = ColumnSettings.Empty()
        }


    type Msg =
    | Noop
    | CoreUpdate of update : BoxToTabletop.Client.Core.Updates
    | UpdateName of name : string
    | UpdatedColumnSettings of ColumnSettings

    module View =
        open Fable.React
        open Fable.React.Helpers
        open Fable.React.Props
        open Fable.React.Standard

        let checkBoxFor cbName isChecked  oc =
            Panel.checkbox [] [
                input [
                    Class "toggle"
                    Id cbName
                    Type "checkbox"
                    Checked isChecked
                    OnChange oc
                ]
                label [ HtmlFor cbName ] [ str cbName ]
            ]

        let createCheckboxes (model : Model) dispatch =
            [
                for col in model.ColumnSettings.EnumerateWithTransformer() ->
                    checkBoxFor col.Name col.Value (fun ev -> col.Func ev.Checked |> UpdatedColumnSettings |> dispatch)
            ]

        let view (model : Model) dispatch =
            Panel.panel [] [
                Panel.heading [] [ str "Project Settings" ]
                Panel.Block.div [] [
                    Input.text [ Input.Size IsMedium; Input.Placeholder "Project Name" ]
                ]
                yield! createCheckboxes model dispatch
            ]

    let handleCoreUpdate (update : Core.Updates) (model :Model) =
        match update with
        | Core.ColumnSettingsChange _ ->
            // this component is the one that updates this setting; we don't need to handle it.
            model, Noop

    let update (model : Model) (msg : Msg) =
        match msg with
        | Noop -> model, Noop
        | CoreUpdate coreUpdate ->
            handleCoreUpdate coreUpdate model
        | UpdateName name -> {model with Name = name}, Noop
        | UpdatedColumnSettings settings ->
            { model with ColumnSettings = settings }, Core.ColumnSettingsChange settings |> CoreUpdate
