namespace BoxToTabletop.Client

open Fable.React

// module Login =
//   open Fable.Core
//   open Fable.Core.JsInterop
//   open Auth0Client
//   open globals

//   // // ts2fable 0.7.1
//   // module rec auth0Module =
//   //   open System
//   //   open Fable.Core
//   //   open Fable.Core.JS

//   //   type Auth0ClientOptions = Auth0ClientOptions

//   //   type [<AllowNullLiteral>] IExports =
//   //       abstract createAuth0Client: options: Auth0ClientOptions -> unit

//   // type ClientCredentials = {
//   //   domain : string
//   //   client_id : string
//   // }

//   type IAuth0 =
//     [<Emit("createAuth0Client($0)")>]
//     abstract createAuth0Client : Auth0ClientOptions -> Auth0Client
//     //ClientCredentials -> obj
//     // abstract isAuthenticated : unit -> bool

//   //[<Import("*", from="@auth0/auth0-spa-js")>]
//   [<ImportAll("@auth0/auth0-spa-js")>]
//   let auth0Module : IAuth0 = jsNative
//   // [<Emit("await createAuth0Client($0)")>]
//   // let creatAuth0Client 

//   // [<ImportMember("@auth0/auth0-spa-js/createAuth0Client")>]
//   // let test : obj -> obj = jsNative

//   type Auth0ClientOpts() =
//     interface Auth0ClientOptions with
//       override val domain = "" with get, set
//       override val client_id = "" with get, set
//       override val issuer = None with get, set
//       override val redirect_uri = None with get, set
//       override val leeway = None with get, set
//       override val cacheLocation = None with get, set
//       override val useRefreshTokens = Some (true) with get, set
//       override val authorizeTimeoutInSeconds = None with get, set
//       override val auth0Client = None with get, set
//       override val legacySameSiteCookie = None with get, set
//       override val useCookiesForTransactions = None with get, set
//       override val advancedOptions = None with get, set
//       override val sessionCheckExpiryDays = None with get, set
//       //base login options
//       override val display = None with get, set
//       override val prompt  = None with get, set
//       override val max_age  = None with get, set
//       override val ui_locales  = None with get, set
//       override val id_token_hint  = None with get, set
//       override val screen_hint = None with get, set
//       override val login_hint = None with get, set
//       override val acr_values  = None with get, set
//       override val scope = None with get, set
//       override val audience = None with get, set
//       override val connection = None with get, set
//       override val organization = None with get, set
//       override val invitation  = None with get, set
//       override val Item = (fun _ -> None) with get, set

//   let login() =
//     let redirectUri = Browser.Dom.window.location.origin
//     // let creds = {
//     //   Auth0ClientOptions.domain = "dev-6duts2ta.us.auth0.com"
//     //   Auth0ClientOptions.client_id = "znj5EvPfoPrzk7B7JF2hGmws8mdXVXqJ"
//     // }
//     let creds = Auth0ClientOpts() :> Auth0ClientOptions
//     creds.domain <- "dev-6duts2ta.us.auth0.com"
//     creds.client_id <- "znj5EvPfoPrzk7B7JF2hGmws8mdXVXqJ"
//     let auth0 : IAuth0 = importAll "@auth0/auth0-spa-js" //importDefault
//     // let authFunc = import "createAuth0Client" "@auth0/auth0-spa-js"
//     let client = auth0.createAuth0Client creds
//     //let auth0 = auth0Module.IExports.createAuth0Client creds

//     printfn "Auth0 client result is %A" client
//     // let isAuthed = auth0Module.isAuthenticated()
//     // printfn "IsAuthenticated is %A" isAuthed
//     ()

module Login =
  open Fable.Core
  open Fable.Core.JsInterop
  open Elmish
  open Fable.Core.JS

  type [<AllowNullLiteral>] User() =
    member val name : string option = None with get, set
    member val given_name: string option = None with get, set
    member val family_name: string option = None with get, set
    member val middle_name: string option = None with get, set
    member val nickname: string option = None with get, set
    member val preferred_username: string option = None with get, set
    member val profile: string option = None with get, set
    member val picture: string option = None with get, set
    member val website: string option = None with get, set
    member val email: string option = None with get, set
    member val email_verified: bool option = None with get, set
    member val gender: string option = None with get, set
    member val birthdate: string option = None with get, set
    member val zoneinfo: string option = None with get, set
    member val locale: string option = None with get, set
    member val phone_number: string option = None with get, set
    member val phone_number_verified: bool option = None with get, set
    member val address: string option = None with get, set
    member val updated_at: string option = None with get, set
    member val sub: string option = None with get, set
    member val user_id : string option = None with get, set

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

  let createFromObject (jsObj : obj) =
    let user = User()
    user.name <- jsObj?name
    user.given_name <- jsObj?given_name
    user.nickname <- jsObj?nickname
    user.email <- jsObj?email
    user.email_verified <- jsObj?email_verified
    user.locale <- jsObj?locale
    user.user_id <- jsObj?user_id
    user

  let tryProp jsObj prop =
    let v = jsObj?(prop)
    printfn "v is %A" v
    Some v

  let createFromObject2 (jsObj : obj) =
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
  
  type Model = {
    User : UserRecord option
  } with
    static member Empty() = { User = None }

    // [<Emit "$0[$1]{{=$2}}">]member val Item: string -> obj option = None with get, set

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

  let checkIsAuthenticated() =
    Cmd.OfPromise.either (isAuthenticated) () AuthenticationResult AuthenticationCheckError

  let tryLogin() =
    printfn "logging in in login"
    let tryLogin() = login (Some "localhost:8090")
    Cmd.OfPromise.either tryLogin () LoginSuccess LoginError

  let createClient() =
    // let redirectUri = Browser.Dom.window.location.origin
    printfn "creating client in login"
    let createClient() = configureClient("dev-6duts2ta.us.auth0.com", "znj5EvPfoPrzk7B7JF2hGmws8mdXVXqJ")
    Cmd.OfPromise.either createClient () CreateClientSuccess CreateClientError

  let tryGetUser() =
    printfn "Getting user"
    Cmd.OfPromise.either getUser () GotUser GetUserException

  let tryLogout() =
    printfn "Logging out..."
    Cmd.OfPromise.attempt logout () LogoutError

  let tryParseQuery() =
    let query = Browser.Dom.window.location.search
    let codeIndex = query.IndexOf("code=")
    let stateIndex = query.IndexOf("state=")
    if codeIndex > 0 && stateIndex > 0 then
      printfn "Code & state indexes are %i, %i" codeIndex stateIndex
      Cmd.OfPromise.attempt handleRedirect () RedirectException
    else
      printfn "no code & state found, cannot log in user"
      tryLogin()


  let update msg model =
    match msg with
    | TryCreateClient ->
      model, Cmd.none
    | CreateClientSuccess _ -> //client ->
      //printfn "Client success is %A" client
      printfn "create client success"
      model, Cmd.ofMsg CheckIsAuthenticated
    | CreateClientError exn ->
      printfn "Create client error is %A" exn
      model, Cmd.none
    | TryLogin ->
      printfn "message handler trying login"
      model, tryLogin()
    | LoginSuccess o ->
      printfn "Login result is probably successful: %A" o
      model, Cmd.none
    | LoginError e ->
      printfn "Login error is %A" e
      model, Cmd.none
    | CheckIsAuthenticated ->
      printfn "Checking is authenticated..."
      model, checkIsAuthenticated()
    | AuthenticationResult res ->
      printfn "Auth result is %A" res
      match res with
      | Some true ->
        printfn "user is authenticated"
        model, tryGetUser()
      | Some false ->
        printfn "user is not authenticated"
        model, tryParseQuery()
      | None ->
        printfn "Unable to verify authenticatoin status"
        model, tryLogin()
    | AuthenticationCheckError e ->
      printfn "Authentication check error is %A" e
      model, Cmd.none
    | TryGetUser -> model, tryGetUser()
    | GotUser user ->
      let user = createFromObject2 user
      printfn "User after parsing is %A" user
      { model with User = Some user }, Cmd.none
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
