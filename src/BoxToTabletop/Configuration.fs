module BoxToTabletop.Configuration

open System
open Npgsql
open System.Reflection
open Argu


let private appName () =
    Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyProductAttribute>()
    |> Seq.tryHead
    |> Option.map (fun attr -> attr.Product)
    |> Option.defaultValue "unknown"


type PostgresConfig = {
    PostgresHost : string
    PostgresDatabase : string
    PostgresUsername : string
    PostgresPassword : string
    PostgresMaxPoolSize : int
}   with
    static member Default () = {
            PostgresHost = "localhost"
            PostgresDatabase = "boxtotabletop"
            PostgresUsername = "postgres"
            PostgresPassword = "postgres"
            PostgresMaxPoolSize = 300
        }
    member x.PostgresConnectionStringSetTimeOut (timeout: TimeSpan) =
        let host = x.PostgresHost
        let cn = sprintf "Host=%s;Database=%s;Username=%s;Password=%s" (string host) x.PostgresDatabase x.PostgresUsername x.PostgresPassword
        let builder = NpgsqlConnectionStringBuilder(cn)
        builder.Pooling <- true
        builder.MaxPoolSize <- x.PostgresMaxPoolSize
        builder.Timeout <- timeout.TotalSeconds |> int // conversion because Timeout is in seconds
        builder.CommandTimeout <- timeout.TotalSeconds |> int  // conversion because Timeout is in seconds
        builder.ReadBufferSize <- builder.ReadBufferSize * 8
        builder.WriteBufferSize <- builder.WriteBufferSize * 8
        builder.ApplicationName <- appName ()
        builder.ConnectionString
    member x.PostgresConnectionString () =
        TimeSpan.FromSeconds 60. |> x.PostgresConnectionStringSetTimeOut

    member x.Printable = { x with PostgresPassword = "<password>" }

type Auth0Config = {
    /// The issuer, the URL given by Auth0 for the application
    Domain : string
    /// The url for this API, must match what is in Auth0 for the API for the application
    Audience : string
    /// The client id _of the single page application_ that will be calling this API
    ClientId : string
} with
    static member Empty() = {
        Domain = ""
        Audience = ""
        ClientId = ""
    }

type ApplicationConfig = {
    PostgresConfig : PostgresConfig
    Auth0Config : Auth0Config
} with
    static member Default() = {
        PostgresConfig = PostgresConfig.Default()
        Auth0Config = Auth0Config.Empty()
    }

    static member Create postgresConf auth0Conf = { PostgresConfig = postgresConf; Auth0Config = auth0Conf }


type CLIArguments =
    | Info
    | Version
    | PostgresHost of hostname : string
    | AuthDomain of string
    | AuthAudience of string
    | AuthClientId of string
    | Usage
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Info -> "More detailed information"
            | Version -> "Version of application"
            | PostgresHost _ -> "Postgres hostname"
            | AuthDomain _ -> "The domain as given by Auth0 for the application."
            | AuthAudience _ -> "The URL of this API. Must match the API Audience in Auth0."
            | AuthClientId _ -> "The Client ID of the Application that will be calling this API."
            | Usage -> "Print this usage info."

let parseArgs args =
    let parser = ArgumentParser.Create<CLIArguments>(programName = "BoxToTabletop")
    parser.Parse(args)

let printUsage() =
    let parser = ArgumentParser.Create<CLIArguments>(programName = "BoxToTabletop")
    parser.PrintUsage()

let tryParseAuth0Config (results : ParseResults<CLIArguments>) =
    let domain = results.GetResult(AuthDomain, defaultValue = "")
    let audience = results.GetResult(AuthAudience, defaultValue = "")
    let clientId = results.GetResult(AuthClientId, defaultValue = "")
    if domain = "" then
        Error "Missing domain"
    elif audience = "" then
        Error "Missing Audience"
    elif clientId = "" then
        Error "Missing Client Id"
    else
        {
            Domain = domain
            Audience = audience
            ClientId = clientId
        } |> Ok