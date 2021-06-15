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

open Elmish
open Elmish.DragAndDrop

open Fulma
open Fulma.Extensions.Wikiki
open Thoth.Elmish
open BoxToTabletop.Client
open Elmish.React

// This gives weird sass errors for some reason (webpack using sass to load it?)
// Fable.Core.JsInterop.importAll "../node_modules/bulma-switch/dist/css/bulma-switch.min.css"

type SpinnerState = {
    Spin : bool
    Sources : Guid list
}

type ErrorMessage = 
| WithTitle of title : string * message : string
| JustMessage of message : string

type Model = {
    Config : Config.T
    LoginModel : Login.Model

    AddUnitModel : AddUnit.Model
    UnitsListModel : UnitsList.Model
    ProjectSettingsModel : ProjectSettings.Model
    ProjectsListModel : ProjectsList.Model
    ErrorMessages : Map<Guid, ErrorMessage>

    SpinnerState : SpinnerState
    IsProjectSectionCollapsed : bool
    IsProjectSelected : bool
    
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
            ProjectSettingsModel = ProjectSettings.Model.Init(config)
            ProjectsListModel = ProjectsList.Model.Init(config, dnd)
            ErrorMessages = Map.empty

            SpinnerState = { Spin = false; Sources = [] }
            IsProjectSectionCollapsed = false
            IsProjectSelected = false

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
                ProjectSettingsModel = ProjectSettings.Model.Init(this.Config)
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

let repopulateConfig (model : Model) = model.RepopulateConfig()

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
| RemoveErrorMessage of messageId : Guid
| RaiseToast // raises a toast based on the state of the model

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
                            Heading.CustomClass "pad-left-10"
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
                state.ProjectSettingsModel (fun (x : ProjectSettings.Msg) ->  ProjectSettingsMsg x |> dispatch)

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

    let errorMessages model dispatch =
        [
            for message in model.ErrorMessages do
                let key = message.Key
                let value = message.Value
                match value with
                | WithTitle (t, m) -> yield AlertMessage.renderError key t m (fun errorId -> RemoveErrorMessage errorId |> dispatch)
                | JustMessage m -> yield AlertMessage.renderError key "Error" m (fun errorId -> RemoveErrorMessage errorId |> dispatch)
        ]
        |> div []

    let view (model : Model) (dispatch : Msg -> unit)  =
        let projectView state =
            UnitsList.View.view state.UnitsListModel (fun (x : UnitsList.Msg) ->  UnitsListMsg x |> dispatch)

        let inputView model =
            AddUnit.View.view model.AddUnitModel (fun (x : AddUnit.Msg) -> AddUnitMsg x |> dispatch)

        let navbar = navbar model dispatch
        let dividerOption = divider model
        DragDropContext.context model.DragAndDrop (DndMsg >> dispatch) div [] [
            navbar
            Container.container [ Container.IsFluid ] [
                Columns.columns [ Columns.IsGap (Screen.All, Columns.Is1); Columns.IsGrid; ]
                    [
                        leftPanel model dispatch
                        if dividerOption.IsSome then Option.get dividerOption
                        if model.IsProjectSelected then
                            Column.column [  ] [
                                errorMessages model dispatch
                                inputView model
                                projectView model
                            ]
                        else
                            Column.column [] [
                                errorMessages model dispatch
                            ]
                    ]
            ]
        ]

let handleProjectSettingsMsg (msg : ProjectSettings.Msg) (model : Model) =
    let updateResponse = ProjectSettings.update model.ProjectSettingsModel msg

    let model =
        match updateResponse.spinner with
        | Some (SpinnerStart sourceId) -> addSpinStart sourceId model
        | Some (SpinnerEnd sourceId) -> removeSpin sourceId model
        | None -> model
    
    let mdl, cmd = 
        match updateResponse.raised with
        | Some (ProjectSettings.RaisedMsg.ProjectDeleted projectId) ->
            let cmd = ProjectsList.createProjectDeletedMsg projectId |> ProjectsListMsg |> Cmd.ofMsg
            model, cmd
        | Some (ProjectSettings.RaisedMsg.ProjectLoaded project) ->
            let addMdl = { model.AddUnitModel with ProjectId = project.Id }
            let model = { model with AddUnitModel = addMdl }
            let cmd = UnitsList.createProjectChangeMsg project |> UnitsListMsg |> Cmd.ofMsg
            let cmd2 = AddUnit.createProjectChangeMsg project |> AddUnitMsg |> Cmd.ofMsg
            model, [ cmd; cmd2 ] |> Cmd.batch
        | Some (ProjectSettings.RaisedMsg.UpdatedColumnSettings cs) ->
            let cmd = AddUnit.createColumnSettingsChangeMsg cs |> AddUnitMsg |> Cmd.ofMsg
            let cmd2 = UnitsList.createColumnChangeMsg cs |> UnitsListMsg |> Cmd.ofMsg
            model, [ cmd; cmd2 ] |> Cmd.batch
        | None -> model, Cmd.none
        

    let projectSettingsModel = updateResponse.model
    let projectSettingsCmd = updateResponse.cmd |> Cmd.map ProjectSettingsMsg

    let cmds = [ projectSettingsCmd; cmd ] |> Cmd.batch
    { mdl with ProjectSettingsModel = projectSettingsModel }, cmds

let handleAddUnitMsg (msg : AddUnit.Msg) (model : Model) =
    let updateResponse = AddUnit.update model.AddUnitModel msg
    let model =
        match updateResponse.spinner with
        | Some (SpinnerStart sourceId) -> addSpinStart sourceId model
        | Some (SpinnerEnd sourceId) -> removeSpin sourceId model
        | None -> model
    
    let mdl, cmd =
        match updateResponse.raised with
        | Some (AddUnit.NewUnitAdded unit) ->
            let cmd = UnitsList.createAddUnitMsg unit |> UnitsListMsg |> Cmd.ofMsg
            model, cmd
        | None -> model, Cmd.none
    
    let addMdl = updateResponse.model
    let addCmd = updateResponse.cmd |> Cmd.map AddUnitMsg

    { mdl with AddUnitModel = addMdl }, [ cmd; addCmd ] |> Cmd.batch

let handleUnitsListMsg (msg : UnitsList.Msg) (model : Model) =
    let updateResponse = UnitsList.update model.UnitsListModel msg

    let model = 
        match updateResponse.spinner with
        | Some (SpinnerStart sourceId) -> addSpinStart sourceId model
        | Some (SpinnerEnd sourceId) -> removeSpin sourceId model
        | None -> model

    let mdl, cmd =
        match updateResponse.raised with
        | Some (DndStart (dndMdl, unitId)) ->
            if unitId <> Guid.Empty then
                { model with DragAndDrop = dndMdl; DraggedUnitId = Some unitId }, Cmd.none
            else
                { model with DragAndDrop = dndMdl }, Cmd.none
        | Some DndEnd ->
            { model with DragAndDrop = DragAndDropModel.Empty(); DraggedUnitId = None }, Cmd.none
        | Some (ClearErrorMessage (messageId)) ->
            model |> clearMessageById messageId, Cmd.none
        | Some (LiftErrorMessage (title, message, messageId)) ->
            let msgId = messageId |> Option.defaultWith Guid.NewGuid
            let errMsg = (title, message) |> WithTitle
            model |> addErrorMessage msgId errMsg, Cmd.none
        | None -> model, Cmd.none

    let unitsCmd = updateResponse.cmd
    let unitsMdl = updateResponse.model

    let cmds = [ (Cmd.map UnitsListMsg unitsCmd); cmd ] |> Cmd.batch

    { mdl with UnitsListModel = unitsMdl }, cmds

let handleProjectsListMsg (msg : ProjectsList.Msg) (model : Model) =
    let updateResponse = ProjectsList.update model.ProjectsListModel msg

    let model =
        match updateResponse.spinner with
        | Some (SpinnerStart sourceId) -> addSpinStart sourceId model
        | Some (SpinnerEnd sourceId) -> removeSpin sourceId model
        | None -> model
    
    let mdl, cmd =
        match updateResponse.raised with
        | Some (ProjectsList.ProjectSelected project) ->
            let cmd = ProjectSettings.ExternalSourceMsg.ProjectSelected (project) |> ProjectSettings.External |> ProjectSettingsMsg |> Cmd.ofMsg
            let cmd2 = AddUnit.createProjectChangeMsg project |> AddUnitMsg |> Cmd.ofMsg
            let cmd3 = UnitsList.createProjectChangeMsg project |> UnitsListMsg |> Cmd.ofMsg
            let model = { model with IsProjectSelected = true }
            model, [ cmd; cmd2; cmd3 ] |> Cmd.batch
        | Some (ProjectsList.TransferUnitTo projectId) ->
            printfn "TODO: RE-IMPLEMENT THIS"
            model, Cmd.none
        | Some (ProjectsList.DndMsg dndMsg) ->
            printfn "TODO: RE-IMPLEMENT THIS"
            model, Cmd.none
        | Some (ProjectsList.ErrorMessage (title, message)) ->
            let model = addErrorMessage (Guid.NewGuid()) (WithTitle (title, message)) model
            model, Cmd.none
        | None -> model, Cmd.none
    
    let plCmd = updateResponse.cmd |> Cmd.map ProjectsListMsg
    let plModel = updateResponse.model

    { mdl with ProjectsListModel = plModel}, [ cmd; plCmd ] |> Cmd.batch

let handleLoginMsg (msg : Login.Msg) (model : Model) =
    let updateResponse = Login.update msg model.LoginModel

    let model =
        match updateResponse.spinner with
        | Some (SpinnerStart sourceId) -> addSpinStart sourceId model
        | Some (SpinnerEnd sourceId) -> removeSpin sourceId model
        | None -> model

    let mdl, cmd =
        match updateResponse.raised with
        | Some (Login.Auth0ConfigLoaded conf) ->
            let config = Config.withAuth0Config conf model.Config
            { model with Config = config }, Cmd.none
        | Some (Login.GotToken t) ->
            let config = Config.withToken t model.Config
            let cmd = Start |> Cmd.ofMsg
            let model = { model with Config = config } |> repopulateConfig
            model, cmd
        | Some (Login.RaiseError errorMsg) ->
            let err = WithTitle("Login Error", errorMsg)
            addErrorMessage (Guid.NewGuid()) err model, Cmd.none
        | None -> model, Cmd.none
    
    let loginMdl = updateResponse.model
    let loginCmd = updateResponse.cmd |> Cmd.map LoginMsg

    { mdl with LoginModel = loginMdl}, [ cmd; loginCmd ] |> Cmd.batch

let handleDndMsg (msg : DragAndDropMsg) (model : Model) =
    let updateResponse = UnitsList.update model.UnitsListModel (UnitsList.Msg.DndMsg (msg, None))

    let newDnd = updateResponse.model.DragAndDrop
    let unitListCmd = updateResponse.cmd
    let unitListMdl = updateResponse.model
    let projListCmd = ProjectsList.DndModelChange newDnd |> ProjectsList.External |> Cmd.ofMsg |> Cmd.map ProjectsListMsg

    let cmd = Cmd.map UnitsListMsg unitListCmd
    { model with UnitsListModel = unitListMdl; DragAndDrop = newDnd }, [cmd; projListCmd] |> Cmd.batch

open Fable.FontAwesome

let update (msg : Msg) (model : Model) =
    let maybeAddSpin cmd = cmd
        // if model.SpinnerState.Spin then
        //     let tcmd = Cmd.ofMsg RaiseToast
        //     [ cmd; tcmd ] |> Cmd.batch
        // else
        //     cmd

    match msg with
    | Start ->
        let loadProjectsCmd = Cmd.ofMsg (ProjectsList.Msg.LoadAllProjects)
        model, Cmd.map ProjectsListMsg loadProjectsCmd
    | LoginMsg loginMsg ->
        let model, cmd = handleLoginMsg loginMsg model
        model, (cmd |> maybeAddSpin)
    // | LoginMsg (Login.Msg.GetTokenSuccess token) ->
    //     let loginModel, loginCmd = Login.update (Login.Msg.GetTokenSuccess token) model.LoginModel
    //     let config = { model.Config with JwtToken = loginModel.JwtToken }
    //     let startCmd = Cmd.ofMsg Start
    //     let loginCmd = loginCmd  |> Cmd.map LoginMsg
    //     let model = { model with Config = config }
    //     let cmd = [ startCmd; loginCmd ] |> Cmd.batch |> maybeAddSpin
    //     (model.RepopulateConfig()), cmd
    // | LoginMsg loginMsg ->
    //     let loginModel, loginCmd = Login.update loginMsg model.LoginModel
    //     { model with LoginModel = loginModel }, (loginCmd |> Cmd.map LoginMsg) |> maybeAddSpin
    | DndMsg dndMsg ->
        let model, cmd = handleDndMsg dndMsg model
        model, (cmd |> maybeAddSpin)
    | AddUnitMsg msg -> 
        let model, cmd = handleAddUnitMsg msg model
        model, (cmd |> maybeAddSpin)
    | UnitsListMsg unitsListMsg ->
        let model, cmd = handleUnitsListMsg unitsListMsg model
        model, (cmd |> maybeAddSpin)
    | ProjectSettingsMsg projectSettingsMsg ->
        let model, cmd = handleProjectSettingsMsg projectSettingsMsg model
        model, (cmd |> maybeAddSpin)
    | ProjectsListMsg projListMsg ->
        let model, cmd = handleProjectsListMsg projListMsg model
        model, ( cmd |> maybeAddSpin)
    | ExpandProjectNav ->
        let model = { model with IsProjectSectionCollapsed = false }
        model, ( Cmd.none |> maybeAddSpin)
    | CollapseProjectNav ->
        let model = { model with IsProjectSectionCollapsed = true }
        model, ( Cmd.none |> maybeAddSpin)
    | RemoveErrorMessage errorId ->
        let m = model.ErrorMessages |> Map.remove errorId
        { model with ErrorMessages = m }, Cmd.none
    | RaiseToast ->
        let cmd = Cmd.none
            // todo: recreate & fix this upstream
        // if model.SpinnerState.Spin then
        //     let bldr =
        //         Toast.message "Loading..."
        //         |> Toast.icon (Fa.i [Fa.Solid.Spinner; Fa.Spin ] [])
        //         |> Toast.position Toast.TopRight
        //         |> Toast.timeout (TimeSpan.FromSeconds (2.0))
        //         |> Toast.title "title"
        //     let toastCmd = bldr |> Toast.info
        //     printfn "toast command is %A" toastCmd
        //     model, toastCmd
        // else model, Cmd.none
        model, cmd



let init (serverUrl : string option) =
    let config = 
        Config.T.Default()
#if DEBUG
        |> Config.withClientUrl "localhost:8092"
#else
        |> Config.withClientUrl "https://boxtotabletop.com"
#endif
    let config =
        match serverUrl with
        | Some url -> Config.withServerUrl url config
        | None -> config
    let model = Model.InitWithConfig config
    
    let cmd = Login.InitLoginProcess config |> LoginMsg |> Cmd.ofMsg

    { model with Config = config}, cmd

Program.mkProgram
    init
    update
    View.view
// |> Program.withConsoleTrace
// |> Toast.Program.withToast Toast.renderToastWithFulma
|> Program.withReactBatched "root"
#if DEBUG
|> Program.runWith (Some "http://localhost:5000")
#else
// in release mode, point to the server
|> Program.runWith (Some "https://api.boxtotabletop.com")
    //(Some "http://localhost:8092")
#endif


