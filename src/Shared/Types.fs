namespace BoxToTabletop.Domain

open System
open BoxToTabletop.Domain.Helpers

module Types =

    type Unit = {
        Id : Guid
        Name : string
        /// The number of models in the unit.
        /// <remarks>
        /// This is such a fundamental measurement of what we're doing that it's not going to be a category
        /// </remarks>
        Models : int
        Assembled : int
        Primed : int
        Painted : int
        Based : int

    } with
        static member Empty() = {
            Id = Guid.NewGuid()
            Name = ""
            Models = 0
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
            AssemblyVisible = true
            PrimedVisible = false
            PaintedVisible = true
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

    module Unit =
        let enumerateColumns (cs : ColumnSettings) (unit : Unit) =
            [
                if cs.AssemblyVisible then yield ("Assembled", unit.Assembled)
                if cs.PrimedVisible then yield ("Primed", unit.Primed)
                if cs.PaintedVisible then yield ("Painted", unit.Painted)
                if cs.BasedVisible then yield ("Based", unit.Based)
            ]
