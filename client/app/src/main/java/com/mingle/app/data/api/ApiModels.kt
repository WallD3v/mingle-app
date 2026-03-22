package com.mingle.app.data.api

data class AuthRequest(val mnemonic: String)

data class AuthResponse(
    val accessToken: String,
    val userId: String
)

data class ErrorResponse(
    val code: String,
    val message: String
)

data class MeResponse(val userId: String)
