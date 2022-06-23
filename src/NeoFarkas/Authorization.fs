module NeoFarkas.Authorization

open System
open Microsoft.AspNetCore.Authorization
open Microsoft.Extensions.Logging
open Microsoft.AspNetCore.Http

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

