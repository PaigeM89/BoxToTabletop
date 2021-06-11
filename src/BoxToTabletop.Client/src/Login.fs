namespace BoxToTabletop.Client

open Fable.React

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

  // let createFromObject (jsObj : obj) =
  //   let user = User()
  //   user.name <- jsObj?name
  //   user.given_name <- jsObj?given_name
  //   user.nickname <- jsObj?nickname
  //   user.email <- jsObj?email
  //   user.email_verified <- jsObj?email_verified
  //   user.locale <- jsObj?locale
  //   user.user_id <- jsObj?user_id
  //   user

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
  
  type Model = {
    User : UserRecord option
    JwtToken : string option
  } with
    static member Empty() = { User = None; JwtToken = None }

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
  let getToken : unit -> Promise<string> = jsNative

  let checkIsAuthenticated() =
    Cmd.OfPromise.either (isAuthenticated) () AuthenticationResult AuthenticationCheckError

  let tryLogin() =
    let tryLogin() = login (Some "localhost:8090")
    Cmd.OfPromise.either tryLogin () LoginSuccess LoginError

  let createClient() =
    let createClient() = configureClient("dev-6duts2ta.us.auth0.com", "znj5EvPfoPrzk7B7JF2hGmws8mdXVXqJ")
    Cmd.OfPromise.either createClient () CreateClientSuccess CreateClientError

  let tryGetUser() =
    Cmd.OfPromise.either getUser () GotUser GetUserException

  let tryLogout() =
    Cmd.OfPromise.attempt logout () LogoutError

  let tryParseQuery() =
    let query = Browser.Dom.window.location.search
    let codeIndex = query.IndexOf("code=")
    let stateIndex = query.IndexOf("state=")
    if codeIndex > 0 && stateIndex > 0 then
      Cmd.OfPromise.attempt handleRedirect () RedirectException
    else
      printfn "no code & state found, cannot log in user"
      tryLogin()

  let tryGetToken() =
    Cmd.OfPromise.either getToken () GetTokenSuccess GetTokenException

  let update msg model =
    match msg with
    | TryCreateClient ->
      model, Cmd.none
    | CreateClientSuccess _ ->
      model, Cmd.ofMsg CheckIsAuthenticated
    | CreateClientError exn ->
      printfn "Create client error is %A" exn
      model, Cmd.none
    | TryLogin ->
      model, tryLogin()
    | LoginSuccess o ->
      model, Cmd.none
    | LoginError e ->
      printfn "Login error is %A" e
      model, Cmd.none
    | CheckIsAuthenticated ->
      model, checkIsAuthenticated()
    | AuthenticationResult res ->
      match res with
      | Some true ->
        model, tryGetUser()
      | Some false ->
        model, tryParseQuery()
      | None ->
        model, tryLogin()
    | AuthenticationCheckError e ->
      printfn "Authentication check error is %A" e
      model, Cmd.none
    | TryGetUser -> model, tryGetUser()
    | GotUser user ->
      let user = createFromObject user
      { model with User = Some user }, tryGetToken()
    | GetUserException e ->
      printfn "Get user exception: %A" e
      model, Cmd.none
    | TryLogout ->
      model, tryLogout()
    | LogoutError e ->
      printfn "logout error: %A" e
      model, Cmd.none
    | TryRedirect -> model, tryParseQuery()
    | RedirectException e ->
      printfn "Error during redirect from login: %A" e
      model, Cmd.none
    | GetTokenSuccess t ->
      { model with JwtToken = Some t }, Cmd.none
    | GetTokenException e ->
      printfn "Error getting token: %A" e
      model, Cmd.none
