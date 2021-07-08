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
let Auth0Config = Root %% "auth_config"

// todo: see if these can better handle guids
// and see if we can make routing cleaner in the server
// i don't like any of the ways I'm doing these

module ProjectRoutes =
    let Root = Root %% "projects"
    // let GETALL = Root
    let GET() = PrintfFormat<_,_,_,_,_> (Root %% "%O")
    // let POST = Root
    let PUT() = PrintfFormat<_,_,_,_,_> (Root %% "%O")
    let PUT2 = fun baseUrl (id : Guid) -> baseUrl %% Root %% (sprintf "%O" id)
    let DELETE() = PrintfFormat<_,_,_,_,_> (Root %% "%O")

    module Priorities =
        let PrioritiesRoot = "priorities"
        let PUT() = PrintfFormat<_,_,_,_,_> (Root %% "%O" %% PrioritiesRoot)

    module Columns =
        let GET() = PrintfFormat<_,_,_,_,_> (Root %% "%O" %% "columns")
        let PUT() = PrintfFormat<_,_,_,_,_> (Root %% "%O" %% "columns" %% "%O")

module UnitRoutes =
    let Root = Root %% "units"
    let GET() = PrintfFormat<_,_,_,_,_> (Root %% "%O")
    let PUTCollection = Root
    let PUT() = PrintfFormat<_,_,_,_,_> (Root %% "%O")
    let DELETE() = PrintfFormat<_,_,_,_,_> (Root %% "%O")

    // todo: this really should just be a PATCH on unit but that requires a lot more infrastructure
    module Transfer =
        let Root = Root %% "%O/transfer"
        let POST() = PrintfFormat<_,_,_,_,_> Root

    module Columns =
        let GET() = PrintfFormat<_,_,_,_,_> (Root %% "%O" %% "columns")
        let PUT() = PrintfFormat<_,_,_,_,_> (Root %% "%O" %% "columns" %% "%O")
