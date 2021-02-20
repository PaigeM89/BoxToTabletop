namespace BoxToTabletop.Client

open BoxToTabletop.Domain

module Core =

    type Updates =
    | MCCVisibilityChange of mcc : Types.ModelCountCategory
