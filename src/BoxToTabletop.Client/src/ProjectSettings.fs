namespace BoxToTabletop.Client

open BoxToTabletop.Domain.Types
open Fulma
open Elmish
open System

open BoxToTabletop.Domain

module ProjectSettings =
    open FSharp.Control.AsyncRx
    open Fable.Reaction

    type Model = {
        //Name : string
        Project : Types.Project option
        ColumnSettings : ColumnSettings option
        Config : Config.T
    } with
        static member Init(config : Config.T) = {
            //Name = ""
            Project = None
            ColumnSettings = None
            Config = config
        }

        static member InitFromProject config project = {
            Project = Some project
            ColumnSettings = Some project.ColumnSettings
            Config = config
        }


    type Msg =
    | Noop
    //| CoreUpdate of update : BoxToTabletop.Client.Core.Updates
    //| UpdateName of name : string
    //| LoadProjectOrNew of projectId : Guid option
    | MaybeLoadProject of projectId : Guid option
    | ProjectLoaded of project : Project
    | ProjectLoadFailed of exn
    | UpdateProject of project : Project
    | UpdateProjectSuccess of project : Project
    | UpdateProjectFailure of exn
    //| UpdateProjectSettings of
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
            match model.ColumnSettings with
            | Some cs ->
                [
                    for col in cs.EnumerateWithTransformer() ->
                        checkBoxFor col.Name col.Value (fun ev -> col.Func ev.Checked |> UpdatedColumnSettings |> dispatch)
                ]
            | None -> []

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
            model,  Cmd.none

    module Fetching =
        open Fetch
        open Thoth.Fetch

        let loadProject (model : Model) (id : Guid) =
            let loadFunc i = Promises.loadProject model.Config i
            let cmd = Cmd.OfPromise.either loadFunc id ProjectLoaded ProjectLoadFailed
            model, cmd

        let updateProject (model : Model) (project : Types.Project) =
            let updateFunc p = Promises.updateProject model.Config project
            let model = { model with Project = Some project }
            let cmd = Cmd.OfPromise.either updateFunc project UpdateProjectSuccess UpdateProjectFailure
            model, cmd


    let update (model : Model) (msg : Msg) =
        match msg with
        | Noop -> model, Cmd.none
        | MaybeLoadProject projectIdOpt ->
            match projectIdOpt with
            | Some projId -> Fetching.loadProject model projId
            | None ->
                { model with Project = None }, Cmd.none

//        | LoadProjectOrNew projectIdOpt ->
//            match projectIdOpt with
//            | Some projectId ->
//                Fetching.loadProject model projectId
//            | None ->
//                let newProj = Project.Empty()
//                { model with Project = Some newProj }, Cmd.ofMsg (ProjectLoaded newProj)
        | ProjectLoaded proj ->
            { model with Project = Some proj }, Cmd.none
        | ProjectLoadFailed e ->
            printfn "%A" e
            model, Cmd.none
        | UpdateProject project ->
            Fetching.updateProject model project
        | UpdateProjectSuccess _ -> model, Cmd.none
        | UpdateProjectFailure exn ->
            printfn "%A" exn
            model, Cmd.none
        | UpdatedColumnSettings settings ->
            printfn "in settings handler, new settings are %A" settings
            { model with ColumnSettings = Some settings }, Cmd.none
