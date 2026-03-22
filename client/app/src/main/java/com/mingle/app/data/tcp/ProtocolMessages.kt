@file:OptIn(kotlinx.serialization.ExperimentalSerializationApi::class)

package com.mingle.app.data.tcp

import kotlinx.serialization.Serializable
import kotlinx.serialization.protobuf.ProtoNumber

@Serializable
data class ClientMessage(
    @ProtoNumber(1) val protocolVersion: Int = PROTOCOL_VERSION,
    @ProtoNumber(2) val register: AuthRequest? = null,
    @ProtoNumber(3) val login: AuthRequest? = null,
    @ProtoNumber(4) val me: MeRequest? = null,
    @ProtoNumber(5) val ping: PingRequest? = null,
    @ProtoNumber(6) val profileGet: ProfileGetRequest? = null,
    @ProtoNumber(7) val profileUpdate: ProfileUpdateRequest? = null,
    @ProtoNumber(8) val userSearch: UserSearchRequest? = null
)

@Serializable
data class ServerMessage(
    @ProtoNumber(1) val protocolVersion: Int = PROTOCOL_VERSION,
    @ProtoNumber(2) val authSuccess: AuthSuccess? = null,
    @ProtoNumber(3) val meSuccess: MeSuccess? = null,
    @ProtoNumber(4) val error: ErrorMessage? = null,
    @ProtoNumber(5) val pong: PongMessage? = null,
    @ProtoNumber(6) val profileData: ProfileData? = null,
    @ProtoNumber(7) val profileUpdated: ProfileData? = null,
    @ProtoNumber(8) val userSearchResults: UserSearchResults? = null
)

@Serializable
data class AuthRequest(
    @ProtoNumber(1) val mnemonic: String
)

@Serializable
data class MeRequest(
    @ProtoNumber(1) val token: String
)

@Serializable
data class PingRequest(
    @ProtoNumber(1) val unixTimeMs: Long
)

@Serializable
data class ProfileGetRequest(
    @ProtoNumber(1) val token: String
)

@Serializable
data class ProfileUpdateRequest(
    @ProtoNumber(1) val token: String,
    @ProtoNumber(2) val displayName: String,
    @ProtoNumber(3) val username: String
)

@Serializable
data class UserSearchRequest(
    @ProtoNumber(1) val token: String,
    @ProtoNumber(2) val query: String
)

@Serializable
data class AuthSuccess(
    @ProtoNumber(1) val accessToken: String,
    @ProtoNumber(2) val userId: String
)

@Serializable
data class MeSuccess(
    @ProtoNumber(1) val userId: String
)

@Serializable
data class ErrorMessage(
    @ProtoNumber(1) val code: String,
    @ProtoNumber(2) val message: String
)

@Serializable
data class PongMessage(
    @ProtoNumber(1) val unixTimeMs: Long
)

@Serializable
data class ProfileData(
    @ProtoNumber(1) val userId: String,
    @ProtoNumber(2) val displayName: String,
    @ProtoNumber(3) val username: String,
    @ProtoNumber(4) val lastSeenAtUnixMs: Long
)

@Serializable
data class UserSearchResultItem(
    @ProtoNumber(1) val userId: String,
    @ProtoNumber(2) val displayName: String,
    @ProtoNumber(3) val username: String,
    @ProtoNumber(4) val lastSeenAtUnixMs: Long
)

@Serializable
data class UserSearchResults(
    @ProtoNumber(1) val items: List<UserSearchResultItem> = emptyList()
)

const val PROTOCOL_VERSION = 1
