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

            let hasExpectedBearerToken (hdrval:string) =
                let parts = hdrval.Split(' ', StringSplitOptions.RemoveEmptyEntries ||| StringSplitOptions.TrimEntries)
                parts.Length = 2 && parts.[0] = "Bearer" && requirement.IsValidAccessToken(parts.[1])

            if header |> Seq.exists hasExpectedBearerToken
            then context.Succeed(requirement)
            else context.Fail()

            System.Threading.Tasks.Task.CompletedTask

    // Custom authorization for Matrix Homeserver requests to the application service (the bot)
    type HomeserverTokenRequirement (token:string) =
        interface IAuthorizationRequirement

        member _.IsValidAccessToken(presentedToken:string) = token = presentedToken

    type HomeserverAccessTokenHandler (httpContext:IHttpContextAccessor, logger:ILogger<HomeserverAccessTokenHandler>) =
        inherit AuthorizationHandler<HomeserverTokenRequirement>()

        override _.HandleRequirementAsync(context:AuthorizationHandlerContext, requirement:HomeserverTokenRequirement) =
            let presentedToken = httpContext.HttpContext.Request.Query.["access_token"]

            if requirement.IsValidAccessToken(presentedToken)
            then context.Succeed(requirement)
            else context.Fail()

            System.Threading.Tasks.Task.CompletedTask




let accessDenied = RequestErrors.FORBIDDEN "Access Denied"
let mustHaveAccessToken = authorizeByPolicyName "HasAccessToken" accessDenied

let matrixAccessDenied : HttpHandler =
    handleContext(
        fun context -> task {
            return! context.WriteJsonAsync {
                Matrix.errcode = Matrix.M_FORBIDDEN
                Matrix.error = "invalid token"
            }
        })

let mustHaveHomeserverToken = authorizeByPolicyName "HasHomeserverToken" matrixAccessDenied

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

let handleMatrixEvent_1  (txid:int) (next:HttpFunc) (context:HttpContext) =
    async {
        use reader = new StreamReader(context.Request.Body)
        let! body = reader.ReadToEndAsync() |> Async.AwaitTask

        ApplicationService.matrixEventActor.Post(body)

        return next context
    } |> Async.StartAsTask

let handleMatrixTransactions (txid:int) =
    handleContext(
        fun context ->
            async{
                use reader = new StreamReader(context.Request.Body)
                let! body = reader.ReadToEndAsync() |> Async.AwaitTask

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
                // let logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("NeoFarkas.Web")

                options.AddPolicy("HasAccessToken",
                    fun policy ->
                        let token = nfOptions.Value.AccessToken
                        policy.Requirements.Add(Authorization.AccessTokenRequirement(token)))
                options.AddPolicy("HasHomeserverToken",
                    fun policy ->
                        let token = nfOptions.Value.HomeserverToken
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
