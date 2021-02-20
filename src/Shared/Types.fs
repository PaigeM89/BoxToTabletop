namespace BoxToTabletop.Domain

open System

module Types =
    type Unit = {
        Id : Guid
        Name : string
        Models : int
        Assembled : int
        Primed : int
        Painted : int
        Based : int
        Points : int
        Power : int
        Cost : int
        Purchased : bool
    } with
        static member Empty() = {
            Id = Guid.NewGuid()
            Name = ""
            Models = 0
            Assembled = 0
            Primed = 0
            Painted = 0
            Based = 0
            Points= 0
            Power = 0
            Cost = 0
            Purchased = true
        }

    type Category = {
        Id : Guid
        Name : string
    } with
        static member Empty() = {
            Id = Guid.NewGuid()
            Name = ""
        }

    type ColumnSettings = {
        ShowModelCount : bool
        ShowAssembled : bool
        ShowPrimed : bool
        ShowPainted : bool
        ShowBased : bool
        ShowPoints : bool
        ShowPower : bool
        ShowCost : bool
        ShowPurchased : bool
    } with
        static member Empty() = {
            ShowModelCount = true
            ShowAssembled = true
            ShowPrimed = true
            ShowPainted = true
            ShowBased = true
            ShowPoints = true
            ShowPower = false
            ShowCost = false
            ShowPurchased = false
        }

    type Project = {
        Id : Guid
        Name : string
        Category : Category option
        ColumnSettings : ColumnSettings
        Units : Unit list
        IsPublic : bool
    } with
        static member Empty() = {
            Id = Guid.NewGuid()
            Name = ""
            Category = None
            ColumnSettings = ColumnSettings.Empty()
            Units = []
            IsPublic = true
        }

module Helpers =
    let parseIntOrZero (s : string) =
            match System.Int32.TryParse(s) with
            | true, x -> x
            | false, _ -> 0

    let tryParseGuid (s : string) =
            match Guid.TryParse(s) with
            | true, g -> Some g
            | false, _ -> None
