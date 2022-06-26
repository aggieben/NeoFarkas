module NeoFarkas.ApplicationService

open System.Threading
open System.Threading.Tasks
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging

let matrixEventActor =
    MailboxProcessor<string>.Start(
        fun inbox ->
            async {
                let! msg = inbox.Receive()
                printfn "received matrix event with payload: %A" msg
                return ()
            })

type MatrixApplicationService(logger:ILogger<MatrixApplicationService>) =
    interface IHostedService with
        member this.StartAsync(cancellationToken: CancellationToken): Task =
            logger.LogTrace("started matrix application service")
            Task.CompletedTask

        member this.StopAsync(cancellationToken: CancellationToken): Task =
            logger.LogTrace("stopped matrix application service")
            Task.CompletedTask