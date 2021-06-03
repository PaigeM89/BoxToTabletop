module Root

open System
open System.ComponentModel
open App
open BoxToTabletop.Client.UnitsList
open BoxToTabletop.Domain.Routes
open BoxToTabletop.Domain.Types
open BoxToTabletop.Client.Core
open Browser.Types
open Fable.React
open Fable.Reaction
open Elmish
open Elmish.DragAndDrop
open FSharp.Control
open Fulma
open Fulma.Extensions.Wikiki
open BoxToTabletop.Client
open Elmish.React

type SpinnerState = {
    Spin : bool
    Sources : Guid list
}

type Model = {
    Config : Config.T
    LoginModel : Login.Model

    AddUnitModel : AddUnit.Model
    UnitsListModel : UnitsList.Model
    ProjectSettings : ProjectSettings.Model
    ProjectsListModel : ProjectsList.Model
    ErrorMessages : Map<Guid, string>

    SpinnerState : SpinnerState
    IsProjectSectionCollapsed : bool
    
    DragAndDrop : DragAndDropModel
    DraggedUnitId : Guid option
} with
    static member InitWithConfig (config : Config.T) = 
        let dnd = DragAndDropModel.Empty()
        {
            Config = config
            LoginModel = Login.Model.Empty()
            AddUnitModel = AddUnit.Model.Init(config)
            UnitsListModel = UnitsList.Model.Init(config, dnd)
            ProjectSettings = ProjectSettings.Model.Init(config)
            ProjectsListModel = ProjectsList.Model.Init(config, dnd)
            ErrorMessages = Map.empty

            SpinnerState = { Spin = false; Sources = [] }
            IsProjectSectionCollapsed = false

            DragAndDrop = dnd
            DraggedUnitId = None
        }

    static member Empty() =
        Config.T.Default() |> Model.InitWithConfig
    
    member this.RepopulateConfig() = 
        let dnd = DragAndDropModel.Empty()
        {
            this with
                AddUnitModel = AddUnit.Model.Init(this.Config)
                UnitsListModel = UnitsList.Model.Init(this.Config, dnd)
                ProjectSettings = ProjectSettings.Model.Init(this.Config)
                ProjectsListModel = ProjectsList.Model.Init(this.Config, dnd)
        }

let addErrorMessage messageId message model =
    let msgs = model.ErrorMessages |> Map.add messageId message
    { model with ErrorMessages = msgs}

let clearMessageById messageId model =
    let msgs = model.ErrorMessages |> Map.remove messageId
    { model with ErrorMessages = msgs}

let addSpinStart source (model : Model) =
    let spin = { model.SpinnerState with Spin = true; Sources = source :: model.SpinnerState.Sources }
    { model with SpinnerState = spin }

let removeSpin source (model : Model) =
    let sources = model.SpinnerState.Sources |> List.filter (fun x -> x <> source)
    let spin = List.isEmpty sources |> not
    let newSpinState = { Spin = spin; Sources = sources}
    { model with SpinnerState = newSpinState }

type Msg =
| Start
| LoginMsg of Login.Msg
| DndMsg of DragAndDropMsg
| AddUnitMsg of AddUnit.Msg
| UnitsListMsg of UnitsList.Msg
| ProjectSettingsMsg of ProjectSettings.Msg
| ProjectsListMsg of ProjectsList.Msg
| ExpandProjectNav
| CollapseProjectNav

module View =
    open Fable.React.Props
    open Fable.FontAwesome

    let mapShowSpinner (model : Model) =
        match model.SpinnerState.Spin with
        | true ->
            div [ Class ("block " + Fa.Classes.Size.Fa3x) ] [
                Fa.i [ Fa.Solid.Spinner; Fa.Spin ] []
            ] |> Some
        | false -> None

    let tryShowUserInfo (model : Model) =
        match model.LoginModel.User with
        | Some user ->
            let display = Option.defaultValue "Unknown user" user.GivenName
            Label.label [] [ str ("Welcome, " + display) ]
        | None ->
            Label.label [] [ str ("Welcome! Please log in to use the site. ") ]

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
                            Heading.Modifiers [ 
                                Modifier.Spacing (Spacing.PaddingLeft, Spacing.Is5)
                            ]
                        ] [
                            str "Box To Tabletop"
                        ]
                    ]
                ]
            ]
            Navbar.Item.div [] [
                tryShowUserInfo model
            ]
            Navbar.End.div [] [
                if Option.isSome spinner then Navbar.Item.div [] [ Option.get spinner ]
                if model.LoginModel.User.IsSome then
                    Navbar.Item.div [] [
                        Button.button
                            [ Button.OnClick (fun _ -> Login.Msg.TryLogout |> LoginMsg |> dispatch)]
                            [ str "Log Out" ]
                    ]
                else
                    Navbar.Item.div [] [
                        Button.button
                            [ Button.OnClick (fun _ -> Login.Msg.TryLogin |> LoginMsg |> dispatch)]
                            [ str "Log In" ]
                    ]
                Navbar.Item.div [] [
                    Control.div [ ] [
                        Button.a [ Button.Props [ Href "https://github.com/PaigeM89/BoxToTabletop" ] ] [
                            Icon.icon [] [
                                Fa.i [ Fa.Brand.Github ] []
                            ]
                            span [] [ str "Github" ]
                        ]
                    ]
                ]
            ]
        ]

    let leftPanel (model : Model) dispatch =
        let projectSettingsView state =
            ProjectSettings.View.view
                state.ProjectSettings (fun (x : ProjectSettings.Msg) ->  ProjectSettingsMsg x |> dispatch)

        if model.IsProjectSectionCollapsed then
            Column.column [ Column.Width (Screen.All, Column.Is1) ] [
                Button.button [ Button.Color Color.IsInfo; Button.IsRounded; Button.IsExpanded; Button.OnClick (fun _ -> ExpandProjectNav |> dispatch)  ] [
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
                        Button.OnClick (fun _ -> CollapseProjectNav |> dispatch)
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
        DragDropContext.context model.DragAndDrop (DndMsg >> dispatch) div [] [
            navbar
            //AlertMessage.page()
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

let handleProjectSettingsMsg (msg : ProjectSettings.Msg) (model : Model) =
    let setMdl, setCmd = ProjectSettings.update model.ProjectSettings msg

    let mdl, newCmd =
        match msg with
        | ProjectSettings.ProjectLoaded (Ok proj) ->
            /// TODO: Clean this up, make it spin
            let addMdl = { model.AddUnitModel with ProjectId = proj.Id }
            let model = { model with AddUnitModel = addMdl } // ShowSpinner = false;
            let cmd2 = UnitsList.Msg.External (UnitsList.ExternalMsg.ProjectChange proj) |> UnitsListMsg |> Cmd.ofMsg
            let cmd3 = AddUnit.Msg.UpdateColumnSettings proj.ColumnSettings |> AddUnitMsg |> Cmd.ofMsg
            model, [ cmd2; cmd3 ] |> Cmd.batch
        | ProjectSettings.ProjectLoaded (Error _) ->
            model, Cmd.none //{ model with ShowSpinner = false }
        | ProjectSettings.UpdatedColumnSettings cs ->
            let cmd1 = UnitsList.Msg.External (UnitsList.ExternalMsg.ColumnSettingsChange cs) |> UnitsListMsg |> Cmd.ofMsg
            let cmd2 = AddUnit.Msg.UpdateColumnSettings cs |> AddUnitMsg |> Cmd.ofMsg
            model, [ cmd1 ; cmd2 ] |> Cmd.batch
        | _ -> model, Cmd.none

    let cmds = [ (Cmd.map ProjectSettingsMsg setCmd); newCmd ] |> Cmd.batch
    { mdl with ProjectSettings = setMdl }, cmds

let handleAddUnitMsg (msg : AddUnit.Msg) (model : Model) =
    let addMdl, addCmd = AddUnit.update model.AddUnitModel msg

    let mdl, cmd =
        match msg with
        | AddUnit.AddNewUnit unit ->
            let msg = UnitsList.ExternalMsg.AddNewUnit unit |> UnitsList.External
            model, Cmd.ofMsg (UnitsListMsg msg)
        | _ -> model, Cmd.none

    let cmds = [ (Cmd.map AddUnitMsg addCmd ); cmd] |> Cmd.batch
    { mdl with AddUnitModel = addMdl }, cmds

let handleUnitsListMsg (msg : UnitsList.Msg) (model : Model) =
    let unitsMdl, unitsCmd = UnitsList.update model.UnitsListModel msg
    let mdl,newMsg =
        match msg with
        | External (DndStart (dnd, unitId)) ->
            if unitId <> Guid.Empty then
                { model with DragAndDrop = dnd; DraggedUnitId = Some unitId}, Cmd.none
            else
                { model with DragAndDrop = dnd}, Cmd.none
        | External (DndEnd) ->
            { model with DragAndDrop = DragAndDropModel.Empty(); DraggedUnitId = None }, Cmd.none
        | External (SpinnerStart sourceId) ->
            let model = addSpinStart sourceId model
            model, Cmd.none
        | External (SpinnerEnd sourceId) ->
            let model = removeSpin sourceId model
            model, Cmd.none
        | External (LogErrorMessage (src, msg)) ->
            // todo: log errors here
            Fable.Core.JS.console.error("Error: ", src, msg)
            model, Cmd.none
        | External (LiftErrorMessage (messageId, message)) ->
            let msgId = messageId |> Option.defaultValue (Guid.NewGuid())
            let mdl = addErrorMessage msgId message model
            mdl, Cmd.none
        | External (ClearErrorMessage messageId) ->
            let mdl = clearMessageById messageId model
            mdl, Cmd.none
        | _ -> model, Cmd.none

    let cmds = [ (Cmd.map UnitsListMsg unitsCmd); newMsg ] |> Cmd.batch

    { mdl with UnitsListModel = unitsMdl }, cmds

let handleProjectsListMsg (msg : ProjectsList.Msg) (model : Model) =
    let listMdl, listCmd = ProjectsList.update model.ProjectsListModel msg

    let projectLoadSpinId = Guid.Parse("1DE04F5F-9C92-47A4-9202-ADE3D89D4B22")
    let addSpin() = addSpinStart projectLoadSpinId model
    let removeSpin() = removeSpin projectLoadSpinId model

    let mdl, newMsg =
        match msg with
        | ProjectsList.LoadAllProjects ->
            let mdl = addSpin()
            mdl, Cmd.none
        | ProjectsList.External (ProjectsList.DndMsg dndMsg) ->
            model, Cmd.none
        | ProjectsList.ProjectsLoadError _
        | ProjectsList.AllProjectsLoaded _ ->
            let mdl = removeSpin()
            mdl, Cmd.none
        | ProjectsList.DefaultProjectCreated proj ->
            let settingsCmd = Cmd.ofMsg (ProjectSettings.Msg.ProjectLoaded (Ok proj)) |> Cmd.map ProjectSettingsMsg
            let mdl = removeSpin()
            { mdl with ProjectsListModel = listMdl }, settingsCmd
        | ProjectsList.ProjectSelected proj ->
            let cmd = Cmd.ofMsg (ProjectSettings.Msg.MaybeLoadProject (Some proj.Id)) |> Cmd.map ProjectSettingsMsg
            model, cmd
        | ProjectsList.External (ProjectsList.TransferUnitTo projectId) ->
            match model.DraggedUnitId with
            | Some unitId ->
                printfn "transferring unit %A to project %A" unitId projectId
                let cmd = UnitsList.TransferUnitTo (unitId, projectId) |> UnitsList.External |> UnitsListMsg |> Cmd.ofMsg
                model, cmd
            | None ->
                printfn "Tried to transfer a unit to project %A but did not find a unit Id in the model" projectId
                model, Cmd.none
        | ProjectsList.OnHover _
        | ProjectsList.External _ ->
            model, Cmd.none

    let cmds = [ (Cmd.map ProjectsListMsg listCmd); newMsg ] |> Cmd.batch
    { mdl with ProjectsListModel = listMdl }, cmds

let handleDndMsg (msg : DragAndDropMsg) (model : Model) =
    let unitListMdl, unitListCmd = UnitsList.update model.UnitsListModel (UnitsList.Msg.DndMsg (msg, None))

    let newDnd = unitListMdl.DragAndDrop
    let projListCmd = ProjectsList.DndModelChange newDnd |> ProjectsList.External |> Cmd.ofMsg |> Cmd.map ProjectsListMsg

    let cmd = Cmd.map UnitsListMsg unitListCmd
    { model with UnitsListModel = unitListMdl; DragAndDrop = newDnd }, [cmd; projListCmd] |> Cmd.batch

let update (msg : Msg) (model : Model) : (Model * Cmd<Msg>) =
    match msg with
    | Start ->
        let loadProjectsCmd = Cmd.ofMsg (ProjectsList.Msg.LoadAllProjects)
        model, Cmd.map ProjectsListMsg loadProjectsCmd
    | LoginMsg (Login.Msg.GetTokenSuccess token) ->
        let loginModel, loginCmd = Login.update (Login.Msg.GetTokenSuccess token) model.LoginModel
        let config = { model.Config with JwtToken = loginModel.JwtToken }
        let startCmd = Cmd.ofMsg Start
        let loginCmd = loginCmd  |> Cmd.map LoginMsg
        let model = { model with Config = config }
        (model.RepopulateConfig()), [ startCmd; loginCmd ] |> Cmd.batch
    | LoginMsg loginMsg ->
        let loginModel, loginCmd = Login.update loginMsg model.LoginModel
        { model with LoginModel = loginModel }, (loginCmd  |> Cmd.map LoginMsg)
    | DndMsg dndMsg ->
        handleDndMsg dndMsg model
    | AddUnitMsg msg -> handleAddUnitMsg msg model
    | UnitsListMsg unitsListMsg ->
        handleUnitsListMsg unitsListMsg model
    | ProjectSettingsMsg projectSettingsMsg -> handleProjectSettingsMsg projectSettingsMsg model
    | ProjectsListMsg projListMsg -> handleProjectsListMsg projListMsg model
    | ExpandProjectNav ->
        { model with IsProjectSectionCollapsed = false }, Cmd.none
    | CollapseProjectNav ->
        { model with IsProjectSectionCollapsed = true }, Cmd.none


let init (url : string option) =
    let config = Config.T.Default()
    let config =
        match url with
        | Some url -> Config.withServerUrl url config
        | None -> config
    let model = Model.InitWithConfig config
    
    let cmd = Login.createClient() |> Cmd.map LoginMsg

    { model with Config = config}, cmd

Program.mkProgram
    init
    update
    View.view
// |> Program.withConsoleTrace
|> Program.withReactBatched "root"
#if DEBUG
|> Program.runWith (Some "http://localhost:5000")
#else
// in release mode, point to nginx
|> Program.runWith (Some "http://localhost:8092")
#endif


