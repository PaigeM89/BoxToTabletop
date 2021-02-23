module BoxToTabletop.Domain.Routes

open System

let combine (str1 : string) (str2 : string) =
    if str2 |> String.IsNullOrWhiteSpace then
        str1
    else
        str1.TrimEnd '/' + "/" + str2.TrimStart('/').TrimEnd('/')

let inline (%%) str1 str2 =
    combine str1 str2


let Root = "/api/v1"

// todo: see if these can better handle guids
// and see if we can make routing cleaner in the server

module Project =
    let Root = Root %% "projects"
    let GETALL = Root
    let GET() = PrintfFormat<_,_,_,_,_> (Root %% "%O")
    let POST = Root
    let PUT() = PrintfFormat<_,_,_,_,_> (Root %% "%O")
    let PUT2 = fun baseUrl (id : Guid) -> baseUrl %% Root %% (sprintf "%O" id)
    let DELETE() = PrintfFormat<_,_,_,_,_> (Root %% "%O")

    module UnitRoutes =
        // all units are accessed from within a project
        let Root = Root %% "%O/units"
        let Units = "units"
        let GETALL() = PrintfFormat<_,_,_,_,_> Root
        let GET() = PrintfFormat<_,_,_,_,_> (Root %% "%O")
        let POST() = PrintfFormat<_,_,_,_,_> Root
        let PUT() = PrintfFormat<_,_,_,_,_> (Root %% "%O")
        let PUT2 =
            fun baseUrl (projId : Guid) ->
                let b = PUT2 baseUrl projId
                fun (unitId : Guid) -> Root %% b %% Units %% (sprintf "%O" unitId)
        let DELETE() = PrintfFormat<_,_,_,_,_> (Root %% "%O")

