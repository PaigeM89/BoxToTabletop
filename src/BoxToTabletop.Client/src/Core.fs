namespace BoxToTabletop.Client

open BoxToTabletop.Domain
open BoxToTabletop.Domain.Types

module Core =

    type Updates =
    //| MCCVisibilityChange of mcc : Types.ModelCountCategory
    | ColumnSettingsChange of ColumnSettings
