module NeoFarkas.Program

open System
open System.Threading.Tasks

open Microsoft.Extensions.Hosting

[<EntryPoint>]
let main args =
    async {
        return!
            [ Web.configureWebHostBuilder()
                .Build()
                .RunAsync() ]
        |> Task.WhenAny
        |> Async.AwaitTask
    }
    |> Async.RunSynchronously
    |> ignore
    0