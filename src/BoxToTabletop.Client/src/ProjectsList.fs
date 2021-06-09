module App.ProjectsList

open System
open BoxToTabletop.Domain.Types
open Fulma
open Fable.React
open BoxToTabletop.Client
open Elmish
open Elmish.DragAndDrop

type Model = {
    Projects : Project list
    SelectedProject : Guid option
    Config : Config.T
    DragAndDrop : DragAndDropModel 
    HoveredProject : Guid option
    NewProjectName : string
} with
    static member Empty() = {
        Projects = []
        SelectedProject = None
        Config = Config.T.Default()
        DragAndDrop = DragAndDropModel.Empty()
        HoveredProject = None
        NewProjectName = ""
    }

    static member Init(config : Config.T, dndModel : DragAndDropModel) = {
        Projects = []
        SelectedProject = None
        Config = config
        DragAndDrop = dndModel
        HoveredProject = None
        NewProjectName = ""
    }

/// Messages that are raised externally and handled here.
type ExternalSourceMsg =
| DndModelChange of DragAndDropModel
| ProjectDeleted of projectId : Guid
| DragAndDropMsg of DragAndDropMsg

type RaisedMsg =
| ProjectSelected of project : Project
| TransferUnitTo of projectId : Guid
| DndMsg of DragAndDropMsg
| ErrorMessage of title: string * message: string


type Msg =
| External of ExternalSourceMsg
| TransferUnit of projectId : Guid
| LoadAllProjects
| AllProjectsLoaded of projects : Project list
| ProjectsLoadError of exn
/// A project has been selected from the projects menu
| Selected of project : Project
| OnHover of projectId : Guid
| NewProjectNameUpdate of value : string
| CreateNewProject
| SaveProjectSuccess of Project
| SaveProjectError of exn

let createProjectDeletedMsg id = ProjectDeleted id |> External

module View =
    open Fable.FontAwesome

    let dragAndDropConfig = DragAndDropConfig.Empty()

    let onHover projId dispatch = (fun _ _ -> projId |> OnHover |> dispatch)
    let onDrop projId dispatch = (fun _ id -> printfn "Dropping %A on project" id; projId |> TransferUnit |> dispatch)

    let createMenuItems (model : Model) (dispatch : Msg -> unit) =
        let isSelectedProject (input : Guid) =
            match model.SelectedProject with
            | None -> false
            | Some g -> g = input
        let isHoveredProject (input : Guid) =
            match model.HoveredProject with
            | None -> false
            | Some x -> x = input
        let menuItem label proj isSelected =
            Menu.Item.li
                [
                    Menu.Item.IsActive isSelected
                    Menu.Item.OnClick (fun _ -> Selected proj |> dispatch )
                ] [
                    ElementGenerator.Create (sprintf "%A-dnd" proj.Id) [] [] [str label]
                    |> DropArea.asBucket model.DragAndDrop dragAndDropConfig (fun _ _ -> ()) (onDrop proj.Id dispatch) (DragAndDropMsg >> External >> dispatch)
                ]

        [ for proj in model.Projects ->
            let isSelected = isSelectedProject proj.Id
            // let isHovered = isHoveredProject proj.Id
            menuItem proj.Name proj isSelected
        ]

    let detectEnterKey (kev : Browser.Types.KeyboardEvent) dispatch =
        if kev.key = "Enter" then
            CreateNewProject |> dispatch
        else
            ()

    let view (model : Model) (dispatch : Msg -> unit) =
        Panel.panel [] [
            Panel.heading [] [ str "Projects" ]
            Panel.Block.div [] [
                Menu.menu [] [
                    Menu.list [] [
                        yield! createMenuItems model dispatch
                        Menu.Item.li [
                                Menu.Item.OnClick(fun ev -> ev.preventDefault() )
                                Menu.Item.Option.Modifiers [ Modifier.Display (Screen.All, Display.Flex) ]
                        ] [
                            Input.input [ 
                                Input.Option.Placeholder "New Project"; 
                                Input.OnChange (fun ev -> ev.Value |> NewProjectNameUpdate |> dispatch); 
                                Input.Props [ Props.OnKeyPress(fun k -> detectEnterKey k dispatch) ] ]
                            Button.button [
                                Button.OnClick (fun _ -> CreateNewProject |> dispatch )
                            ] [ Fa.i [ Fa.Solid.Plus ] []  ]
                        ]
                    ]
                ]
            ]
        ]

type UpdateResponse = Core.UpdateResponse<Model, Msg, RaisedMsg>
let projectListSpinnerId = Guid.Parse("BEE7A043-3F8D-4959-ADB6-6C10706D0E32")

module ApiCalls =

    let loadAllProjects (model : Model) =
        let loadFunc() = Promises.loadAllProjects model.Config
        let cmd = Cmd.OfPromise.either loadFunc () AllProjectsLoaded ProjectsLoadError
        model, cmd

    let saveNewProject (model : Model) project =
        let save = Promises.saveProject model.Config
        let cmd = Cmd.OfPromise.either save project SaveProjectSuccess SaveProjectError
        model, cmd

let handleExternalSourceMsg model msg =
    match msg with
    | ProjectDeleted projectId ->
        let projects = model.Projects |> List.filter (fun x -> x.Id = projectId)
        let selectedProject =
            match model.SelectedProject with
            | Some p when p = projectId -> None
            | _ -> model.SelectedProject
        ({ model with Projects = projects; SelectedProject = selectedProject}, Cmd.none)
    | DndModelChange dndmdl ->
        ({ model with DragAndDrop = dndmdl }, Cmd.none)
    | DragAndDropMsg (DragAndDropMsg.DragEnd) ->
        ({ model with HoveredProject = None }, Cmd.none)
    | DragAndDropMsg _ ->
        (model, Cmd.none)

let update model msg =
    match msg with
    | External ext -> 
        let model, cmd = handleExternalSourceMsg model ext
        UpdateResponse.basic model cmd
    | LoadAllProjects -> 
        let model, cmd = ApiCalls.loadAllProjects model
        UpdateResponse.withSpin model cmd (Core.SpinnerStart projectListSpinnerId)
    | AllProjectsLoaded projects ->
        if List.length projects = 0 then
            UpdateResponse.withSpin model Cmd.none (Core.SpinnerEnd projectListSpinnerId)
        else
            let model = { model with Projects = projects }
            UpdateResponse.withSpin model Cmd.none (Core.SpinnerEnd projectListSpinnerId)
    | ProjectsLoadError e ->
        printfn "Error loading projects: %A" e
        let raised = ErrorMessage ("Error loading projects", "There was an error loading your projects.")
        let spin = Some (Core.SpinnerEnd projectListSpinnerId)
        UpdateResponse.create model Cmd.none spin (Some raised)
    | Selected proj ->
        let model = { model with SelectedProject = Some (proj.Id) }
        let raised = ProjectSelected proj
        UpdateResponse.withRaised model Cmd.none raised
    | OnHover projectId ->
        let model = { model with HoveredProject = Some projectId }
        UpdateResponse.basic model Cmd.none
    | NewProjectNameUpdate value ->
        let model = {model with NewProjectName = value}
        UpdateResponse.basic model Cmd.none
    | CreateNewProject ->
        let newProj = { Project.Empty() with Name = model.NewProjectName; Id = Guid.NewGuid() }
        let model, cmd = ApiCalls.saveNewProject model newProj
        UpdateResponse.withSpin model cmd (Core.SpinnerStart projectListSpinnerId)
    | SaveProjectSuccess proj ->
        let projects = proj :: model.Projects
        let model = { model with Projects = projects; NewProjectName = ""; SelectedProject = Some proj.Id }
        let raised = RaisedMsg.ProjectSelected proj
        UpdateResponse.create model Cmd.none (Some (Core.SpinnerEnd projectListSpinnerId)) (Some raised)
    | SaveProjectError e ->
        printfn "Save new project error: %A" e
        UpdateResponse.withSpin model Cmd.none (Core.SpinnerEnd projectListSpinnerId)
    | TransferUnit(projectId) ->
        let raised = TransferUnitTo projectId
        UpdateResponse.withRaised model Cmd.none raised

