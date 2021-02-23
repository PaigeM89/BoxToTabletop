module BoxToTabletop.DbTypes

open System

type Unit = {
    id : Guid
    project_id : Guid
    name : string
    models : int
    assembled : int
    primed : int
    painted : int
    based : int
    priority : int
} with
    static member FromDomainType (unit : Domain.Types.Unit) : Unit =
        {
            id = unit.Id
            project_id = unit.ProjectId
            name = unit.Name
            models = unit.Models
            assembled = unit.Assembled
            primed = unit.Primed
            painted = unit.Painted
            based = unit.Based
            priority = 0
        }

    member this.ToDomainType() : Domain.Types.Unit = {
        Domain.Types.Unit.Id = this.id
        ProjectId = this.project_id
        Name = this.name
        Models = this.models
        Assembled = this.assembled
        Primed = this.primed
        Painted = this.painted
        Based = this.based
        Priority = this.priority
    }

type Project = {
    id : Guid
    name : string
    is_public : bool
    assembled_visible : bool
    primed_visible : bool
    painted_visible : bool
    based_visible : bool
} with
    static member FromDomainType (project : Domain.Types.Project) : Project =
        {
            id = project.Id
            name = project.Name
            is_public = project.IsPublic
            assembled_visible = project.ColumnSettings.AssemblyVisible
            primed_visible = project.ColumnSettings.PrimedVisible
            painted_visible = project.ColumnSettings.PaintedVisible
            based_visible = project.ColumnSettings.BasedVisible
        }

    member this.ToDomainType() : Domain.Types.Project = {
        Domain.Types.Project.Id = this.id
        Name = this.name
        IsPublic = this.is_public
        ColumnSettings = {
            AssemblyVisible = this.assembled_visible
            PrimedVisible = this.primed_visible
            PaintedVisible = this.painted_visible
            BasedVisible = this.based_visible
        }
        //Category = None
        Units = []
    }

