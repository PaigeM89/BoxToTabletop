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
        //Name : string
        Project : Types.Project option
        //ColumnSettings : ColumnSettings option
        Config : Config.T
    } with
        static member Init(config : Config.T) = {
            //Name = ""
            Project = None
            //ColumnSettings = None
            Config = config
        }

        static member InitFromProject config project = {
            Project = Some project
            //ColumnSettings = Some project.ColumnSettings
            Config = config
        }

    module Model =
        let setProject m p =
            //{ m with Project = Some p; ColumnSettings = Some p.ColumnSettings }
            { m with Project = Some p }


    type Msg =
    | Noop
    | MaybeLoadProject of projectId : Guid option
    | ProjectLoaded of Result<Project, Thoth.Fetch.FetchError>
    | ProjectLoadFailed of exn
    | UpdateProject of project : Project
    | UpdateProjectSuccess of project : Project
    | UpdateProjectFailure of exn
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

            Panel.panel [] [
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
            ]

    module ApiCalls =
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

        let tryUpdateColumnSettings (model : Model) (cs : ColumnSettings) =
            match model.Project with
            | Some project ->
                updateProject model { project with ColumnSettings = cs }
            | None ->
                printfn "Received column settings update of %A when there is no project loaded" cs
                model, Cmd.none


    let update (model : Model) (msg : Msg) =
        match msg with
        | Noop -> model, Cmd.none
        | MaybeLoadProject projectIdOpt ->
            match projectIdOpt with
            | Some projId -> ApiCalls.loadProject model projId
            | None ->
                { model with Project = None }, Cmd.none
        | ProjectLoaded (Ok proj) ->
            let mdl = Model.setProject model proj
            mdl, Cmd.none
        | ProjectLoaded (Error e) ->
            printfn "%A" e
            model, Cmd.none
        | ProjectLoadFailed e ->
            printfn "%A" e
            model, Cmd.none
        | UpdateProject project ->
            ApiCalls.updateProject model project
        | UpdateProjectSuccess _ -> model, Cmd.none
        | UpdateProjectFailure exn ->
            printfn "%A" exn
            model, Cmd.none
        | UpdatedColumnSettings cs ->
            ApiCalls.tryUpdateColumnSettings model cs
