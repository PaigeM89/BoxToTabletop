namespace BoxToTabletop.Domain

open System
open BoxToTabletop.Domain.Helpers

#if FABLE_COMPILER
open Thoth.Json
#else
open Thoth.Json.Net
#endif

module Types =

    type Unit = {
        Id : Guid
        ProjectId : Guid
        Name : string
        Models : int
        Assembled : int
        Primed : int
        Painted : int
        Based : int
        /// Lower priority units are higher in the table.
        /// New units are added with priority 0, then the rest are re-ordered.
        /// This is not intended to be a user visible field.
        Priority : int
    } with
        static member Empty() = {
            Id = Guid.NewGuid()
            ProjectId = Guid.Empty
            Name = ""
            Models = 0
            Assembled = 0
            Primed = 0
            Painted = 0
            Based = 0
            Priority = 0
        }
        static member Decoder : Decoder<Unit> =
            Decode.object
                (fun get ->
                    {
                        Id = get.Required.Field "id" Decode.guid
                        ProjectId = get.Required.Field "projectId" Decode.guid
                        Name = get.Required.Field "name" Decode.string
                        Models = get.Required.Field "models" Decode.int
                        Assembled = get.Required.Field "assembled" Decode.int
                        Primed = get.Required.Field "primed" Decode.int
                        Painted = get.Required.Field "painted" Decode.int
                        Based = get.Required.Field "based" Decode.int
                        Priority = get.Required.Field "priority" Decode.int
                    }
                )

        static member DecodeMany : Decoder<Unit list> =
            Decode.list Unit.Decoder

        static member Encoder (unit : Unit) =
            let v =
                Encode.object
                    [
                        "id", Encode.guid unit.Id
                        "projectId", Encode.guid unit.ProjectId
                        "name", Encode.string unit.Name
                        "models", Encode.int unit.Models
                        "assembled", Encode.int unit.Assembled
                        "primed", Encode.int unit.Primed
                        "painted", Encode.int unit.Painted
                        "based", Encode.int unit.Based
                        "priority", Encode.int unit.Priority
                    ]
            printfn "encoded unit is %s" (v.ToString())
            v

    type UnitPriority = {
        UnitId : Guid
        UnitPriority : int
    } with
        static member Decoder : Decoder<UnitPriority> =
            Decode.object
                (fun get ->
                    {
                      UnitId = get.Required.Field "unitid" Decode.guid
                      UnitPriority = get.Required.Field "unitpriority" Decode.int
                    }
                )

        member this.Encode() =
            let e = Encode.object [
                "unitid", Encode.guid this.UnitId
                "unitpriority", Encode.int this.UnitPriority
            ]
            printfn "Encoded object is %s" (e.ToString())
            e

        static member EncodeList (ups : UnitPriority list) =
            let v = Encode.list (ups |> List.map (fun x -> x.Encode()))
            printfn "encoded unit list is \"%s\"" (v.ToString())
            v

        static member DecodeList : Decoder<UnitPriority list> =
            Decode.list UnitPriority.Decoder

    module UnitPriority =
        let denseRank (priorities : UnitPriority list) =
            priorities

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
        //Category : ProjectCategory option
        ColumnSettings : ColumnSettings
        Units : Unit list
        IsPublic : bool
    } with
        static member Empty() = {
            Id = Guid.NewGuid()
            Name = "Default Project"
            //Category = None
            ColumnSettings = ColumnSettings.Empty()
            Units = []
            IsPublic = true
        }

        static member Decoder : Decoder<Project> =
            Decode.object
                (fun get -> {
                    Project.Id = get.Required.Field "id" Decode.guid
                    Name = get.Required.Field "name" Decode.string
                    IsPublic = get.Required.Field "isPublic" Decode.bool
                    Units = [] //will be populated in a 2nd call
                    ColumnSettings = {
                        AssemblyVisible = get.Required.Field "assemblyVisible" Decode.bool
                        PrimedVisible = get.Required.Field "primedVisible" Decode.bool
                        PaintedVisible = get.Required.Field "paintedVisible" Decode.bool
                        BasedVisible = get.Required.Field "basedVisible" Decode.bool
                    }
                }
            )

        static member DecodeMany : Decoder<Project list> =
            Decode.list Project.Decoder

        static member Encoder (project : Project) =
            Encode.object [
                "id", Encode.guid project.Id
                "name", Encode.string project.Name
                "isPublic", Encode.bool project.IsPublic
                "assemblyVisible", Encode.bool project.ColumnSettings.AssemblyVisible
                "primedVisible", Encode.bool project.ColumnSettings.PrimedVisible
                "paintedVisible", Encode.bool project.ColumnSettings.PaintedVisible
                "basedVisible", Encode.bool project.ColumnSettings.BasedVisible
            ]

    module Unit =
        let enumerateColumns (cs : ColumnSettings) (unit : Unit) =
            [
                if cs.AssemblyVisible then yield ("Assembled", unit.Assembled)
                if cs.PrimedVisible then yield ("Primed", unit.Primed)
                if cs.PaintedVisible then yield ("Painted", unit.Painted)
                if cs.BasedVisible then yield ("Based", unit.Based)
            ]
