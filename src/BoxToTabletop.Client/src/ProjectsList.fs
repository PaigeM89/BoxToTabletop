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
} with
    static member Empty() = {
        Projects = []
        SelectedProject = None
        Config = Config.T.Default()
        DragAndDrop = DragAndDropModel.Empty()
        HoveredProject = None
    }

    static member Init(config : Config.T, dndModel : DragAndDropModel) = {
        Projects = []
        SelectedProject = None
        Config = config
        DragAndDrop = dndModel
        HoveredProject = None
    }
type ExternalMsg = 
| DndMsg of DragAndDropMsg
| DndModelChange of DragAndDropModel
| TransferUnitTo of projectId : Guid

type Msg =
| LoadAllProjects
| External of ExternalMsg
| AllProjectsLoaded of projects : Project list
| ProjectsLoadError of exn
/// If no projects are found, a default empty one is created
| DefaultProjectCreated of project : Project
/// A project has been selected from the projects menu
| ProjectSelected of project : Project
| OnHover of projectId : Guid

module View =

    let dragAndDropConfig = DragAndDropConfig.Empty()

    let onHover projId dispatch = (fun _ _ -> projId |> OnHover |> dispatch)
    let onDrop projId dispatch = (fun _ id -> printfn "Dropping %A on project" id; projId |> TransferUnitTo |> External |> dispatch)

    let createMenuItems (model : Model) dispatch =
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
                    Menu.Item.OnClick (fun ev -> ProjectSelected proj |> dispatch )
                ] [
                    ElementGenerator.Create (sprintf "%A-dnd" proj.Id) [] [] [str label]
                    |> DropArea.asBucket model.DragAndDrop dragAndDropConfig (fun _ _ -> ()) (onDrop proj.Id dispatch) (DndMsg >> External >> dispatch)
                ]

        [ for proj in model.Projects ->
            let isSelected = isSelectedProject proj.Id
            // let isHovered = isHoveredProject proj.Id
            menuItem proj.Name proj isSelected
        ]

    let view (model : Model) dispatch =
        Panel.panel [] [
            Panel.heading [] [ str "Projects" ]
            Panel.Block.div [] [
                Menu.menu [] [
                    Menu.list [] [
                        yield! createMenuItems model dispatch
                    ]
                ]
            ]
        ]


module ApiCalls =

    let loadAllProjects (model : Model) =
        let loadFunc() = Promises.loadAllProjects model.Config
        let cmd = Cmd.OfPromise.either loadFunc () AllProjectsLoaded ProjectsLoadError
        model, cmd

let update model msg =
    match msg with
    | LoadAllProjects -> ApiCalls.loadAllProjects model
    | External (DndMsg DragAndDropMsg.DragEnd) ->
        { model with HoveredProject = None}, Cmd.none
    | External (DndMsg dndMsg) ->
        model, Cmd.none
    | External (DndModelChange dndMdl ) ->
        { model with DragAndDrop = dndMdl}, Cmd.none
    | AllProjectsLoaded projects ->
        if List.length projects = 0 then
            let proj = Project.Empty()
            model, Cmd.ofMsg (DefaultProjectCreated proj)
        else
            { model with Projects = projects }, Cmd.none
    | ProjectsLoadError e ->
        printfn "%A" e
        model, Cmd.none
    | DefaultProjectCreated proj ->
        { model with Projects = proj :: model.Projects; SelectedProject = Some proj.Id }, Cmd.none
    | ProjectSelected proj ->
        { model with SelectedProject = Some (proj.Id) }, Cmd.none
    | OnHover projectId ->
        { model with HoveredProject = Some projectId }, Cmd.none
    | External (TransferUnitTo (newProjectId)) ->
        model, Cmd.none

