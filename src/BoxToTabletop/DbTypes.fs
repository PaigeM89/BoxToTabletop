module BoxToTabletop.DbTypes

open System

type Unit = {
    Id : Guid
    ProjectId : Guid
    Name : string
    Models : int
    Assembled : int
    Primed : int
    Painted : int
    Based : int
} with
    static member FromDomainType (projId : Guid) (unit : Domain.Types.Unit) : Unit =
        {
            Id = unit.Id
            ProjectId = projId
            Name = unit.Name
            Models = unit.Models
            Assembled = unit.Assembled
            Primed = unit.Primed
            Painted = unit.Painted
            Based = unit.Based
        }

