namespace BoxToTabletop.Client

open Fable.React
open BoxToTabletop.Domain
open BoxToTabletop.Domain.Types

module Login =
  open Fable.Core
  open Fable.Core.JsInterop
  open Elmish
  open Fable.Core.JS

  type UserRecord = {
    /// The full name
    Name : string option
    /// The first name
    GivenName : string option
    /// In at least one case, this is the first part of the email address
    /// Probably should avoid using
    Nickname : string option
    Email : string option
    EmailVerified : bool option
    Locale : string option
    UserId : string option
  } with
    static member Empty() = {
      Name = None
      GivenName = None
      Nickname = None
      Email = None
      EmailVerified = Some false
      Locale = None
      UserId = None
    }

  let tryProp jsObj prop =
    let v = jsObj?(prop)
    Some v

  let createFromObject (jsObj : obj) =
    { UserRecord.Empty() with
        Name = tryProp jsObj "name"
        GivenName = tryProp jsObj "given_name"
        Nickname = tryProp jsObj "nickname"
        Email = tryProp jsObj "email"
        EmailVerified = tryProp jsObj "email_verified"
        Locale = tryProp jsObj "locale"
        UserId = tryProp jsObj "sub"
    }

  type Msg =
  | InitLoginProcess of config : Config.T
  | TryGetAuth0Config of config : Config.T
  | GetAuth0ConfigSuccess of Result<Auth0ConfigJson, Thoth.Fetch.FetchError>
  | GetAuth0ConfigError of exn
  | TryCreateClient
  | CreateClientSuccess of unit //of client : obj
  | CreateClientError of exn
  | TryLogin
  | LoginSuccess of obj // loginResult : obj
  | LoginError of exn
  | CheckIsAuthenticated
  | AuthenticationResult of bool option
  | AuthenticationCheckError of exn
  | TryGetUser
  | GotUser of userObj : obj
  | GetUserException of exn
  | TryLogout
  | LogoutError of exn
  | TryRedirect
  | RedirectException of exn
  | GetTokenSuccess of token : string
  | GetTokenException of exn
  
  type RaisedMsg =
  | Auth0ConfigLoaded of conf : Auth0ConfigJson
  | RaiseError of message : string
  | GotToken of token : string

  type Model = {
    User : UserRecord option
    JwtToken : string option
    ServerUrl : string
    ClientUrl : string
    Auth0Config : Types.Auth0ConfigJson
  } with
    static member Empty() = { 
      User = None
      JwtToken = None
      ServerUrl = ""
      ClientUrl = ""
      Auth0Config = Auth0ConfigJson.Empty()
    }

    member this.Reset() = {
      this with
        JwtToken = None
        User = None
    }

  [<ImportMember("./auth0/login.js")>]
  let configureClient : (string * string -> Promise<unit>)= jsNative

  [<ImportMember("./auth0/login.js")>]
  let login : (string option -> Promise<obj>) = jsNative

  [<ImportMember("./auth0/login.js")>]
  let isAuthenticated : (unit -> Promise<bool option>) = jsNative

  [<ImportMember("./auth0/login.js")>]
  let getUser : (unit -> Promise<obj>) = jsNative

  [<ImportMember("./auth0/login.js")>]
  let logout : (unit -> Promise<unit>) = jsNative

  [<ImportMember("./auth0/login.js")>]
  let handleRedirect : unit -> Promise<unit> = jsNative

  [<ImportMember("./auth0/login.js")>]
  let getToken : string -> Promise<string> = jsNative

  let tryGetAuth0Config (config : Config.T) =
    Cmd.OfPromise.either (Promises.getAuth0Config) config GetAuth0ConfigSuccess GetAuth0ConfigError

  let createClient (conf : Auth0ConfigJson) =
    let createClient() = configureClient(conf.Domain, conf.ClientId)
    Cmd.OfPromise.either createClient () CreateClientSuccess CreateClientError

  let checkIsAuthenticated() =
    Cmd.OfPromise.either (isAuthenticated) () AuthenticationResult AuthenticationCheckError

  let tryLogin (clientUrl : string) =
    let tryLogin() = login (Some clientUrl)
    Cmd.OfPromise.either tryLogin () LoginSuccess LoginError

  let tryGetUser() =
    Cmd.OfPromise.either getUser () GotUser GetUserException

  let tryLogout() =
    Cmd.OfPromise.attempt logout () LogoutError

  let tryParseQuery model =
    let query = Browser.Dom.window.location.search
    let codeIndex = query.IndexOf("code=")
    let stateIndex = query.IndexOf("state=")
    if codeIndex > 0 && stateIndex > 0 then
      let msgFunc () = CheckIsAuthenticated
      Cmd.OfPromise.either handleRedirect () msgFunc RedirectException
    else
      printfn "no code & state found, cannot log in user"
      tryLogin model.ClientUrl

  let tryGetToken audience =
    Cmd.OfPromise.either getToken audience GetTokenSuccess GetTokenException

  type UpdateResponse = Core.UpdateResponse<Model, Msg, RaisedMsg>

  let update msg model =
    match msg with
    | InitLoginProcess config ->
      UpdateResponse.basic { model with ServerUrl = config.ServerUrl } (tryGetAuth0Config config)
    | TryGetAuth0Config config ->
      UpdateResponse.basic model (tryGetAuth0Config config)
    | GetAuth0ConfigSuccess (Ok auth0conf) ->
      let raised = Auth0ConfigLoaded auth0conf
      let cmd = createClient auth0conf
      let model = { model with Auth0Config = auth0conf }
      UpdateResponse.withRaised model cmd raised
    | GetAuth0ConfigSuccess (Error e) ->
      printfn "Error getting auth0 config: %A" e
      let raised = RaiseError "Unable to contact server." //"Unable to get Auth0 config."
      UpdateResponse.withRaised model Cmd.none raised
    | GetAuth0ConfigError e ->
      printfn "Error getting auth0 config: %A" e
      let raised = RaiseError "Unable to contact server." //"Unable to get Auth0 config."
      UpdateResponse.withRaised model Cmd.none raised
    | TryCreateClient ->
      let cmd = createClient model.Auth0Config
      UpdateResponse.basic model cmd
    | CreateClientSuccess _ ->
      let cmd = Cmd.ofMsg CheckIsAuthenticated
      UpdateResponse.basic model cmd
    | CreateClientError exn ->
      printfn "Create client error is %A" exn
      let raised = RaiseError "Unable to initialize login." // "Unable to create auth0 client"
      UpdateResponse.withRaised model Cmd.none raised
    | TryLogin ->
      let cmd = tryLogin model.ClientUrl
      UpdateResponse.basic model cmd
    | LoginSuccess _ ->
      UpdateResponse.basic model Cmd.none
    | LoginError e ->
      printfn "Login error is %A" e
      let raised = RaiseError "Unable to log in."
      UpdateResponse.withRaised model Cmd.none raised
    | CheckIsAuthenticated ->
      let cmd = checkIsAuthenticated()
      UpdateResponse.basic model cmd
    | AuthenticationResult res ->
      match res with
      | Some true ->
        let cmd = tryGetUser()
        UpdateResponse.basic model cmd
      | Some false ->
        let cmd = tryParseQuery model
        UpdateResponse.basic model cmd
      | None ->
        let cmd = tryLogin model.ClientUrl
        UpdateResponse.basic model cmd
    | AuthenticationCheckError e ->
      printfn "Authentication check error is %A" e
      let raised = RaiseError "Unable to authenticate user."
      UpdateResponse.withRaised model Cmd.none raised
    | TryGetUser ->
      let cmd = tryGetUser()
      UpdateResponse.basic model cmd
    | GotUser user ->
      let user = createFromObject user
      let model = { model with User = Some user }
      let cmd = tryGetToken model.Auth0Config.Audience
      UpdateResponse.basic model cmd
    | GetUserException e ->
      printfn "Get user exception: %A" e
      let raised = RaiseError "Unable to get user information."
      UpdateResponse.withRaised model Cmd.none raised
    | TryLogout ->
      let cmd = tryLogout()
      UpdateResponse.basic model cmd
    | LogoutError e ->
      printfn "logout error: %A" e
      let raised = RaiseError "Error while logging out."
      UpdateResponse.withRaised model Cmd.none raised
    | TryRedirect ->
      let cmd = tryParseQuery model
      UpdateResponse.basic model cmd
    | RedirectException e ->
      printfn "Error during redirect from login: %A" e
      let raised = RaiseError "Error redirecting from login."
      UpdateResponse.withRaised model Cmd.none raised
    | GetTokenSuccess t ->
      let model = { model with JwtToken = Some t }
      let raised = GotToken t
      UpdateResponse.withRaised model Cmd.none raised
    | GetTokenException e ->
      printfn "Error getting token: %A" e
      let raised = RaiseError "Error while getting token."
      UpdateResponse.withRaised model Cmd.none raised
