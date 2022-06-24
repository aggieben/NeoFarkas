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


type private AccessTokenRequirement (token:string) =
    interface IAuthorizationRequirement

    member _.IsValidAccessToken(presentedToken:string) = token = presentedToken

type private AccessTokenHandler (httpContext:IHttpContextAccessor, logger:ILogger<AccessTokenHandler>) =
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
        route "/" >=> text $"NeoFarkas {Common.getVersionDescription()} here, reporting for duty."
        POST >=> route "/invitation-request" >=> mustHaveAccessToken >=> Successful.OK "access token accepted"
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
        .AddScoped<IAuthorizationHandler, AccessTokenHandler>()
        .AddHostedService<ApplicationService.MatrixApplicationService>()
        .AddGiraffe()
        .AddAuthorization(
            fun options ->
                let sp = services.BuildServiceProvider()

                options.AddPolicy("HasAccessToken",
                    fun policy ->
                        let nfOptions = sp.GetRequiredService<IOptionsSnapshot<Common.NeoFarkasOptions>>()
                        let logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("NeoFarkas.Web")
                        let token = nfOptions.Value.AccessToken
                        logger.LogDebug("Configured with neofarkas access token: '{0}'", Regex.Replace(token, ".", "*"))

                        policy.Requirements.Add(AccessTokenRequirement(token)))
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
