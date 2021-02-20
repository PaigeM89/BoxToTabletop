namespace BoxToTabletop.Client

open Fulma

open BoxToTabletop.Domain

module ProjectSettings =
    //open Elmish
    open FSharp.Control.AsyncRx
    open Fable.Reaction

    type Model = {
        Name : string
        ColumnSettings : Types.ColumnSettings
    } with
        static member Init() = {
            Name = ""
            ColumnSettings = Types.ColumnSettings.Empty()
        }

    module Model =
        let updateShowAssembled value model =
            let cs = { model.ColumnSettings with ShowAssembled = value }
            { model with ColumnSettings = cs }

    type Msg =
    | UpdateName of name : string
    | UpdateShowAssembled of value : bool

    module View =
        open Fable.React
        open Fable.React.Helpers
        open Fable.React.Props
        open Fable.React.Standard

        let checkBoxFor cbName isChecked  oc =
            div [] [
                input [
                    Class "toggle"
                    Id cbName
                    Type "checkbox"
                    Checked isChecked
                    OnChange oc
                ]
                label [ HtmlFor cbName ] [ str cbName ]
            ]

        let createCheckboxes (settings: Types.ColumnSettings) dispatch =
            [
                checkBoxFor "Show Assembled" settings.ShowAssembled (fun ev -> ev.Checked |> UpdateShowAssembled |> dispatch)
            ]

        let view (model : Model) dispatch =
            summary [] [
                str "Name"
                br []
                input [ Id "projectNameInput"; DefaultValue model.Name; OnChange (fun ev -> UpdateName ev.Value |> dispatch) ]
                br []
                yield! createCheckboxes (model.ColumnSettings) dispatch
            ]


    let update (model : Model) (msg : Msg) =
        match msg with
        | UpdateName name -> {model with Name = name}
        | UpdateShowAssembled value ->
            Model.updateShowAssembled value model
        | _ -> model
