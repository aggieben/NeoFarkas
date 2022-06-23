module NeoFarkas.ApplicationService

open System.Threading
open System.Threading.Tasks
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging

type MatrixApplicationService(logger:ILogger<MatrixApplicationService>) =
    interface IHostedService with
        member this.StartAsync(cancellationToken: CancellationToken): Task =
            logger.LogTrace("started matrix application service")
            Task.CompletedTask

        member this.StopAsync(cancellationToken: CancellationToken): Task =
            logger.LogTrace("stopped matrix application service")
            Task.CompletedTask