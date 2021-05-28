module BoxToTabletop.Configuration

open System
open Npgsql
open System.Reflection

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

type ApplicationConfig = {
    PostgresConfig : PostgresConfig
} with
    static member Default() = {
        PostgresConfig = PostgresConfig.Default()
    }

    static member Create postgresConf = { PostgresConfig = postgresConf}
