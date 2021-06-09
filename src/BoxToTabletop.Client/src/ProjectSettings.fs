namespace BoxToTabletop.Client

open BoxToTabletop.Domain.Types
open Browser.Types
open Fulma
open Elmish
open Elmish.React
open System

open BoxToTabletop.Domain

module ProjectSettings =

    type Model = {
        Project : Types.Project option
        DeleteInitiated : bool
        Config : Config.T
    } with
        static member Init(config : Config.T) = {
            Project = None
            DeleteInitiated = false
            Config = config
        }

        static member InitFromProject config project = {
            Project = Some project
            DeleteInitiated = false
            Config = config
        }

    module Model =
        let setProject m p =
            { m with Project = Some p }

    type RaisedMsg =
    | ProjectDeleted of projectId : Guid
    | ProjectLoaded of project : Project
    | UpdatedColumnSettings of settings : ColumnSettings

    type ExternalSourceMsg =
    | ProjectSelected of project : Project
        //projectId : Guid

    type Msg =
    | External of ExternalSourceMsg
    // | MaybeLoadProject of projectId : Guid option
    | ProjectLoaded of Result<Project, Thoth.Fetch.FetchError>
    | ProjectLoadFailed of exn
    | UpdateProject of project : Project
    | UpdateProjectSuccess of project : Project
    | UpdateProjectFailure of exn
    | UpdatedColumnSettings of ColumnSettings
    | DeleteInitiated
    | DeleteConfirmed
    | DeleteCanceled
    | DeleteSuccess of projectId : Guid
    | DeleteError of exn

    module View =
        open Fable.FontAwesome
        open Fable.React
        open Fable.React.Helpers
        open Fable.React.Props
        open Fable.React.Standard
        open Fulma.Extensions.Wikiki

        let deleteProjectModal dispatch =
            Modal.modal [ Modal.IsActive true ] [
                Modal.background [] []
                Modal.Card.card [] [
                    Modal.Card.head [ GenericOption.Modifiers [ Modifier.BackgroundColor IsDanger; Modifier.TextColor IsDanger ] ] [
                        h2 [] [ str "Are you sure?"]
                    ]
                    Modal.Card.body [] [
                        Label.label [] [
                            str "Are you sure you want to delete this project? All units on this project will be deleted. This action CANNOT be undone."
                        ]
                    ]
                    Modal.Card.foot [] [
                        Button.button [ 
                            Button.Color IsDanger 
                            Button.OnClick (fun _ -> DeleteConfirmed |> dispatch)
                        ] [
                            str "Yes, I want to delete"
                        ]
                        Button.button [ 
                            Button.Color IsPrimary 
                            Button.OnClick (fun _ -> DeleteCanceled |> dispatch)
                        ] [
                            str "Cancel"
                        ]
                    ]
                ]
            ]

        let checkBoxFor cbName isChecked  oc =
            Panel.Block.div [] [
                Switch.switch [
                    Switch.IsRtl
                    Switch.Checked isChecked
                    Switch.OnChange oc
                    Switch.Id (cbName + "-checkbox")
                    Switch.Color Color.IsInfo
                ] [ str cbName ]
            ]
            // Panel.checkbox [] [
            //     input [
            //         Class "toggle"
            //         Id cbName
            //         Type "checkbox"
            //         Checked isChecked
            //         OnChange oc
            //     ]
            //     label [ HtmlFor cbName ] [ str cbName ]
            // ]

        let createCheckboxes (projOpt : Project option) dispatch =
            match projOpt with
            | Some cs ->
                [
                    for col in cs.ColumnSettings.EnumerateWithTransformer() ->
                        checkBoxFor col.Name col.Value (fun ev -> col.Func ev.Checked |> UpdatedColumnSettings |> dispatch)
                ]
            | None -> []

        let view (model : Model) dispatch =
            let nameChangeFunc (ev : Event) =
                match model.Project with
                | Some proj ->
                    { proj with Name = ev.Value } |> UpdateProject
                | None ->
                    //todo: maybe don't bother even rendering this if project is missing?
                    { Types.Project.Empty() with Name = ev.Value } |> UpdateProject

            if model.Project.IsNone then
                div [] []
            else
                Panel.panel [] [
                    if model.DeleteInitiated then deleteProjectModal dispatch
                    Panel.heading [] [ str "Project Settings" ]
                    Panel.Block.div [] [
                        Input.text [
                            Input.Size IsMedium
                            match model.Project with
                            | Some p -> Input.ValueOrDefault p.Name
                            | None -> Input.Placeholder "Project Name"
                            Input.OnChange (fun ev -> nameChangeFunc ev |> dispatch)
                        ]
                    ]
                    yield! createCheckboxes model.Project dispatch
                    Panel.Block.div [] [
                        Button.button [ 
                            Button.IsFullWidth
                            Button.Color IsDanger
                            Button.OnClick (fun _ -> DeleteInitiated |> dispatch)
                        ] [
                            Fa.i [ Fa.Solid.Trash ] []
                            Label.label [ Label.CustomClass "pad-left-10" ] [ str "Delete" ]
                        ]
                    ]
                ]

    type UpdateResponse = Core.UpdateResponse<Model, Msg, RaisedMsg>
    let projectSettingsSpinnerId = Guid.Parse("2A85B2FE-AB77-4ED7-B9F0-FDB148C7B553")

    module ApiCalls =
        open Fetch
        open Thoth.Fetch

        let loadProject (model : Model) (id : Guid) =
            let loadFunc i = Promises.loadProject model.Config i
            let cmd = Cmd.OfPromise.either loadFunc id ProjectLoaded ProjectLoadFailed
            model, cmd

        let updateProject (model : Model) (project : Types.Project) =
            let updateFunc p = Promises.updateProject model.Config p
            let model = { model with Project = Some project }
            let cmd = Cmd.OfPromise.either updateFunc project UpdateProjectSuccess UpdateProjectFailure
            model, cmd

        let deleteProject (model : Model) (projectId : Guid) =
            let deleteFunc id = Promises.deleteProject model.Config id
            let cmd = Cmd.OfPromise.either deleteFunc projectId (fun _ -> DeleteSuccess projectId) DeleteError
            model, cmd

        let tryUpdateColumnSettings (model : Model) (cs : ColumnSettings) =
            match model.Project with
            | Some project ->
                updateProject model { project with ColumnSettings = cs }
            | None ->
                printfn "Received column settings update of %A when there is no project loaded" cs
                model, Cmd.none

    let handleExternalSourceMsg model (msg : ExternalSourceMsg) =
        match msg with
        | ProjectSelected project ->
            let mdl = { model with Project = Some project }
            UpdateResponse.basic mdl Cmd.none

    let update (model : Model) (msg : Msg) =
        match msg with
        | External ext -> 
            handleExternalSourceMsg model ext
        | ProjectLoaded (Ok proj) ->
            let mdl = Model.setProject model proj
            UpdateResponse.withSpin mdl Cmd.none (Core.SpinnerEnd projectSettingsSpinnerId)
        | ProjectLoaded (Error e) ->
            printfn "%A" e
            UpdateResponse.withSpin model Cmd.none (Core.SpinnerEnd projectSettingsSpinnerId)
        | ProjectLoadFailed e ->
            printfn "%A" e
            UpdateResponse.withSpin model Cmd.none (Core.SpinnerEnd projectSettingsSpinnerId)
        | UpdateProject project ->
            let mdl, cmd = ApiCalls.updateProject model project
            UpdateResponse.withSpin mdl cmd (Core.SpinnerStart projectSettingsSpinnerId)
        | UpdateProjectSuccess project -> 
            let raised = RaisedMsg.UpdatedColumnSettings project.ColumnSettings
            let spin = Core.SpinnerEnd projectSettingsSpinnerId
            UpdateResponse.create model Cmd.none (Some spin) (Some raised)
        | UpdateProjectFailure exn ->
            printfn "%A" exn
            UpdateResponse.withSpin model Cmd.none (Core.SpinnerEnd projectSettingsSpinnerId)
        | UpdatedColumnSettings cs ->
            let mdl, cmd = ApiCalls.tryUpdateColumnSettings model cs
            UpdateResponse.withSpin mdl cmd (Core.SpinnerStart projectSettingsSpinnerId)
        | DeleteInitiated ->
            let mdl = {model with DeleteInitiated = true}
            UpdateResponse.basic mdl Cmd.none
        | DeleteConfirmed ->
            match model.Project with
            | Some p ->
                let mdl, cmd = ApiCalls.deleteProject model p.Id
                UpdateResponse.withSpin mdl cmd (Core.SpinnerStart projectSettingsSpinnerId)
            | None ->
                let mdl = { model with DeleteInitiated = false }
                UpdateResponse.basic mdl Cmd.none
        | DeleteCanceled ->
            let mdl = {model with DeleteInitiated = false}
            UpdateResponse.basic mdl Cmd.none
        | DeleteSuccess (projectId) ->
            let mdl = { model with DeleteInitiated = false; Project = None }
            let spin = Core.SpinnerEnd projectSettingsSpinnerId
            let raised = RaisedMsg.ProjectDeleted projectId
            UpdateResponse.create mdl Cmd.none (Some spin) (Some raised)
        | DeleteError e ->
            printfn "Error deleting project: %A" e
            let mdl = { model with DeleteInitiated = false }
            UpdateResponse.withSpin mdl Cmd.none (Core.SpinnerEnd projectSettingsSpinnerId)
