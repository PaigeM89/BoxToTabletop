namespace BoxToTabletop.Client

open Fulma

open BoxToTabletop.Domain

module ProjectSettings =
    open FSharp.Control.AsyncRx
    open Fable.Reaction

    type Model = {
        Name : string
        ModelCountCategories : Types.ModelCountCategory list
    } with
        static member Init() = {
            Name = ""
            ModelCountCategories = Types.stubCategories()
        }


    type Msg =
    | Noop
    | CoreUpdate of update : BoxToTabletop.Client.Core.Updates
    | UpdateName of name : string
    | ToggleMCCVisibility of mcc : Types.ModelCountCategory

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

        let createCheckboxes (model : Model) dispatch =
            [
                for mcc in model.ModelCountCategories ->
                    checkBoxFor mcc.Name mcc.Enabled (fun ev -> { mcc with Enabled = not mcc.Enabled } |> ToggleMCCVisibility |> dispatch)
            ]

        let view (model : Model) dispatch =
            summary [] [
                Heading.h3 [] [ str "Project Name" ]
                hr []
                input [ Id "projectNameInput"; DefaultValue model.Name; OnChange (fun ev -> UpdateName ev.Value |> dispatch) ]
                br []
                yield! createCheckboxes model dispatch
            ]

    let handleCoreUpdate (update : Core.Updates) (model :Model) =
        match update with
        | Core.MCCVisibilityChange mcc ->
            model, Noop

    let update (model : Model) (msg : Msg) =
        match msg with
        | Noop -> model, Noop
        | CoreUpdate coreUpdate ->
            handleCoreUpdate coreUpdate model
        | UpdateName name -> {model with Name = name}, Noop
        | ToggleMCCVisibility mcc ->
            let existing = Types.getModelCountCategoryByName mcc.Name model.ModelCountCategories
            match existing with
            | Some e ->
                let newMccs = Types.replaceModelCountCategory mcc model.ModelCountCategories
                { model with ModelCountCategories = newMccs }, Core.MCCVisibilityChange mcc |> CoreUpdate
            | None ->
                // todo: is this oging to cause consistent behavior, if triggered?
                { model with ModelCountCategories = ( mcc :: model.ModelCountCategories ) }, Core.MCCVisibilityChange mcc |> CoreUpdate

