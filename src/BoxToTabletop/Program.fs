namespace BoxToTabletop

open System.Reflection

module AssemblyInfo =

    let metaDataValue (mda: AssemblyMetadataAttribute) = mda.Value

    let getMetaDataAttribute (assembly: Assembly) key =
        assembly.GetCustomAttributes(typedefof<AssemblyMetadataAttribute>)
        |> Seq.cast<AssemblyMetadataAttribute>
        |> Seq.find (fun x -> x.Key = key)

    let getReleaseDate assembly =
        "ReleaseDate"
        |> getMetaDataAttribute assembly
        |> metaDataValue

    let getGitHash assembly =
        "GitHash"
        |> getMetaDataAttribute assembly
        |> metaDataValue

    let getVersion assembly =
        "AssemblyVersion"
        |> getMetaDataAttribute assembly
        |> metaDataValue

    let assembly = lazy (Assembly.GetEntryAssembly())

    let printVersion() =
        let version = assembly.Force().GetName().Version
        printfn "%A" version

    let printInfo() =
        let assembly = assembly.Force()
        let name = assembly.GetName()
        let version = assembly.GetName().Version
        let releaseDate = getReleaseDate assembly
        let githash = getGitHash assembly
        printfn "%s - %A - %s - %s" name.Name version releaseDate githash

module Say =
    open System

    let nothing name = name |> ignore

    let hello name = sprintf "Hello %s" name

    let colorizeIn color str =
        let oldColor = Console.ForegroundColor
        Console.ForegroundColor <- (Enum.Parse(typedefof<ConsoleColor>, color) :?> ConsoleColor)
        printfn "%s" str
        Console.ForegroundColor <- oldColor

module Main =
    open Argu
    open Microsoft.AspNetCore.Builder
    open Microsoft.AspNetCore.Hosting
    open Microsoft.Extensions.Hosting
    open Microsoft.Extensions.DependencyInjection
    open Giraffe
    open BoxToTabletop

    type CLIArguments =
        | Info
        | Version
        | Favorite_Color of string // Look in App.config
        | Run
        interface IArgParserTemplate with
            member s.Usage =
                match s with
                | Info -> "More detailed information"
                | Version -> "Version of application"
                | Favorite_Color _ -> "Favorite color"
                | Run -> "Run the server"

    [<EntryPoint>]
    let main (argv: string array) =
        let parser = ArgumentParser.Create<CLIArguments>(programName = "BoxToTabletop")
        let results = parser.Parse(argv)
        if results.Contains Version then
            AssemblyInfo.printVersion()
        elif results.Contains Info then
            AssemblyInfo.printInfo()
        elif results.Contains Run then
            let host = BoxToTabletop.Webhost.buildHost()
            let thing = host.Run()
            ()
        else
            parser.PrintUsage() |> printfn "%s"
        0
