module Root

open System.ComponentModel
open App
open BoxToTabletop.Domain.Routes
open BoxToTabletop.Domain.Types
open BoxToTabletop.Client.Core
open Browser.Types
open Fable.React
open Fable.Reaction
open Elmish

open FSharp.Control
open Fulma
open Fulma.Extensions.Wikiki
open BoxToTabletop.Client

type Model = {
    UnitsListModel : UnitsList.Model
    ProjectSettings : ProjectSettings.Model
    ProjectsListModel : ProjectsList.Model
    Config : Config.T
    ShowSpinner : bool
    IsProjectSectionCollapsed : bool
} with
    static member InitWithConfig (config : Config.T) = {
        //Project = Project.Model.Init(config)
        UnitsListModel = UnitsList.Model.Init(config)
        ProjectSettings = ProjectSettings.Model.Init(config)
        ProjectsListModel = ProjectsList.Model.Empty()
        ShowSpinner = false
        Config = config
        IsProjectSectionCollapsed = false
    }

    static member Empty() =
        Config.T.Default() |> Model.InitWithConfig

type Msg =
| Start
//| Core of Core.Updates
| UnitsListMsg of UnitsList.Msg
| ProjectSettingsMsg of ProjectSettings.Msg
| ProjectsListMsg of ProjectsList.Msg

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

    let leftPanel (model : Model) dispatch =
        let projectSettingsView state =
            ProjectSettings.View.view
                state.ProjectSettings (fun (x : ProjectSettings.Msg) ->  ProjectSettingsMsg x |> dispatch)

        if model.IsProjectSectionCollapsed then
            Column.column [ Column.Width (Screen.All, Column.Is1) ] [
                Button.button [ Button.Color Color.IsInfo; Button.IsRounded; Button.IsExpanded  ] [
                    Fa.i [ Fa.Solid.AngleDoubleRight; Fa.Size Fa.Fa3x;  ] [ ]
                ]
            ]
        else

            Column.column [ Column.Width (Screen.All, Column.Is3) ] [
                Level.right [] [
                    Button.button [
                        Button.Color Color.NoColor
                        Button.IsRounded
                        Button.CustomClass "collapse-button"
                    ] [
                        Fa.i [ Fa.Solid.AngleDoubleLeft; Fa.Size Fa.Fa3x;  ] [ ]
                    ]
                ]
                ProjectsList.View.view model.ProjectsListModel (fun (x : ProjectsList.Msg) -> ProjectsListMsg x |> dispatch)
                projectSettingsView model
            ]

    let divider model =
        if model.IsProjectSectionCollapsed then
            None
        else
            Some
                (Column.column [ Column.Width (Screen.All, Column.Is1) ] [
                            Divider.divider [ Divider.IsVertical ]
                    ])

    let view (model : Model) (dispatch : Msg -> unit)  =
        let projectView state =
            UnitsList.View.view state.UnitsListModel (fun (x : UnitsList.Msg) ->  UnitsListMsg x |> dispatch)

        let navbar = navbar model dispatch
        let dividerOption = divider model
        div [] [
            navbar
            Container.container [ Container.IsFluid ] [
                Columns.columns [ Columns.IsGap (Screen.All, Columns.Is1); Columns.IsGrid; Columns.IsVCentered ]
                    [
                        leftPanel model dispatch
                        if dividerOption.IsSome then Option.get dividerOption
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
    //| ProjectSettings.CoreUpdate msg -> Cmd.ofMsg (Core msg)
    | _ ->
        printfn "none lol"
        Cmd.none

let handleProjectSettingsMsg (msg : ProjectSettings.Msg) (model : Model) =
    match msg with
    | ProjectSettings.ProjectLoaded proj ->
        let projset = ProjectSettings.Model.InitFromProject model.Config proj
        let model = { model with ShowSpinner = false; ProjectSettings = projset }
        let cmd = UnitsList.Msg.LoadUnitsForProject proj.Id |> UnitsListMsg |> Cmd.ofMsg
        model, cmd
    | _ -> model, Cmd.none

let handleUnitsListMsg (msg : UnitsList.Msg) (model : Model) =
    match msg with
    | UnitsList.UpdateUnitRow _
    | UnitsList.AddUnit _ ->
        { model with ShowSpinner = true }, Cmd.none
    | UnitsList.AddUnitSuccess _
    | UnitsList.AddUnitFailure _
    | UnitsList.UpdateUnitSuccess _
    | UnitsList.UpdateUnitFailure _ ->
        { model with ShowSpinner  = false }, Cmd.none
    | _ -> model, Cmd.none

let handleProjectsListMsg (msg : ProjectsList.Msg) (model : Model) =
    match msg with
    | ProjectsList.LoadAllProjects ->
        { model with ShowSpinner = true }, Cmd.none
    | ProjectsList.ProjectsLoadError _
    | ProjectsList.AllProjectsLoaded _ ->
        { model with ShowSpinner = false }, Cmd.none
    | ProjectsList.DefaultProjectCreated proj ->
        printfn "calling project lists update"
        //let listMdl, projListCmd = ProjectsList.update model.ProjectsListModel msg
        let settingsCmd = Cmd.ofMsg (ProjectSettings.Msg.ProjectLoaded proj) |> Cmd.map ProjectSettingsMsg
        let cmds =
            [
                settingsCmd
                //(Cmd.map ProjectsListMsg projListCmd)
            ] |> Cmd.batch
            // ProjectsListModel = listMdl
        { model with ShowSpinner = false; }, cmds

let update (msg : Msg) (model : Model) : (Model * Cmd<Msg>)=
    printfn "in root update for msg %A" msg
    match msg with
    | Start ->
        //let loadProjectCmd = Cmd.ofMsg (ProjectSettings.Msg.LoadProjectOrNew None)
        let loadProjectsCmd = Cmd.ofMsg (ProjectsList.Msg.LoadAllProjects)
        //let loadUnitsCmd = Cmd.ofMsg (Project.Msg.LoadUnitsForProject System.Guid.Empty)
        { model with ShowSpinner = true }, Cmd.map ProjectsListMsg loadProjectsCmd
    | UnitsListMsg unitsListMsg ->
        let model, firstCmd = handleUnitsListMsg unitsListMsg model
        let unitsListMdl, projectCmd = UnitsList.update model.UnitsListModel unitsListMsg

        let cmd =
            if firstCmd.IsEmpty then
                Cmd.map UnitsListMsg projectCmd
            else
                let c = Cmd.map UnitsListMsg projectCmd
                [ c; firstCmd ] |> Cmd.batch

        { model with UnitsListModel = unitsListMdl }, cmd
    | ProjectSettingsMsg projectSettingsMsg ->
        let model, firstCmd = handleProjectSettingsMsg projectSettingsMsg model
        let settingsMdl, settingsCmd = ProjectSettings.update model.ProjectSettings projectSettingsMsg
        let cmd =
            if firstCmd.IsEmpty then
                Cmd.map ProjectSettingsMsg settingsCmd
            else
                let c = Cmd.map ProjectSettingsMsg settingsCmd
                [ c; firstCmd ] |> Cmd.batch
        { model with ProjectSettings = settingsMdl }, cmd
    | ProjectsListMsg projListMsg ->
        let model, firstCmd = handleProjectsListMsg projListMsg model
        let listMdl, listCmd = ProjectsList.update model.ProjectsListModel projListMsg
        let cmd =
            let c = Cmd.map ProjectsListMsg listCmd
            [ c; firstCmd ] |> Cmd.batch
        { model with ProjectsListModel = listMdl }, cmd
//    | ProjectSettingsMsg (ProjectSettings.UpdatedColumnSettings cs) ->
//        //let model, settingsCmd = handleProjectSettingsMsg msg
//        // capture a ProjectSettings message and dispatch it manually, then collect the results
//        //let project, cmd = UnitsList.update model.UnitsListModel (Project.CoreUpdate (Updates.ColumnSettingsChange cs))
//        let settings, settingsCmd = ProjectSettings.update model.ProjectSettings (ProjectSettings.UpdatedColumnSettings cs)
//        { model with Project = project; ProjectSettings = settings },
//            [ Cmd.map ProjectMsg cmd; Cmd.map ProjectSettingsMsg settingsCmd ] |> Cmd.batch
//    | ProjectSettingsMsg settingsMsg ->
//        let project', cmd = ProjectSettings.update model.ProjectSettings settingsMsg
//        { model with ProjectSettings = project' }, Cmd.map ProjectSettingsMsg cmd

let init (url : string option) =
    printfn "in init"
    let config = Config.T.Default()
    let config =
        match url with
        | Some url -> Config.withServerUrl url config
        | None -> config
    let model = Model.InitWithConfig config
    { model with Config = config}, Cmd.ofMsg Start

Program.mkProgram
    init
    update
    View.view
|> Program.withConsoleTrace
|> Program.withReactBatched "root"
|> Program.runWith (Some "http://localhost:5000")


