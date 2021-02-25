namespace BoxToTabletop.Client

module AlertMessage =
    type AlertMessage =
    | InfoMessage of msg : string
    | ErrorMessage of msg : string

