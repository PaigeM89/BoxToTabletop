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

//     let draggableLanguage = Fable.React.FunctionComponent.Of(fun (props: {| a: AlertMessage |}) ->
//         //React.functionComponent(fun (props: {| lang: Language |}) ->
//             let dragState, drag, preview = BoxToTabletop.ReactDND.useDrag [
//                 DragSpec.Item { ``type`` = "Language"; dragSrc = props.a }
//                 DragSpec.Collect (fun mon -> { isDragging = mon.isDragging() })
//                 DragSpec.End (fun dragItem mon -> printf "DragEnd: %A; Mon.DropTarget: %A" dragItem mon)
//             ]

//             div [
//                 Ref drag
//                 Style[Border "1px solid gray"; Padding "4px"; BackgroundColor (if dragState.isDragging then "yellow" else "white")]
//                 Key (props.a.Value()) //props.lang.Name
//             ] [
//                 str (props.a.Value())//props.lang.Name
//             ]
//     )

//     let dropTarget = Fable.React.FunctionComponent.Of(fun (props: {| a: AlertMessage |}) ->
//         //React.functionComponent(fun () ->
//         //let selectedLang, setSelectedLang = React.useState<Language option>(None)
//         let state =  Fable.React.HookBindings.Hooks.useState<AlertMessage option>(None)

//         let dropState, drop = useDrop [
//             DropSpec.Accept "Language"
//             DropSpec.Collect (fun mon -> { canDrop = mon.canDrop(); isOver = mon.isOver() })
//             DropSpec.Drop (fun (dragItem: DragItem<AlertMessage>) ->
//                 //setSelectedLang(Some dragItem.dragSrc)
//                 state.update(fun s -> Some dragItem.dragSrc)
//             )
//         ]

//         div [
//             Ref drop
//             Style[BackgroundColor "whitesmoke"; Padding "20px"; BackgroundColor (if dropState.isOver then "lightgreen" else "white")]
//         ] [
//             //selectedLang
//             state.current
//             |> Option.map (fun l -> sprintf "You selected: %s" "hi")
//             |> Option.defaultValue "Drag your .net language of choice here"
//             |> str
//         ]
//     )

//     let page = Fable.React.FunctionComponent.Of(fun () ->
//         //React.functionComponent(fun () ->
//         //let state = HookBindings.Hooks.useState<AlertMessage list>([])
//             //React.useState<Language list>([])

//         //React.useEffectOnce(fun () ->
// //        HookBindings.Hooks.useEffect( fun () ->
// //            [ "C#"; "F#"; "VB" ]
// //            |> List.map (fun l -> { Name = l })
// //            //|> setLanguages
// //            |> state.update(fun x -> Some x)
// //        )

//         div [Style[Width "300px"; MarginLeft "auto"; MarginRight "auto"]] [
//             //ReactDND.dndProviderHtml5 [] [
//             dndProvider [DndProviderProps.Backend html5Backend] [

//                 //dropTarget()
//                 dropTarget({| a = InfoMessage "hello world"|})
//                 draggableLanguage ( {| a = ErrorMessage "this is an error messge" |})
// //
// //                [ "F#"; "C#"; "VB" ]
// //                |> List.map (fun l -> { Name = l })
// //                |> List.map (fun lang -> draggableLanguage {| lang = lang |})
// //                |> ofList
//             ]
//         ]
//     )
