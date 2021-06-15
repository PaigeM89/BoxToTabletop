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

    let renderError errorId title message dispatch =
        Message.message [ Message.Color IsDanger ] [
            Message.header [] [
                str title
                Delete.delete [ Delete.OnClick (fun _ -> dispatch errorId) ] []
            ]
            Message.body [] [
                str message
            ]
        ]

module Toast =
    open Thoth.Elmish
    open Fable.FontAwesome
    open Fulma

    let renderToastWithFulma =
        { new Toast.IRenderer<Fa.IconOption list> with
            member __.Toast children color =
                Notification.notification [ Notification.CustomClass color ]
                    children

            member __.CloseButton onClick =
                Notification.delete [ Props [ OnClick onClick ] ]
                    [ ]

            member __.Title txt =
                Heading.h5 []
                           [ str txt ]

            member __.Icon (iconSettings : Fa.IconOption list) =
                let icon = iconSettings @ [ Fa.Size Fa.Fa2x ]
                Icon.icon [ Icon.Size IsMedium ] [
                    Fa.i icon [ ] 
                ]

            member __.SingleLayout title message =
                div [ ]
                    [ title; message ]

            member __.Message txt =
                span [ ]
                    [ str txt ]

            member __.SplittedLayout iconView title message =
                Columns.columns [ 
                    Columns.IsGapless
                    Columns.IsVCentered
                ] [ 
                    Column.column [ 
                        Column.Width (Screen.All, Column.Is2)
                    ] [ 
                        if not (isNull iconView) then iconView else div [] []
                    ]
                    Column.column [ ] [ 
                        if not (isNull title) then title else div [] []
                        if not (isNull message) then message else div [] []
                    ] 
                ]

            member __.StatusToColor status =
                match status with
                | Toast.Success -> "is-success"
                | Toast.Warning -> "is-warning"
                | Toast.Error -> "is-danger"
                | Toast.Info -> "is-info"
        }
