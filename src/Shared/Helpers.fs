namespace BoxToTabletop.Domain.Helpers

open System

[<RequireQualifiedAccess>]
module String =
    open System.Text
    let stringsEqualCI val1 val2 =
        String.Compare(val1, val2, StringComparison.OrdinalIgnoreCase) = 0

[<RequireQualifiedAccess>]
module Parsing =
    let parseIntOrZero (s : string) =
            match System.Int32.TryParse(s) with
            | true, x -> x
            | false, _ -> 0

    let tryParseGuid (s : string) =
            match Guid.TryParse(s) with
            | true, g -> Some g
            | false, _ -> None
