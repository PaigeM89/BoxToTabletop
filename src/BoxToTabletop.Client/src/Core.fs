namespace BoxToTabletop.Client

open System.Data
open BoxToTabletop.Domain
open BoxToTabletop.Domain.Types
open System
open Fable.SimpleHttp

module Core =

    /// tell the root component to start or stop the spinner
    type SpinnerUpdate =
    | SpinnerStart of sourceId : Guid
    | SpinnerEnd of sourceId : Guid

    type UpdateResponse<'a, 'b, 'd> = {
        model : 'a
        cmd : Elmish.Cmd<'b>
        spinner : SpinnerUpdate option
        raised : 'd option
    } with

        static member basic mdl cmd = {
            model = mdl
            cmd = cmd
            spinner = None
            raised = None
        }

        static member create mdl cmd spin raised = {
            model = mdl
            cmd = cmd
            spinner = spin
            raised = raised
        }

        static member withSpin mdl cmd spin = {
            model = mdl
            cmd = cmd
            spinner = Some spin
            raised = None
        }

        static member withRaised mdl cmd raised = {
            model = mdl
            cmd = cmd
            spinner = None
            raised = Some raised
        }

module Config =
    type Auth0Config = {
        ClientId : string
        Domain : string
        Audience : string
    } with
        static member Empty() = { ClientId = ""; Domain = ""; Audience = "" }
        static member Create clientId domain audience = {
            ClientId = clientId
            Domain = domain
            Audience = audience
        }

    type T = {
        ServerUrl : string
        ClientUrl : string
        Auth0Config : Auth0ConfigJson
        JwtToken : string option
        FeatureFlags : Types.FeatureFlags
        IsDarkMode : bool
    } with
        static member Default() = {
            ServerUrl =  "" //"http://localhost:5000"
            ClientUrl = ""
            Auth0Config = Auth0ConfigJson.Empty()
            JwtToken = None
            FeatureFlags = FeatureFlags.Default()
            IsDarkMode = false
        }

    let withServerUrl (serverUrl : string) (t : T) =
        { t with ServerUrl = serverUrl }
    let withClientUrl (clientUrl : string) (t : T) =
        { t with ClientUrl = clientUrl }
    let withAuth0Config conf (t : T) = { t with Auth0Config = conf }
    let withToken token (t : T) = { t with JwtToken = Some token }

    let withDarkModeFlag b (t : T) = { t with IsDarkMode = b }

module Promises =
    open Fetch
    open Thoth.Json
    open Thoth.Fetch
    open Fable.Core.JS
    open BoxToTabletop.Domain.Routes
    open Routes
    open Core

    // type TokenResponse = {
    //     access_token : string
    //     token_type : string
    // } with
    //     static member Decoder : Decoder<TokenResponse> =
    //         Decode.object (fun get -> 
    //             {
    //                 access_token = get.Required.Field "access_token" Decode.string
    //                 token_type = get.Required.Field "token_type" Decode.string
    //             })

    // let getAccessToken() = promise {
    //     let url = "https://dev-6duts2ta.us.auth0.com/oauth/token"
    //     let headers = [
    //         HttpRequestHeaders.ContentType "application/json"
    //         HttpRequestHeaders.Origin "*"
    //         HttpRequestHeaders.Custom ("Access-Control-Allow-Origin", "*")
    //     ]
    //     let request = 
    //         {|
    //             client_id = "3hT6zjjsWmoQqNlB5i89P06V6LO4dDA0"
    //             client_secret = "uZcbteHZlzqf9dWaVLeFegQU23Q-XGHcoqqRcZMo-n7NkPxNGgxvZZ_gak9volDH"
    //             audience = "http://localhost:5000"
    //             grant_type = "client_credentials"
    //         |}
    //     let data = Thoth.Json.Encode.Auto.toString(0, request)
    //     let decoder = TokenResponse.Decoder
    //     return! Fetch.tryPost(url, data, decoder = decoder, headers = headers)
    // }

    let getBearer (config : Config.T) =
        match config.JwtToken with
        | Some t -> "Bearer " + t
        | None -> "Bearer"

    let getBearerHeader (config : Config.T) =
        HttpRequestHeaders.Authorization (getBearer config)

    let printFetchError (fe : FetchError) =
        match fe with
        | PreparingRequestFailed exn ->
            printfn "Error preparing request: %A" exn
            "Error creating request"
        | DecodingFailed s ->
            printfn "Unable to decode %s to object" s
            "Error reading response"
        | FetchFailed response ->
            printfn "Error fetching, response is %A" response
            sprintf "Received code %i from server" response.Status
        | NetworkError exn ->
            printfn "Network error: %A" exn
            "Error reaching server"

    let buildStaticRoute (config : Config.T) route =
        Routes.combine config.ServerUrl route

    let buildRouteSimple (config : Config.T) route = Routes.combine config.ServerUrl route

    let buildRoute (config : Config.T) (routeEval) =
        fun x -> Routes.combine config.ServerUrl (sprintf routeEval x)

    let buildRoute2 (config : Config.T) routeEval =
        fun (x, y) -> Routes.combine config.ServerUrl (sprintf routeEval x y)

    let addQueryParam qparam qvalue route = route + (sprintf "?%s=%s" qparam qvalue)

    let getAuth0Config (config : Config.T) = promise {
        let url = Routes.Auth0Config |> buildRouteSimple config
        let decoder = Types.Auth0ConfigJson.Decoder
        let headers = [
            Origin "*"
        ]
        return! Fetch.tryGet(url, decoder = decoder, headers = headers)
    }

    let createUnit (config : Config.T) (unit : Types.Unit) = promise {
        let url = UnitRoutes.Root |> buildRouteSimple config
        let data = Types.Unit.Encoder unit
        let _decoder = Types.Unit.Decoder
        let headers = [
            HttpRequestHeaders.Origin "*"
            getBearerHeader config
        ]
        return! Fetch.tryPost(url, data, decoder = _decoder, headers = headers)
    }

    let updateUnit (config : Config.T) (unit : Types.Unit) = promise {
        let url = UnitRoutes.PUT() |> buildRoute config <| unit.Id
        let data = Types.Unit.Encoder unit
        let headers = [
            // HttpRequestHeaders.Origin "*"
            getBearerHeader config
        ]
        let decoder = Types.Unit.Decoder
        return! Fetch.tryPut(url, data, decoder = decoder, headers = headers)
    }

    let updateManyUnits (config : Config.T) (units : Types.Unit list) = promise {
        let url = UnitRoutes.PUTCollection |> buildRouteSimple config
        let data = Types.Unit.EncodeList units
        let headers = [
            getBearerHeader config
        ]
        let decoder = Decode.list (Decode.guid)
        return! Fetch.tryPut(url, data, decoder = decoder, headers = headers)
    }

    let loadUnitsForProject (config : Config.T) (projectId : Guid) = async {
        let url = UnitRoutes.Root |> buildRouteSimple config |> addQueryParam "projectId" (string projectId)
        let! response =
            Http.request url
            |> Http.method GET
            |> Http.header (Headers.accept "application/json")
            |> Http.header (Headers.authorization (getBearer config))
            |> Http.send

        if response.statusCode = 200 then
            let decoder : Thoth.Json.Decoder<Types.Unit list> = Types.Unit.DecodeMany
            let body = response.responseText
            let decoded = Thoth.Json.Decode.fromString decoder body
            return decoded
        elif response.statusCode = 204 then
            return Ok []
        else
            return Error (sprintf "Get All Units returned code %i" response.statusCode)
    }

    let deleteUnit (config : Config.T) (projectId : Guid) (unitId : Guid) : Promise<unit> = promise {
        let url = UnitRoutes.DELETE() |> buildRoute config <| unitId
        let headers = [
            getBearerHeader config
        ]
        return! Fetch.delete(url, headers = headers)
    }

    let transferUnit (config : Config.T) unitId newProjectId = async {
        let url = UnitRoutes.Transfer.POST() |> buildRoute config <| unitId
        let payload = Thoth.Json.Encode.guid newProjectId |> Thoth.Json.Encode.toString 0
        let! response =
            Http.request url
            |> Http.method POST
            |> Http.header (Headers.authorization (getBearer config))
            |> Http.content (BodyContent.Text payload)
            |> Http.send

        printfn "Sending unit transfer request to url %A" url

        if response.statusCode = 200 then
            return Ok unitId
        else
            return Error response.statusCode
    }

    let loadAllProjects (config : Config.T) : Promise<Project list> = promise {
        let url = ProjectRoutes.GETALL |> buildStaticRoute config
        let headers = [
            HttpRequestHeaders.Authorization (getBearer config)
        ]
        let decoder = Types.Project.DecodeMany
        return! Fetch.get(url, decoder = decoder, headers = headers)
    }

    let loadProject (config : Config.T) (id : Guid) = promise {
        let url = ProjectRoutes.GET() |> buildRoute config <| id
        let headers = [
            HttpRequestHeaders.Authorization (getBearer config)
        ]
        let decoder = Types.Project.Decoder
        return! Fetch.tryGet(url, decoder = decoder, headers = headers)
    }

    let saveProject (config : Config.T) (project : Project) = promise {
        let url = ProjectRoutes.POST |> buildRouteSimple config
        let headers = [
            getBearerHeader config
        ]
        let decoder = Types.Project.Decoder
        return! Fetch.tryPost(url, project, decoder = decoder, headers = headers)
    }

    let updateProject (config : Config.T) (project : Project) : Promise<Project> = promise {
        let url = ProjectRoutes.PUT() |> buildRoute config <| project.Id
        let headers = [
            HttpRequestHeaders.Authorization (getBearer config)
        ]
        let decoder = Types.Project.Decoder
        return! Fetch.put(url, project, decoder = decoder, headers = headers)
    }

    let deleteProject (config : Config.T) (projectId : Guid) : Promise<unit> = promise {
        let url = ProjectRoutes.DELETE() |> buildRoute config <| projectId
        let headers = [ getBearerHeader config ]
        return! Fetch.delete(url, headers = headers)
    }

    let updateUnitPriorities (config : Config.T) (projId : Guid) (updates : UnitPriority list) : Promise<Result<int, FetchError>> = promise {
        let url = ProjectRoutes.Priorities.PUT() |> buildRoute config <| projId
        let headers = [
            HttpRequestHeaders.Authorization (getBearer config)
        ]
        //let decoder = Types.UnitPriority.DecodeList 
        let decoder = Decode.int
        let payload = UnitPriority.EncodeList updates
        return! Fetch.tryPut(url, payload, decoder = decoder, headers = headers)
    }

    let updateUnitPriorities2 (config : Config.T) (projId : Guid) (updates : UnitPriority list) = async {
        let url = ProjectRoutes.Priorities.PUT() |> buildRoute config <| projId
        let encoded = UnitPriority.EncodeList updates
        let payload = Thoth.Json.Encode.toString 0 encoded
        let! response =
            Http.request url
            |> Http.method PUT
            |> Http.content (BodyContent.Text payload)
            |> Http.header (Headers.contentType "application/json")
            |> Http.header (Headers.authorization (getBearer config))
            |> Http.send

        printfn "Status: %d" response.statusCode
        printfn "Content: %s" response.responseText
        if response.statusCode >= 200 && response.statusCode < 300 then
            return Ok ()
        else
            return Error response.statusCode
    }

// // https://antongorbikov.com/2020/02/10/getting-started-with-fable-routing/
// module Routing = 
//     open Fable.Core
//     open Fable.Core.JsInterop
//     open Fable.React

//     type BrowserRouterProp =
//     | ForceRefresh of bool

//     let inline BrowserRouter (props: BrowserRouterProp list) (elements : ReactElement list) =
//         ofImport "BrowserRouter" "react-router-dom" (keyValueList CaseRules.LowerFirst props) elements

//     module Routing =
//         type ToObject = {
//             Pathname: string
//             Search: string
//             Hash: string
//             State: string
//         }

//         type LinkProps =
//             | To of U3<string, ToObject, (string -> string)>
//             | Replace of bool

//         let inline Link (props: LinkProps list) (elems: ReactElement list): ReactElement =
//             ofImport "Link" "react-router-dom" (keyValueList CaseRules.LowerFirst props) elems
    
//     module Route =
//         type RouteProps =
//         | Path of string

//         let inline Route (props: RouteProps list) (elems: ReactElement list): ReactElement =
//             ofImport "Route" "react-router-dom" (keyValueList CaseRules.LowerFirst props) elems

//         let inline Switch (props: unit list) (elems: ReactElement list): ReactElement =
//             ofImport "Switch" "react-router-dom" (keyValueList CaseRules.LowerFirst props) elems


[<RequireQualifiedAccess>]
module MediaQuery =

    open Fable.React
    open Fable.React.Props
    open Fable.Core
    open Fable.Core.JsInterop


    [<StringEnum>]
    type Orientation = Portrait | Landscape 

    type IMediaQueryProps = 
    | MaxDeviceWidth of int
    | MinDeviceWidth of int
    | MaxWidth of int 
    | MinWidth of int 
    | Orientation of Orientation
    | MinResolution of string 
    | MaxResolution of string
    | Query of string
        interface IHTMLProp
        
    let mediaQuery (props: IHTMLProp list) children = 
        ofImport "default" 
                 "react-responsive" 
                 (keyValueList CaseRules.LowerFirst props) 
                 children