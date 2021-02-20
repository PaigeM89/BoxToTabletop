namespace BoxToTabletop.Domain

open System

module Helpers =
    let stringsEqualCI val1 val2 =
        String.Compare(val1, val2, StringComparison.OrdinalIgnoreCase) = 0
