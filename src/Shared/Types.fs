namespace BoxToTabletop.Domain

open System

module Types =

    type ModelCountCategory = {
        Id : Guid
        Name : string
        Enabled : bool
    } with
        static member Empty() = {
            Id = Guid.Empty
            Name = ""
            Enabled = false
        }

    type ModelCount = {
        Id : Guid
        Category : ModelCountCategory
        Count : int
    } with
        static member Empty() = {
            Id = Guid.NewGuid()
            Category = ModelCountCategory.Empty()
            Count = 0
        }

    let getCountColumn (cat : ModelCountCategory) (counts : ModelCount list) =
        counts |> List.tryFind (fun x -> Helpers.stringsEqualCI x.Category.Name cat.Name)

    let createCountCategory name =
        { ModelCount.Empty() with Category = { ModelCountCategory.Empty() with Name = name } }

    let stubModelCounts() = [
        createCountCategory "Assembled"
        createCountCategory "Primed"
        createCountCategory "Painted"
        createCountCategory "Based"
    ]

    let stubColumns() = stubModelCounts() |> List.map (fun x -> x.Category)

    type Unit = {
        Id : Guid
        Name : string
        /// The number of models in the unit.
        /// <remarks>
        /// This is such a fundamental measurement of what we're doing that it's not going to be a category
        /// </remarks>
        Models : int
        ModelCounts : ModelCount list
//        Assembled : int
//        Primed : int
//        Painted : int
//        Based : int
//        Points : int
//        Power : int
//        Cost : int
//        Purchased : bool
    } with
        static member Empty() = {
            Id = Guid.NewGuid()
            Name = ""
            Models = 0
            ModelCounts = stubModelCounts()
//            Assembled = 0
//            Primed = 0
//            Painted = 0
//            Based = 0
//            Points= 0
//            Power = 0
//            Cost = 0
//            Purchased = true
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
        CountCategories : ModelCountCategory list
        Units : Unit list
        IsPublic : bool
    } with
        static member Empty() = {
            Id = Guid.NewGuid()
            Name = ""
            Category = None
            ColumnSettings = ColumnSettings.Empty()
            CountCategories = stubColumns()
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
