module Root

open BoxToTabletop.Domain.Types
open BoxToTabletop.Client.Core
open Browser.Types
open Fable.React
open Fable.Reaction

open FSharp.Control
open Fulma

open BoxToTabletop.Client

type State = {
    Project : Project.Model
    ProjectSettings : ProjectSettings.Model
} with
    static member Empty() = {
        Project = Project.Model.Init()
        ProjectSettings = ProjectSettings.Model.Init()
    }

type Model =
| Initializing
| Active of state : State

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
        match model with
        | Active state ->
            Container.container [ Container.IsFluid ] [
                Columns.columns [ Columns.IsGap (Screen.All, Columns.Is1); Columns.IsGrid ]
                    [
                        Column.column [ Column.Width (Screen.All, Column.Is3) ] [ projectSettingsView state ]
                        Column.column [ Column.Width (Screen.All, Column.IsNarrow) ] []
                        // Column.Width (Screen.All, Column.IsFourFifths)
                        Column.column [  ] [ projectView state ]
                    ]
            ]
        | _ -> ()
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

let elmishUpdate (msg : Msg) (model : Model) : (Model * Cmd<Msg>)=
    match msg, model with
    | Start, Initializing ->
        State.Empty() |> Active, Cmd.none
    | Start, Active _ ->
        model, Cmd.none
    | ProjectMsg projectMsg, Active state ->
        let project', cmd = Project.update state.Project projectMsg
        { state with Project = project' } |> Active, projectMsgToCmd cmd
    | ProjectMsg projectMsg, Initializing ->
        let state = State.Empty()
        let project', cmd = Project.update state.Project projectMsg
        { state with Project = project' } |> Active, projectMsgToCmd cmd
    | ProjectSettingsMsg settingsMsg, Active state ->
        let project', projmsg = ProjectSettings.update state.ProjectSettings settingsMsg
        { state with ProjectSettings = project' } |> Active, settingsMsgToCmd projmsg
    | ProjectSettingsMsg settingsMsg, Initializing ->
        let state = State.Empty()
        let project', projmsg = ProjectSettings.update state.ProjectSettings settingsMsg
        { state with ProjectSettings = project' } |> Active, settingsMsgToCmd projmsg
    | Core msg', Initializing ->
        printfn "handling core update from init state"
        let state = State.Empty()
        let settings, cmd' = ProjectSettings.update state.ProjectSettings (ProjectSettings.CoreUpdate msg')
        let project, cmd'' = Project.update state.Project (Project.CoreUpdate msg')
        let state = { state with Project = project; ProjectSettings = settings }
        match cmd', cmd'' with
        | ProjectSettings.Noop, Project.Noop -> Active state , Cmd.none
        | ProjectSettings.Noop, p -> Active state, projectMsgToCmd p
        | p, Project.Noop -> Active state, settingsMsgToCmd p
        | p1, p2 -> Active state, [ projectMsgToCmd p2; settingsMsgToCmd p1 ] |> Cmd.batch
    | Core msg', Active state ->
        printfn "handling core update in active state"
        let settings, cmd' = ProjectSettings.update state.ProjectSettings (ProjectSettings.CoreUpdate msg')
        let project, cmd'' = Project.update state.Project (Project.CoreUpdate msg')
        let state = { state with Project = project; ProjectSettings = settings }
        match cmd', cmd'' with
        | ProjectSettings.Noop, Project.Noop -> Active state , Cmd.none
        | ProjectSettings.Noop, p -> Active state, projectMsgToCmd p
        | p, Project.Noop -> Active state, settingsMsgToCmd p
        | p1, p2 -> Active state, [ projectMsgToCmd p2; settingsMsgToCmd p1 ] |> Cmd.batch



let init () =
    printfn "in init"
    Initializing, Cmd.ofMsg Start

Program.mkProgram
    init
    elmishUpdate
    view
|> Program.withConsoleTrace
|> Program.withReactBatched "elmish-app"
|> Program.run


