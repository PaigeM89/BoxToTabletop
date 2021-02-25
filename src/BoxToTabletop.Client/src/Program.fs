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
    AddUnitModel : AddUnit.Model
    UnitsListModel : UnitsList.Model
    ProjectSettings : ProjectSettings.Model
    ProjectsListModel : ProjectsList.Model
    Config : Config.T
    ShowSpinner : bool
    IsProjectSectionCollapsed : bool
} with
    static member InitWithConfig (config : Config.T) = {
        //Project = Project.Model.Init(config)
        AddUnitModel = AddUnit.Model.Init(config)
        UnitsListModel = UnitsList.Model.Init(config)
        ProjectSettings = ProjectSettings.Model.Init(config)
        ProjectsListModel = ProjectsList.Model.Init(config)
        ShowSpinner = false
        Config = config
        IsProjectSectionCollapsed = false
    }

    static member Empty() =
        Config.T.Default() |> Model.InitWithConfig

type Msg =
| Start
| AddUnitMsg of AddUnit.Msg
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

        let inputView model =
            AddUnit.View.view model.AddUnitModel (fun (x : AddUnit.Msg) -> AddUnitMsg x |> dispatch)

        let navbar = navbar model dispatch
        let dividerOption = divider model
        div [] [
            navbar
            Container.container [ Container.IsFluid ] [
                Columns.columns [ Columns.IsGap (Screen.All, Columns.Is1); Columns.IsGrid; Columns.IsVCentered ]
                    [
                        leftPanel model dispatch
                        if dividerOption.IsSome then Option.get dividerOption
                        Column.column [  ] [
                            inputView model
                            projectView model
                        ]
                    ]
            ]
        ]



open Elmish.React

let handleProjectSettingsMsg (msg : ProjectSettings.Msg) (model : Model) =
    let setMdl, setCmd = ProjectSettings.update model.ProjectSettings msg

    let mdl, newCmd =
        match msg with
        | ProjectSettings.ProjectLoaded proj ->
            let addMdl = { model.AddUnitModel with ProjectId = proj.Id }
            let model = { model with ShowSpinner = false; AddUnitModel = addMdl }
            let cmd = UnitsList.Msg.LoadUnitsForProject proj.Id |> UnitsListMsg |> Cmd.ofMsg
            let cmd2 = AddUnit.Msg.UpdateColumnSettings proj.ColumnSettings |> AddUnitMsg |> Cmd.ofMsg
            model, [ cmd; cmd2 ] |> Cmd.batch
        | ProjectSettings.UpdatedColumnSettings newProj ->
            let cmd1 = UnitsList.Msg.UpdatedColumnSettings newProj |> UnitsListMsg |> Cmd.ofMsg
            let cmd2 = AddUnit.Msg.UpdateColumnSettings newProj |> AddUnitMsg |> Cmd.ofMsg
            model, [ cmd1 ; cmd2 ] |> Cmd.batch
        | _ -> model, Cmd.none

    let cmds = [ (Cmd.map ProjectSettingsMsg setCmd); newCmd ] |> Cmd.batch
    { mdl with ProjectSettings = setMdl }, cmds

let handleAddUnitMsg (msg : AddUnit.Msg) (model : Model) =
    let addMdl, addCmd = AddUnit.update model.AddUnitModel msg

    let mdl, cmd =
       match msg with
       | AddUnit.AddNewUnit unit ->
            let msg = UnitsList.TryAddNewUnit unit
            model, Cmd.ofMsg (UnitsListMsg msg)
       | _ -> model, Cmd.none

    let cmds = [ (Cmd.map AddUnitMsg addCmd ); cmd] |> Cmd.batch
    { mdl with AddUnitModel = addMdl }, cmds

let handleUnitsListMsg (msg : UnitsList.Msg) (model : Model) =
    let unitsMdl, unitsCmd = UnitsList.update model.UnitsListModel msg
    let mdl,newMsg =
        match msg with
        | UnitsList.TryUpdatePriorities
        | UnitsList.TryAddNewUnit _
        //| UnitsList.TryDeleteRow _ - needs an appropriate success msg
        | UnitsList.LoadUnitsForProject _ ->
            { model with ShowSpinner = true }, Cmd.none
        //| UnitsList.UpdatePrioritiesResponse _
        | UnitsList.UpdatePrioritiesResponse2 _
        | UnitsList.UpdatePrioritiesFailure _
        | UnitsList.AddNewUnitResponse _
        | UnitsList.AddNewUnitFailure _
        | UnitsList.LoadUnitsResponse _
        | UnitsList.LoadUnitsFailure _ ->
            { model with ShowSpinner = false }, Cmd.none
        | _ -> model, Cmd.none

    let cmds = [ (Cmd.map UnitsListMsg unitsCmd); newMsg ] |> Cmd.batch

    { mdl with UnitsListModel = unitsMdl }, cmds

let handleProjectsListMsg (msg : ProjectsList.Msg) (model : Model) =
    let listMdl, listCmd = ProjectsList.update model.ProjectsListModel msg

    let mdl, newMsg =
        match msg with
        | ProjectsList.LoadAllProjects ->
            { model with ShowSpinner = true }, Cmd.none
        | ProjectsList.ProjectsLoadError _
        | ProjectsList.AllProjectsLoaded _ ->
            { model with ShowSpinner = false }, Cmd.none
        | ProjectsList.DefaultProjectCreated proj ->
            printfn "calling project lists update"
            let settingsCmd = Cmd.ofMsg (ProjectSettings.Msg.ProjectLoaded proj) |> Cmd.map ProjectSettingsMsg
            { model with ShowSpinner = false; ProjectsListModel = listMdl }, settingsCmd
        | ProjectsList.ProjectSelected proj ->
            let cmd = Cmd.ofMsg (ProjectSettings.Msg.MaybeLoadProject (Some proj.Id)) |> Cmd.map ProjectSettingsMsg
            { model with ShowSpinner = true }, cmd

    let cmds = [ (Cmd.map ProjectsListMsg listCmd); newMsg ] |> Cmd.batch
    { mdl with ProjectsListModel = listMdl }, cmds

let update (msg : Msg) (model : Model) : (Model * Cmd<Msg>)=
    printfn "in root update for msg %A" msg
    match msg with
    | Start ->
        let loadProjectsCmd = Cmd.ofMsg (ProjectsList.Msg.LoadAllProjects)
        { model with ShowSpinner = true }, Cmd.map ProjectsListMsg loadProjectsCmd
    | AddUnitMsg msg -> handleAddUnitMsg msg model
    | UnitsListMsg unitsListMsg ->
        handleUnitsListMsg unitsListMsg model
    | ProjectSettingsMsg projectSettingsMsg -> handleProjectSettingsMsg projectSettingsMsg model
    | ProjectsListMsg projListMsg -> handleProjectsListMsg projListMsg model


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


