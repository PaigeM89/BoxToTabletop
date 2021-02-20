namespace BoxToTabletop.Client

open BoxToTabletop.Domain
open BoxToTabletop.Domain.Types

module Core =

    type Updates =
    /// A change in the visible columns, which needs to be propagated to multiple components.
    | ColumnSettingsChange of ColumnSettings
