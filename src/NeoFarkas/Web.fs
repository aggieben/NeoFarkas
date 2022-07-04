module NeoFarkas.Web

open System
open System.Text.RegularExpressions

open Microsoft.AspNetCore.Authorization
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging

open Giraffe
open Microsoft.Extensions.Options
open System.IO

module Authorization =

    // Custom authorization for management API
    type AccessTokenRequirement (token:string) =
        interface IAuthorizationRequirement
        member _.IsValidAccessToken(presentedToken:string) = token = presentedToken

    type AccessTokenHandler (httpContext:IHttpContextAccessor, logger:ILogger<AccessTokenHandler>) =
        inherit AuthorizationHandler<AccessTokenRequirement>()

        override _.HandleRequirementAsync(context:AuthorizationHandlerContext, requirement:AccessTokenRequirement) =
            let header = httpContext.HttpContext.Request.Headers.Authorization

            logger.LogDebug("authorization header(s): {0}", sprintf "%A" header)

            let hasExpectedBearerToken (hdrval:string) =
                let parts = hdrval.Split(' ', StringSplitOptions.RemoveEmptyEntries ||| StringSplitOptions.TrimEntries)
                logger.LogDebug("validating presented token: '{0}'", parts.[1])
                parts.Length = 2 && parts.[0] = "Bearer" && requirement.IsValidAccessToken(parts.[1])

            if header |> Seq.exists hasExpectedBearerToken
            then context.Succeed(requirement)
            else context.Fail()

            System.Threading.Tasks.Task.CompletedTask

    // Custom authorization for Matrix Homeserver requests to the application service (the bot)
    type HomeserverTokenRequirement (token:string) =
        interface IAuthorizationRequirement
        member _.IsValidAccessToken(presentedToken:string) = token = presentedToken

    type HomeserverTokenHandler (httpContext:IHttpContextAccessor, logger:ILogger<HomeserverTokenHandler>) =
        inherit AuthorizationHandler<HomeserverTokenRequirement>()

        override _.HandleRequirementAsync(context:AuthorizationHandlerContext, requirement:HomeserverTokenRequirement) =
            let presentedToken = httpContext.HttpContext.Request.Query.["access_token"]

            logger.LogDebug("evaluating presented HS token: '{0}'", presentedToken)

            if requirement.IsValidAccessToken(presentedToken)
            then context.Succeed(requirement)
            else context.Fail()

            System.Threading.Tasks.Task.CompletedTask

let Policies = {|
    HasAccessToken = "HasAccessToken"
    HasHomeserverToken = "HasHomeserverToken"
|}

let accessDenied =
     RequestErrors.FORBIDDEN "Access Denied"

let mustHaveAccessToken =
    authorizeByPolicyName Policies.HasAccessToken accessDenied

let matrixAccessDenied : HttpHandler =
    handleContext(
        fun context -> task {
            context.SetStatusCode 403
            return! context.WriteJsonAsync {
                Matrix.errcode = Matrix.M_FORBIDDEN
                Matrix.error = "invalid token"
            }
        })

let mustHaveHomeserverToken =
    authorizeByPolicyName Policies.HasHomeserverToken matrixAccessDenied

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

let handleMatrixTransactions (txid:int) =
    handleContext(
        fun context ->
            async{
                let logger = context.GetService<ILoggerFactory>().CreateLogger("NeoFarkas.Web")
                use reader = new StreamReader(context.Request.Body)
                let! body = reader.ReadToEndAsync() |> Async.AwaitTask

                logger.LogDebug("Matrix event payload: {0}", body)

                ApplicationService.matrixEventActor.Post(body)

                return Some context
            } |> Async.StartAsTask)

let matrixHandlers : Map<string, HttpHandler> =
    Map.empty
    |> Map.add "transactions"
        (mustHaveHomeserverToken >=> choose [
           PUT >=> routeCif "/_matrix/app/v1/transactions/%i" handleMatrixTransactions
           PUT >=> routeCif "/transactions/%i" handleMatrixTransactions
        ])

let webApp =
    choose [
        route "/" >=> text $"NeoFarkas {Common.getVersionDescription()} here, reporting for duty."

        POST >=> routeCi "/invitation-request"
             >=> mustHaveAccessToken
             >=> Successful.OK "access token accepted"

        matrixHandlers.["transactions"]
    ]

let configureApp (app:IApplicationBuilder) =
    app.UseGiraffe webApp

let configureServices (services:IServiceCollection) =
    services
        .AddOptions<Common.NeoFarkasOptions>()
        .BindConfiguration("NeoFarkas")
    |> ignore

    services
        .AddHttpContextAccessor()
        .AddScoped<IAuthorizationHandler, Authorization.AccessTokenHandler>()
        .AddHostedService<ApplicationService.MatrixApplicationService>()
        .AddGiraffe()
        .AddAuthorization(
            fun options ->
                let sp = services.BuildServiceProvider()
                let nfOptions = sp.GetRequiredService<IOptionsSnapshot<Common.NeoFarkasOptions>>()
                let logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("NeoFarkas.Web")

                options.AddPolicy(Policies.HasAccessToken,
                    fun policy ->
                        let token = nfOptions.Value.AccessToken
                        logger.LogDebug("Initializing HasAccessToken policy with token: '{0}'", token)
                        policy.Requirements.Add(Authorization.AccessTokenRequirement(token)))
                options.AddPolicy(Policies.HasHomeserverToken,
                    fun policy ->
                        let token = nfOptions.Value.HomeserverToken
                        logger.LogDebug("Initializing HasHomeserverToken policy with token: '{0}'", token)
                        policy.Requirements.Add(Authorization.HomeserverTokenRequirement(token)))

        )
    |> ignore

let configureWebHostBuilder() =
    Host.CreateDefaultBuilder()
        .ConfigureWebHostDefaults(
            fun webHostBuilder ->
                webHostBuilder
                    .Configure(configureApp)
                    .ConfigureServices(configureServices)
                |> ignore)
