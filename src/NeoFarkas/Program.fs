module NeoFarkas.Program

open System

open Microsoft.Extensions.Hosting

[<EntryPoint>]
let main args =
    Web.configureWebHostBuilder()
        .Build()
        .Run()

    0