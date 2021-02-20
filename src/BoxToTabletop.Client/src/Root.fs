module Root

open Browser.Types
open Fable.React
open Fable.Reaction
//open Feliz
//open Feliz.Bulma
open FSharp.Control
open Fulma

open BoxToTabletop.Client

type State = {
    EnteredText : string
    Messages : string list
    Project : Project.Model
    ProjectSettings : ProjectSettings.Model
} with
    static member Empty() = {
        EnteredText = ""
        Messages = []
        Project = Project.Model.Init()
        ProjectSettings = ProjectSettings.Model.Init()
    }

type Model =
| Initializing
| Active of state : State

type Msg =
| Enter // of enterValue : string
| Change of newValue : string
| Start
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
            Columns.columns [ Columns.IsGap (Screen.All, Columns.Is1) ]
                [
                    Column.column [ Column.Width (Screen.All, Column.Is3) ] [ projectSettingsView state ]
                    Column.column [ Column.Width (Screen.All, Column.Is9) ] [ projectView state ]
                ]
        | _ -> ()
    ]


open Elmish
open Elmish.React

let projectMsgToCmd msg =
    match msg with
    | _ -> Cmd.none

let elmishUpdate (msg : Msg) (model : Model) =
    match msg, model with
    | Enter, Initializing ->
        State.Empty() |> Active, Cmd.none
    | Enter, Active state ->
        { state with
            Messages = state.EnteredText :: state.Messages
            EnteredText = ""
        } |> Active, Cmd.none
    | Change newValue , Initializing ->
        { State.Empty() with EnteredText = newValue }
        |> Active, Cmd.none
    | Change newValue, Active state ->
        { state with EnteredText = newValue } |> Active, Cmd.none
    | Start, _ ->
            State.Empty() |> Active, Cmd.none
    | ProjectMsg projectMsg, Active state ->
            let project', cmd = Project.update state.Project projectMsg
            { state with Project = project' } |> Active, projectMsgToCmd cmd
    | ProjectMsg projectMsg, Initializing ->
            let state = State.Empty()
            let project', cmd = Project.update state.Project projectMsg
            { state with Project = project' } |> Active, projectMsgToCmd cmd
    | ProjectSettingsMsg settingsMsg, Active state ->
            let project' = ProjectSettings.update state.ProjectSettings settingsMsg
            { state with ProjectSettings = project' } |> Active, Cmd.none
    | ProjectSettingsMsg settingsMsg, Initializing ->
            let state = State.Empty()
            let project' = ProjectSettings.update state.ProjectSettings settingsMsg
            { state with ProjectSettings = project' } |> Active, Cmd.none

let init () = Initializing, Cmd.ofMsg Start

printfn "Starting application"

Program.mkProgram
    init
    elmishUpdate
    view
|> Program.withConsoleTrace
|> Program.withReactBatched "elmish-app"
|> Program.run


