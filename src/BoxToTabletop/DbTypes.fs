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
} with
    static member FromDomainType (projId : Guid) (unit : Domain.Types.Unit) : Unit =
        {
            id = unit.Id
            project_id = projId
            name = unit.Name
            models = unit.Models
            assembled = unit.Assembled
            primed = unit.Primed
            painted = unit.Painted
            based = unit.Based
        }

    member this.ToDomainType() : Domain.Types.Unit = {
        Domain.Types.Unit.Id = this.id
        Name = this.name
        Models = this.models
        Assembled = this.assembled
        Primed = this.primed
        Painted = this.painted
        Based = this.based
    }
