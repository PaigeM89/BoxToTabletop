namespace BoxToTabletop.Client

open Fable.React
open Fable.React.Props

module AlertMessage =
    open Fulma
    open Elmish

    type AlertMessage =
    | InfoMessage of msg : string
    | ErrorMessage of msg : string
    with
        member this.Value() =
            match this with
            | InfoMessage v -> v
            | ErrorMessage v -> v

    let renderMessage (msg : AlertMessage) dispatch =
        let structure color header message =
                Message.message [ Message.Color color ] [
                    Message.header [] [
                        str header
                        Delete.delete [ Delete.OnClick (fun _ -> dispatch) ] [ ]
                    ]
                    Message.body [] [
                        str message
                    ]
                ]
        match msg with
        | InfoMessage msg ->
            structure IsInfo "" msg
        | ErrorMessage msg ->
            structure IsDanger "Error" msg
