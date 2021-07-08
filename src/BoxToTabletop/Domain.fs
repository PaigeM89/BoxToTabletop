namespace BoxToTabletop.Domain

open System
open System.Threading
open System.Threading.Tasks
open BoxToTabletop
open BoxToTabletop.Repository
open FsToolkit.ErrorHandling
open FSharp.Control.Tasks.Affine

module Project =

  let getAllProjectsForUser (loader : ILoadProjects) (userId : string) = task {
    let! projects = loader.LoadForUser userId
    return projects |> List.map (fun x -> x.ToDomainType())
  }

  let loadProject (loader : ILoadProjects) (projectId : Guid) (userId : string) = task {
    // todo: For sharing, this should not match owner Id, since the viewer won't own the project
    let! project = loader.Load projectId
    match project with
    | Some proj when proj.owner_id = userId ->
      let! columns = loader.LoadColumnsForProject projectId
      let columns = columns |> Projections.createProjectColumn |> List.ofSeq
      let proj = { proj.ToDomainType() with Columns = columns }
      return proj |> Some
    | _ -> return None
  }

  let saveNewProject (saver : IModifyProjects) (loader : ILoadProjects) userId (project : Types.Project) = task {
    let! existing = loader.Load project.Id
    match existing with
    | None ->
      let project = {project with OwnerId = userId}
      let! saved = DbTypes.Project.FromDomainType {project with OwnerId = userId} |> saver.Save
      return Some (saved, project)
    | Some _ ->
      return None
  }

  let updateProject (saver : IModifyProjects) (loader : ILoadProjects) userId (project : Types.Project) = task {
    let! existing = loader.Load project.Id
    match existing with
    | None ->
      let project = {project with OwnerId = userId}
      let! saved = DbTypes.Project.FromDomainType project |> saver.Save
      return Some (false, project)
    | Some proj when proj.owner_id = userId ->
      let! saved = DbTypes.Project.FromDomainType project |> saver.Update
      return Some (true, project)
    | _ ->
      return None
  }

  type IProjectDomain =
    abstract member GetAllProjectsForUser : Types.UserId -> Task<List<Types.Project>>
    abstract member LoadProject : Types.UserId -> Guid -> Task<Types.Project option>
    abstract member SaveNewProject : Types.UserId -> Types.Project -> Task<Option<int * Types.Project>>
    abstract member UpdateProject : Types.UserId -> Types.Project -> Task<Option<bool * Types.Project>>

  type ProjectDomain(saver : IModifyProjects, loader : ILoadProjects) =
    interface IProjectDomain with
      member this.GetAllProjectsForUser userId = getAllProjectsForUser loader userId
      member this.LoadProject userId projectId = loadProject loader projectId userId
      member this.SaveNewProject userId project = saveNewProject saver loader userId project
      member this.UpdateProject userId project = updateProject saver loader userId project

module Unit =
  open System
  open BoxToTabletop
  open BoxToTabletop.Repository
  open FsToolkit.ErrorHandling
  open FSharp.Control.Tasks.Affine

  let getAllUnitsForProject (loader: ILoadUnits) userId (projectId : Guid) = task {
    let! units = loader.LoadForProject projectId
    return 
      units
      |> List.filter (fun u -> u.owner_id = userId)
      |> List.map (fun x -> x.ToDomainType())
  }

  let loadUnit (loader : ILoadUnits) (userId : string) (unitId : Guid) = task {
    let! unit = loader.Load unitId
    match unit with
    | Some unit when unit.owner_id = userId -> return unit.ToDomainType() |> Some
    | _ -> return None
  }

  let saveNewUnit (saver : IModifyUnits) (loader : ILoadUnits) userId (unit : Types.Unit)  = task {
    let! existing = loader.Load unit.Id
    match existing with
    | None ->
      let unit = { unit with OwnerId = userId }
      let! rowCount = DbTypes.Unit.FromDomainType unit |> saver.Save
      return Some(rowCount, unit)
    | Some _ -> return None
  }

  let updateUnit (saver : IModifyUnits) (loader : ILoadUnits) userId (unit : Types.Unit) = task {
    match! loader.Load unit.Id with
    | None ->
      let unit = { unit with OwnerId = userId }
      let! _ = DbTypes.Unit.FromDomainType unit |> saver.Save
      return Some (false, unit)
    | Some u when u.owner_id = userId ->
      let! _ = DbTypes.Unit.FromDomainType unit |> saver.Update
      return Some (true, unit)
    | _ -> return None
  }

  let updateUnits  (saver : IModifyUnits) (loader : ILoadUnits) userId (units : Types.Unit list) = task {
    let updateFunc u = updateUnit saver loader userId u
    let updates = units |> List.map updateFunc
    let! updates = updates |> Task.WhenAll
    return (updates |> Array.choose id |> List.ofArray)
  }

  let transferUnit (saver : IModifyUnits) userId (unit : Types.Unit) projectId = task {
    let unit = { unit with ProjectId = projectId }
    let! _ = DbTypes.Unit.FromDomainType unit |> saver.Save
    return unit
  }

  type IUnitDomain =
    abstract member GetAllUnitsForProject : Types.UserId -> Guid -> Task<Types.Unit list>
    abstract member LoadUnit : Types.UserId -> Guid -> Task<Types.Unit option>
    abstract member SaveNewUnit : Types.UserId -> Types.Unit -> Task<Option<int * Types.Unit>>
    abstract member UpdateUnit : Types.UserId -> Types.Unit ->  Task<Option<bool * Types.Unit>>
    abstract member UpdateUnits : Types.UserId -> Types.Unit list -> Task<List<bool * Types.Unit>>
    abstract member TransferUnit : Types.UserId -> Types.Unit -> Guid -> Task<Types.Unit>

  type UnitDomain(loader : ILoadUnits, saver : IModifyUnits) =
    interface IUnitDomain with
      member this.GetAllUnitsForProject userId projId = getAllUnitsForProject loader userId projId
      member this.LoadUnit userId unitId = loadUnit loader userId unitId
      member this.SaveNewUnit userId unit = saveNewUnit saver loader userId unit
      member this.UpdateUnit userId unit = updateUnit saver loader userId unit
      member this.UpdateUnits userId units = updateUnits saver loader userId units
      member this.TransferUnit userId unit projectId = transferUnit saver userId unit projectId