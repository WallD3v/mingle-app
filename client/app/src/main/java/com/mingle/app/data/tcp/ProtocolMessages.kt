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
    @ProtoNumber(8) val userSearch: UserSearchRequest? = null,
    @ProtoNumber(9) val dialogsList: DialogsListRequest? = null,
    @ProtoNumber(10) val dialogOpen: DialogOpenRequest? = null,
    @ProtoNumber(11) val messageSend: MessageSendRequest? = null,
    @ProtoNumber(12) val subscribeUpdates: SubscribeUpdatesRequest? = null
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
    @ProtoNumber(8) val userSearchResults: UserSearchResults? = null,
    @ProtoNumber(9) val dialogsData: DialogsData? = null,
    @ProtoNumber(10) val dialogData: DialogData? = null,
    @ProtoNumber(11) val messageSent: MessageSent? = null,
    @ProtoNumber(12) val subscribed: Subscribed? = null,
    @ProtoNumber(13) val messageReceived: MessageReceived? = null
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
data class DialogsListRequest(
    @ProtoNumber(1) val token: String
)

@Serializable
data class DialogOpenRequest(
    @ProtoNumber(1) val token: String,
    @ProtoNumber(2) val peerUserId: String
)

@Serializable
data class MessageSendRequest(
    @ProtoNumber(1) val token: String,
    @ProtoNumber(2) val peerUserId: String,
    @ProtoNumber(3) val text: String
)

@Serializable
data class SubscribeUpdatesRequest(
    @ProtoNumber(1) val token: String
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

@Serializable
data class UserPreview(
    @ProtoNumber(1) val userId: String,
    @ProtoNumber(2) val displayName: String,
    @ProtoNumber(3) val username: String,
    @ProtoNumber(4) val lastSeenAtUnixMs: Long
)

@Serializable
data class DialogMessage(
    @ProtoNumber(1) val messageId: String,
    @ProtoNumber(2) val dialogId: String,
    @ProtoNumber(3) val senderUserId: String,
    @ProtoNumber(4) val text: String,
    @ProtoNumber(5) val createdAtUnixMs: Long
)

@Serializable
data class DialogListItem(
    @ProtoNumber(1) val dialogId: String,
    @ProtoNumber(2) val peer: UserPreview? = null,
    @ProtoNumber(3) val lastMessageText: String,
    @ProtoNumber(4) val lastMessageAtUnixMs: Long
)

@Serializable
data class DialogsData(
    @ProtoNumber(1) val items: List<DialogListItem> = emptyList()
)

@Serializable
data class DialogData(
    @ProtoNumber(1) val dialogId: String,
    @ProtoNumber(2) val peer: UserPreview? = null,
    @ProtoNumber(3) val messages: List<DialogMessage> = emptyList()
)

@Serializable
data class MessageSent(
    @ProtoNumber(1) val message: DialogMessage? = null,
    @ProtoNumber(2) val peer: UserPreview? = null
)

@Serializable
data class Subscribed(
    @ProtoNumber(1) val userId: String
)

@Serializable
data class MessageReceived(
    @ProtoNumber(1) val message: DialogMessage? = null,
    @ProtoNumber(2) val from: UserPreview? = null
)

const val PROTOCOL_VERSION = 1
