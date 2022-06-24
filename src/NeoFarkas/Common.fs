module NeoFarkas.Common

open System

let dirtyVersionMark = if ThisAssembly.Git.IsDirty then "*" else String.Empty

let getVersionDescription () =
    if String.IsNullOrWhiteSpace ThisAssembly.Git.Tag then
        sprintf "%s-%s%s" ThisAssembly.Git.Commit ThisAssembly.Git.Branch dirtyVersionMark
    else
        if String.IsNullOrWhiteSpace ThisAssembly.Git.SemVer.DashLabel then
            sprintf "%s.%s.%s-%s%s%s"
                ThisAssembly.Git.SemVer.Major
                ThisAssembly.Git.SemVer.Minor
                ThisAssembly.Git.SemVer.Patch
                ThisAssembly.Git.SemVer.DashLabel
                ThisAssembly.Git.Commit
                dirtyVersionMark
        else
            sprintf "%s.%s.%s%s%s"
                ThisAssembly.Git.SemVer.Major
                ThisAssembly.Git.SemVer.Minor
                ThisAssembly.Git.SemVer.Patch
                ThisAssembly.Git.Commit
                dirtyVersionMark

type NeoFarkasOptions() =
    member val AccessToken = "" with get, set
    member val ApplicationServiceToken = "" with get, set
    member val HomeserverToken = "" with get, set