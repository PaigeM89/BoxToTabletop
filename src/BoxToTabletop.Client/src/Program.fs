module Root

open BoxToTabletop.Domain.Types
open BoxToTabletop.Client.Core
open Browser.Types
open Fable.React
open Fable.Reaction
open Elmish

open FSharp.Control
open Fulma

open BoxToTabletop.Client

type Model = {
    Project : Project.Model
    ProjectSettings : ProjectSettings.Model
    ShowSpinner : bool
} with
    static member Empty() = {
        Project = Project.Model.Init()
        ProjectSettings = ProjectSettings.Model.Init()
        ShowSpinner = false
    }

type Msg =
| Start
| Core of Core.Updates
| ProjectMsg of Project.Msg
| ProjectSettingsMsg of ProjectSettings.Msg

module View =
    open Fable.React.Props
    open Fable.FontAwesome

    let mapShowSpinner (model : Model) =
        match model.ShowSpinner with
        | true ->
            div [ Class ("block " + Fa.Classes.Size.Fa3x) ] [
                Fa.i [ Fa.Solid.Spinner; Fa.Spin ] []
            ] |> Some
        | false -> None

    let navbar (model : Model) dispatch =
        let spinner = mapShowSpinner model
        Navbar.navbar [ Navbar.Color IsPrimary ] [
            Navbar.Start.div [ ] [
                Level.level [] [
                    Level.item [ Level.Item.HasTextCentered ] [
                        div [ Class ("block " + Fa.Classes.Size.Fa3x) ] [Fa.i [ Fa.Solid.PaintBrush ] []]
                    ]
                    Level.item [ Level.Item.HasTextCentered ] [
                        Heading.h3 [
                            //todo: this doesn't work lol
                            Heading.Modifiers [ Modifier.Spacing (Spacing.PaddingLeft, Spacing.Is5) ]
                        ] [
                            str "Box To Tabletop"
                        ]
                    ]
                ]
            ]
            if Option.isSome spinner then Navbar.End.div [] [ Option.get spinner ]
        ]

    let view (model : Model) (dispatch : Msg -> unit)  =
        let projectSettingsView state =
            ProjectSettings.View.view
                state.ProjectSettings (fun (x : ProjectSettings.Msg) ->  ProjectSettingsMsg x |> dispatch)

        let projectView state =
            Project.View.view state.Project (fun (x : Project.Msg) ->  ProjectMsg x |> dispatch)

        let navbar = navbar model dispatch

        div [] [
            navbar
            Container.container [ Container.IsFluid ] [
                Columns.columns [ Columns.IsGap (Screen.All, Columns.Is1); Columns.IsGrid; Columns.IsVCentered ]
                    [
                        Column.column [ Column.Width (Screen.All, Column.Is3) ] [ projectSettingsView model ]
                        Column.column [ Column.Width (Screen.All, Column.IsNarrow) ] []
                        Column.column [  ] [ projectView model ]
                    ]
            ]
        ]



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

let handleProjectMsg (msg : Project.Msg) (model : Model) =
    match msg with
    | Project.AddUnit _ ->
        { model with ShowSpinner = true }, Cmd.none
    | Project.AddUnitSuccess _ ->
        { model with ShowSpinner = false }, Cmd.none
    | Project.AddUnitFailure _ ->
        { model with ShowSpinner = false }, Cmd.none
    | _ -> model, Cmd.none

let update (msg : Msg) (model : Model) : (Model * Cmd<Msg>)=
    match msg with
    | Start ->
        model, Cmd.none
    | ProjectMsg projectMsg ->
        let model, firstCmd = handleProjectMsg projectMsg model
        let project', projectCmd = Project.update model.Project projectMsg

        let cmd =
            if firstCmd.IsEmpty then
                Cmd.map ProjectMsg projectCmd
            else
                let c = Cmd.map ProjectMsg projectCmd
                [ c; firstCmd ] |> Cmd.batch

        { model with Project = project' }, cmd
    | ProjectSettingsMsg settingsMsg ->
        let project', cmd = ProjectSettings.update model.ProjectSettings settingsMsg
        { model with ProjectSettings = project' }, Cmd.map ProjectSettingsMsg cmd
    | Core msg'->
        printfn "handling core update in active state"
        let settings, cmd' = ProjectSettings.update model.ProjectSettings (ProjectSettings.CoreUpdate msg')
        let project, cmd'' = Project.update model.Project (Project.CoreUpdate msg')
        let model = { model with Project = project; ProjectSettings = settings }
        model, [
            (Cmd.map ProjectSettingsMsg cmd')
            (Cmd.map ProjectMsg cmd'')
        ] |> Cmd.batch

let init () =
    printfn "in init"
    Model.Empty(), Cmd.ofMsg Start

Program.mkProgram
    init
    update
    View.view
|> Program.withConsoleTrace
|> Program.withReactBatched "root"
|> Program.run


