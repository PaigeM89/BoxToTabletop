module App.ProjectsList

open System
open BoxToTabletop.Domain.Types
open Fulma
open Fable.React
open Thoth
open BoxToTabletop.Client
open Elmish

type Model = {
    Projects : Project list
    SelectedProject : Guid option
    Config : Config.T
} with
    static member Empty() = {
        Projects = []
        SelectedProject = None
        Config = Config.T.Default()
    }

    static member Init(config : Config.T) = {
        Projects = []
        SelectedProject = None
        Config = config
    }

type Msg =
| LoadAllProjects
| AllProjectsLoaded of projects : Project list
| ProjectsLoadError of exn
/// If no projects are found, a default empty one is created
| DefaultProjectCreated of project : Project
/// A project has been selected from the projects menu
| ProjectSelected of project : Project

module View =

    let createMenuItems (model : Model) dispatch =
        let isSelectedProject (input : Guid) =
            match model.SelectedProject with
            | None -> false
            | Some g -> g = input
        let menuItem label isSelected proj =
            Menu.Item.li
                [
                    Menu.Item.IsActive isSelected
                    Menu.Item.OnClick (fun ev -> ProjectSelected proj |> dispatch )
                ] [
                    str label
                ]

        [ for proj in model.Projects ->
            let isSelected = isSelectedProject proj.Id
            if isSelected then
                menuItem proj.Name true proj
            else
                menuItem proj.Name false proj
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
    printfn "in project list update"
    match msg with
    | LoadAllProjects -> ApiCalls.loadAllProjects model
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
        printfn "in default project created in projects list"
        { model with Projects = proj :: model.Projects; SelectedProject = Some proj.Id }, Cmd.none
    | ProjectSelected proj ->
        { model with SelectedProject = Some (proj.Id) }, Cmd.none
