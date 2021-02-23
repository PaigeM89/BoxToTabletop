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

module Project =
    let Root = Root %% "projects"
    let GETALL = Root
    let GET() = PrintfFormat<_,_,_,_,_> (Root %% "%O")
    let POST = Root
    let PUT() = PrintfFormat<_,_,_,_,_> (Root %% "%O")
    let DELETE() = PrintfFormat<_,_,_,_,_> (Root %% "%O")

    module UnitRoutes =
        // all units are accessed from within a project
        let Root = "%O/units"
        let GETALL() = PrintfFormat<_,_,_,_,_> Root
        let GET() = PrintfFormat<_,_,_,_,_> (Root %% "%O")
        let POST() = PrintfFormat<_,_,_,_,_> Root
        let PUT() = PrintfFormat<_,_,_,_,_> (Root %% "%O")
        let DELETE() = PrintfFormat<_,_,_,_,_> (Root %% "%O")

