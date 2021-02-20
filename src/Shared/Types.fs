namespace BoxToTabletop.Domain

open System

module Types =

    type ModelCountCategory = {
        Id : Guid
        Name : string
        //todo: consider pulling this out and tupling at a higher level
        //the object relationship seems weird here
        Enabled : bool
    } with
        static member Empty() = {
            Id = Guid.NewGuid()
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

    let getModelCountCategoryByName (name : string) (mccs : ModelCountCategory list) =
        mccs |> List.tryFind (fun x -> Helpers.stringsEqualCI x.Name name)

    let replaceModelCountCategory (newCat : ModelCountCategory) (mccs : ModelCountCategory list) =
        let newList =
            mccs
            |> List.filter (fun x -> not (Helpers.stringsEqualCI x.Name newCat.Name))
        newCat :: newList

    let createCountCategory name =
        { ModelCount.Empty() with Category = { ModelCountCategory.Empty() with Name = name } }

    let stubModelCounts() = [
        createCountCategory "Assembled"
        createCountCategory "Primed"
        createCountCategory "Painted"
        createCountCategory "Based"
    ]

    let stubCategories() = stubModelCounts() |> List.map (fun x -> x.Category)

    type Unit = {
        Id : Guid
        Name : string
        /// The number of models in the unit.
        /// <remarks>
        /// This is such a fundamental measurement of what we're doing that it's not going to be a category
        /// </remarks>
        Models : int
        ModelCounts : ModelCount list
    } with
        static member Empty() = {
            Id = Guid.NewGuid()
            Name = ""
            Models = 0
            ModelCounts = stubModelCounts()
        }

    type Category = {
        Id : Guid
        Name : string
    } with
        static member Empty() = {
            Id = Guid.NewGuid()
            Name = ""
        }

    type Project = {
        Id : Guid
        Name : string
        Category : Category option
        //ColumnSettings : ColumnSettings
        CountCategories : ModelCountCategory list
        Units : Unit list
        IsPublic : bool
    } with
        static member Empty() = {
            Id = Guid.NewGuid()
            Name = ""
            Category = None
            //ColumnSettings = ColumnSettings.Empty()
            CountCategories = stubCategories()
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
