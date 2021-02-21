module Root

open BoxToTabletop.Domain.Types
open BoxToTabletop.Client.Core
open Browser.Types
open Fable.React
open Fable.Reaction

open FSharp.Control
open Fulma

open BoxToTabletop.Client

type Model = {
    Project : Project.Model
    ProjectSettings : ProjectSettings.Model
} with
    static member Empty() = {
        Project = Project.Model.Init()
        ProjectSettings = ProjectSettings.Model.Init()
    }

type Msg =
| Start
| Core of Core.Updates
| ProjectMsg of Project.Msg
| ProjectSettingsMsg of ProjectSettings.Msg

let view (model : Model) (dispatch : Msg -> unit)  =
    let projectSettingsView state =
        ProjectSettings.View.view
            state.ProjectSettings (fun (x : ProjectSettings.Msg) ->  ProjectSettingsMsg x |> dispatch)

    let projectView state =
        Project.View.view state.Project (fun (x : Project.Msg) ->  ProjectMsg x |> dispatch)

    div [] [
        Heading.h2 [] [ str "Welcome to the Box To Table Wargame project tracker!"]
        //match model with
        //| Active state ->
        Container.container [ Container.IsFluid ] [
            Columns.columns [ Columns.IsGap (Screen.All, Columns.Is1); Columns.IsGrid ]
                [
                    Column.column [ Column.Width (Screen.All, Column.Is3) ] [ projectSettingsView model ]
                    Column.column [ Column.Width (Screen.All, Column.IsNarrow) ] []
                    // Column.Width (Screen.All, Column.IsFourFifths)
                    Column.column [  ] [ projectView model ]
                ]
        ]
        //| _ -> ()
    ]


open Elmish
open Elmish.React

let projectMsgToCmd msg =
    match msg with
    | _ -> Cmd.none

let settingsMsgToCmd msg =
    printfn "settings to cmd"
    match msg with
    | ProjectSettings.CoreUpdate msg -> Cmd.ofMsg (Core msg)
    | _ ->
        printfn "none lol"
        Cmd.none

let update (msg : Msg) (model : Model) : (Model * Cmd<Msg>)=
    match msg with
    | Start ->
        model, Cmd.none
    | ProjectMsg projectMsg ->
        let project', cmd = Project.update model.Project projectMsg
        { model with Project = project' }, projectMsgToCmd cmd
    | ProjectSettingsMsg settingsMsg ->
        let project', projmsg = ProjectSettings.update model.ProjectSettings settingsMsg
        { model with ProjectSettings = project' }, settingsMsgToCmd projmsg
    | Core msg'->
        printfn "handling core update in active state"
        let settings, cmd' = ProjectSettings.update model.ProjectSettings (ProjectSettings.CoreUpdate msg')
        let project, cmd'' = Project.update model.Project (Project.CoreUpdate msg')
        let model = { model with Project = project; ProjectSettings = settings }
        match cmd', cmd'' with
        | ProjectSettings.Noop, Project.Noop ->  model , Cmd.none
        | ProjectSettings.Noop, p -> model, projectMsgToCmd p
        | p, Project.Noop -> model, settingsMsgToCmd p
        | p1, p2 -> model, [ projectMsgToCmd p2; settingsMsgToCmd p1 ] |> Cmd.batch

let init () =
    printfn "in init"
    Model.Empty(), Cmd.ofMsg Start

Program.mkProgram
    init
    update
    view
|> Program.withConsoleTrace
|> Program.withReactBatched "root"
|> Program.run


