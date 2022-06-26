module NeoFarkas.Matrix

type ErrorCode =
    | M_FORBIDDEN

type StandardError = {
    errcode : ErrorCode
    error : string
}