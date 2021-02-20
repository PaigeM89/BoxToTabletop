namespace BoxToTabletop.Domain

open System
open BoxToTabletop.Domain.Helpers

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
        counts |> List.tryFind (fun x -> String.stringsEqualCI x.Category.Name cat.Name)

    let getModelCountCategoryByName (name : string) (mccs : ModelCountCategory list) =
        mccs |> List.tryFind (fun x -> String.stringsEqualCI x.Name name)

    let replaceModelCountCategory (newCat : ModelCountCategory) (mccs : ModelCountCategory list) =
        let newList =
            mccs
            |> List.filter (fun x -> not (String.stringsEqualCI x.Name newCat.Name))
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
        //ModelCounts : ModelCount list

        Assembled : int
        Primed : int
        Painted : int
        Based : int

    } with
        static member Empty() = {
            Id = Guid.NewGuid()
            Name = ""
            Models = 0
            //ModelCounts = stubModelCounts()
            Assembled = 0
            Primed = 0
            Painted = 0
            Based = 0
        }

    type ProjectCategory = {
        Id : Guid
        Name : string
    } with
        static member Empty() = {
            Id = Guid.NewGuid()
            Name = ""
        }

    type ColumnSettings = {
        AssemblyVisible : bool
        PrimedVisible : bool
        PaintedVisible : bool
        BasedVisible : bool
    } with
        static member Empty() = {
            AssemblyVisible = false
            PrimedVisible = false
            PaintedVisible = false
            BasedVisible = false
        }

        member this.Enumerate() =
            [
                yield "Assembled", this.AssemblyVisible
                yield "Primed", this.PrimedVisible
                yield "Painted", this.PaintedVisible
                yield "Based", this.BasedVisible

            ]

        member this.EnumerateWithTransformer() =
            [
                yield {| Name = "Assembled"; Value = this.AssemblyVisible; Func = fun newValue -> { this with AssemblyVisible = newValue } |}
                yield {| Name = "Primed"; Value = this.PrimedVisible; Func = fun newValue -> { this with PrimedVisible = newValue } |}
                yield {| Name = "Painted"; Value = this.PaintedVisible; Func = fun newValue -> { this with PaintedVisible = newValue } |}
                yield {| Name = "Based"; Value = this.BasedVisible; Func = fun newValue -> { this with BasedVisible = newValue } |}
            ]

    type Project = {
        Id : Guid
        Name : string
        Category : ProjectCategory option
        ColumnSettings : ColumnSettings
//        CountCategories : ModelCountCategory list
        Units : Unit list
        IsPublic : bool
    } with
        static member Empty() = {
            Id = Guid.NewGuid()
            Name = ""
            Category = None
            ColumnSettings = ColumnSettings.Empty()
            //CountCategories = stubCategories()
            Units = []
            IsPublic = true
        }

    module Unit =
        let enumerateColumns (cs : ColumnSettings) (unit : Unit) =
            [
                if cs.AssemblyVisible then yield ("Assembled", unit.Assembled)
                if cs.PrimedVisible then yield ("Primed", unit.Primed)
                if cs.PaintedVisible then yield ("Painted", unit.Painted)
                if cs.BasedVisible then yield ("Based", unit.Based)
            ]
