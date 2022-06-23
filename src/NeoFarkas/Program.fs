module NeoFarkas.Program

open System
open System.Reflection
open System.Threading.Tasks

open Microsoft.AspNetCore.Authorization
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging

open Giraffe

let dirtyVersionMark = if ThisAssembly.Git.IsDirty then "*" else String.Empty

let getVersionDescription () =
    if String.IsNullOrWhiteSpace ThisAssembly.Git.Tag then
        sprintf "%s-%s%s" ThisAssembly.Git.Commit ThisAssembly.Git.Branch dirtyVersionMark
    else
        if String.IsNullOrWhiteSpace ThisAssembly.Git.SemVer.DashLabel then
            sprintf "%s.%s.%s-%s-%s%s"
                ThisAssembly.Git.SemVer.Major
                ThisAssembly.Git.SemVer.Minor
                ThisAssembly.Git.SemVer.Patch
                ThisAssembly.Git.SemVer.DashLabel
                ThisAssembly.Git.Commit
                dirtyVersionMark
        else
            sprintf "%s.%s.%s-%s%s"
                ThisAssembly.Git.SemVer.Major
                ThisAssembly.Git.SemVer.Minor
                ThisAssembly.Git.SemVer.Patch
                ThisAssembly.Git.Commit
                dirtyVersionMark

let accessDenied = RequestErrors.FORBIDDEN "Access Denied"
let mustHaveAccessToken = authorizeByPolicyName "HasAccessToken" accessDenied

let invitationFormFunc : HttpHandler =
    handleContext(
        fun context ->
            async {
                let logger = context.GetService<ILogger>()
                logger.LogTrace("Handling form")

                use reader = new System.IO.StreamReader(context.Request.Body)
                let! content = reader.ReadToEndAsync() |> Async.AwaitTask

                logger.LogDebug(sprintf "form content: %A" content)

                context.SetStatusCode 200
                return Some context
            } |> Async.StartAsTask)


let webApp =
    choose [
        route "/" >=> text $"NeoFarkas {getVersionDescription()} here, reporting for duty."
        POST >=> route "/invitation-request" >=> mustHaveAccessToken >=> Successful.OK "access token accepted"
    ]

let configureApp (app:IApplicationBuilder) =
    app.UseGiraffe webApp

let configureServices (services:IServiceCollection) =
    services
        .AddHttpContextAccessor()
        .AddAuthorization(
            fun options ->
                options.AddPolicy("HasAccessToken",
                    fun policy -> policy.Requirements.Add(Authorization.AccessTokenRequirement(Environment.GetEnvironmentVariable("NEOFARKAS_ACCESS_TOKEN"))))
        )
        .AddScoped<IAuthorizationHandler, Authorization.AccessTokenHandler>()
        .AddGiraffe()
    |> ignore

let configureWebHostBuilder() =
    Host.CreateDefaultBuilder()
        .ConfigureWebHostDefaults(
            fun webHostBuilder ->
                webHostBuilder
                    .Configure(configureApp)
                    .ConfigureServices(configureServices)
                |> ignore)

let configureActorHostedService() =
    { new IHostedService with
        member this.StartAsync(cancellationToken: Threading.CancellationToken): Task =
            failwith "Not Implemented"
        member this.StopAsync(cancellationToken: Threading.CancellationToken): Task =
            failwith "Not Implemented"

      interface IAsyncDisposable with
        member this.DisposeAsync() : ValueTask =
            failwith "Not Implemented" }

[<EntryPoint>]
let main args =
    async {
        return!
            [ configureWebHostBuilder()
                .Build()
                .RunAsync() ]
        |> Task.WhenAny
        |> Async.AwaitTask
    }
    |> Async.RunSynchronously
    |> ignore
    0