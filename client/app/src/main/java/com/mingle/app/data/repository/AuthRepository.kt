package com.mingle.app.data.repository

import android.util.Log
import com.mingle.app.data.mnemonic.MnemonicUtils
import com.mingle.app.data.storage.SecureSessionStore
import com.mingle.app.data.tcp.TcpAuthClient

sealed class AuthResult {
    data class Success(val userId: String) : AuthResult()
    data class Failure(val message: String) : AuthResult()
}

data class ProfileModel(
    val userId: String,
    val displayName: String,
    val username: String,
    val lastSeenAtUnixMs: Long,
    val isOnline: Boolean
)

data class UserSearchModel(
    val userId: String,
    val displayName: String,
    val username: String,
    val lastSeenAtUnixMs: Long,
    val isOnline: Boolean
)

data class DialogListItemModel(
    val dialogId: String,
    val peer: UserSearchModel,
    val lastMessageText: String,
    val lastMessageAtUnixMs: Long,
    val unreadCount: Int
)

data class DialogMessageModel(
    val messageId: String,
    val dialogId: String,
    val senderUserId: String,
    val text: String,
    val createdAtUnixMs: Long,
    val readByRecipientAtUnixMs: Long
)

data class DialogThreadModel(
    val dialogId: String?,
    val peer: UserSearchModel,
    val messages: List<DialogMessageModel>,
    val hasMoreBefore: Boolean,
    val oldestLoadedUnixMs: Long
)

data class IncomingMessageModel(
    val message: DialogMessageModel,
    val from: UserSearchModel
)

data class MessageReadUpdateModel(
    val dialogId: String,
    val readerUserId: String,
    val readAtUnixMs: Long
)

data class PresenceUpdateModel(
    val userId: String,
    val isOnline: Boolean,
    val lastSeenAtUnixMs: Long
)

sealed class ProfileResult {
    data class Success(val profile: ProfileModel) : ProfileResult()
    data class Failure(val message: String) : ProfileResult()
}

sealed class UserSearchResult {
    data class Success(val items: List<UserSearchModel>) : UserSearchResult()
    data class Failure(val message: String) : UserSearchResult()
}

sealed class DialogListResult {
    data class Success(val items: List<DialogListItemModel>) : DialogListResult()
    data class Failure(val message: String) : DialogListResult()
}

sealed class DialogThreadResult {
    data class Success(val thread: DialogThreadModel) : DialogThreadResult()
    data class Failure(val message: String) : DialogThreadResult()
}

sealed class SendMessageResult {
    data class Success(val message: DialogMessageModel) : SendMessageResult()
    data class Failure(val message: String) : SendMessageResult()
}

internal fun mapServerErrorToRuMessage(errorCode: String?): String {
    return when (errorCode) {
        "INVALID_MNEMONIC" -> "Неверная секретная фраза"
        "UNAUTHORIZED" -> "Аккаунт не найден"
        "USERNAME_TAKEN" -> "Этот username уже занят"
        "INVALID_USERNAME" -> "Неверный формат username"
        "USER_NOT_FOUND" -> "Пользователь не найден"
        "INVALID_MESSAGE" -> "Пустое сообщение нельзя отправить"
        else -> "Внутренняя ошибка сервера"
    }
}

class AuthRepository(
    private val tcpClient: TcpAuthClient,
    private val sessionStore: SecureSessionStore
) {
    private val tag = "AuthRepository"
    fun getCurrentUserId(): String? = sessionStore.getUserId()
    fun setIncomingMessageListener(listener: ((IncomingMessageModel) -> Unit)?) {
        if (listener == null) {
            tcpClient.setMessageListener(null)
            return
        }

        tcpClient.setMessageListener { incoming ->
            val message = incoming.message ?: return@setMessageListener
            val from = incoming.from ?: return@setMessageListener
            listener(
                IncomingMessageModel(
                    message = DialogMessageModel(
                        messageId = message.messageId,
                        dialogId = message.dialogId,
                        senderUserId = message.senderUserId,
                        text = message.text,
                        createdAtUnixMs = message.createdAtUnixMs,
                        readByRecipientAtUnixMs = message.readByRecipientAtUnixMs
                    ),
                    from = UserSearchModel(
                        userId = from.userId,
                        displayName = from.displayName,
                        username = from.username,
                        lastSeenAtUnixMs = from.lastSeenAtUnixMs,
                        isOnline = from.isOnline
                    )
                )
            )
        }
    }

    fun setMessageReadUpdateListener(listener: ((MessageReadUpdateModel) -> Unit)?) {
        if (listener == null) {
            tcpClient.setMessageReadListener(null)
            return
        }

        tcpClient.setMessageReadListener { update ->
            listener(
                MessageReadUpdateModel(
                    dialogId = update.dialogId,
                    readerUserId = update.readerUserId,
                    readAtUnixMs = update.readAtUnixMs
                )
            )
        }
    }

    fun setPresenceUpdateListener(listener: ((PresenceUpdateModel) -> Unit)?) {
        if (listener == null) {
            tcpClient.setPresenceListener(null)
            return
        }

        tcpClient.setPresenceListener { update ->
            listener(
                PresenceUpdateModel(
                    userId = update.userId,
                    isOnline = update.isOnline,
                    lastSeenAtUnixMs = update.lastSeenAtUnixMs
                )
            )
        }
    }

    suspend fun subscribeRealtimeUpdates(): Boolean {
        val token = sessionStore.getToken() ?: return false
        return try {
            val response = tcpClient.subscribeUpdates(token)
            response.subscribed != null && response.error == null
        } catch (ex: Exception) {
            Log.e(tag, "TCP subscribe updates failed", ex)
            false
        }
    }

    suspend fun setAppInForeground(): Boolean {
        tcpClient.setAppInForeground(true)
        return subscribeRealtimeUpdates()
    }

    suspend fun setAppInBackground() {
        tcpClient.setAppInForeground(false)
        try {
            tcpClient.ping()
        } catch (ex: Exception) {
            Log.w(tag, "Background ping failed", ex)
        }
    }

    suspend fun register(mnemonicInput: String): AuthResult {
        return authInternal(mnemonicInput, isRegister = true)
    }

    suspend fun login(mnemonicInput: String): AuthResult {
        return authInternal(mnemonicInput, isRegister = false)
    }

    suspend fun hasValidSession(): Boolean {
        val token = sessionStore.getToken() ?: return false

        return try {
            val response = tcpClient.me(token)
            when {
                response.meSuccess != null -> true
                response.error?.code == "UNAUTHORIZED" -> {
                    Log.w(tag, "Session token unauthorized on /me via TCP.")
                    sessionStore.clear()
                    false
                }
                else -> {
                    Log.w(tag, "Unexpected /me response: $response")
                    false
                }
            }
        } catch (ex: Exception) {
            Log.e(tag, "TCP /me request failed", ex)
            false
        }
    }

    fun loadMnemonicWordsFromInput(input: String): List<String> {
        return MnemonicUtils.parseWords(input).take(24)
    }

    fun generateMnemonic(): String = MnemonicUtils.generate24Words()

    suspend fun getProfile(): ProfileResult {
        val token = sessionStore.getToken() ?: return ProfileResult.Failure("Нужна авторизация")

        return try {
            val response = tcpClient.getProfile(token)
            when {
                response.profileData != null -> {
                    val profile = response.profileData
                    ProfileResult.Success(
                        ProfileModel(
                            userId = profile.userId,
                            displayName = profile.displayName,
                            username = profile.username,
                            lastSeenAtUnixMs = profile.lastSeenAtUnixMs,
                            isOnline = profile.isOnline
                        )
                    )
                }
                response.error != null -> ProfileResult.Failure(mapServerErrorToRuMessage(response.error.code))
                else -> ProfileResult.Failure("Внутренняя ошибка сервера")
            }
        } catch (ex: Exception) {
            Log.e(tag, "TCP getProfile failed", ex)
            ProfileResult.Failure("Ошибка сети. Проверьте соединение")
        }
    }

    suspend fun updateProfile(displayName: String, username: String): ProfileResult {
        val token = sessionStore.getToken() ?: return ProfileResult.Failure("Нужна авторизация")

        return try {
            val response = tcpClient.updateProfile(
                token = token,
                displayName = displayName,
                username = username
            )

            when {
                response.profileUpdated != null -> {
                    val profile = response.profileUpdated
                    ProfileResult.Success(
                        ProfileModel(
                            userId = profile.userId,
                            displayName = profile.displayName,
                            username = profile.username,
                            lastSeenAtUnixMs = profile.lastSeenAtUnixMs,
                            isOnline = profile.isOnline
                        )
                    )
                }
                response.error != null -> ProfileResult.Failure(mapServerErrorToRuMessage(response.error.code))
                else -> ProfileResult.Failure("Внутренняя ошибка сервера")
            }
        } catch (ex: Exception) {
            Log.e(tag, "TCP updateProfile failed", ex)
            ProfileResult.Failure("Ошибка сети. Проверьте соединение")
        }
    }

    suspend fun searchUsersByUsername(query: String): UserSearchResult {
        val token = sessionStore.getToken() ?: return UserSearchResult.Failure("Нужна авторизация")
        val normalized = query.trim()
        if (normalized.isEmpty()) {
            return UserSearchResult.Success(emptyList())
        }

        return try {
            val response = tcpClient.searchUsers(token = token, query = normalized)
            when {
                response.userSearchResults != null -> {
                    val items = response.userSearchResults.items.map {
                        UserSearchModel(
                            userId = it.userId,
                            displayName = it.displayName,
                            username = it.username,
                            lastSeenAtUnixMs = it.lastSeenAtUnixMs,
                            isOnline = it.isOnline
                        )
                    }
                    UserSearchResult.Success(items)
                }
                response.error != null -> UserSearchResult.Failure(mapServerErrorToRuMessage(response.error.code))
                else -> UserSearchResult.Failure("Внутренняя ошибка сервера")
            }
        } catch (ex: Exception) {
            Log.e(tag, "TCP user search failed", ex)
            UserSearchResult.Failure("Ошибка сети. Проверьте соединение")
        }
    }

    suspend fun getDialogs(): DialogListResult {
        val token = sessionStore.getToken() ?: return DialogListResult.Failure("Нужна авторизация")

        return try {
            val response = tcpClient.listDialogs(token)
            when {
                response.dialogsData != null -> {
                    val items = response.dialogsData.items.mapNotNull { item ->
                        val peer = item.peer ?: return@mapNotNull null
                        DialogListItemModel(
                            dialogId = item.dialogId,
                            peer = UserSearchModel(
                                userId = peer.userId,
                                displayName = peer.displayName,
                                username = peer.username,
                                lastSeenAtUnixMs = peer.lastSeenAtUnixMs,
                                isOnline = peer.isOnline
                            ),
                            lastMessageText = item.lastMessageText,
                            lastMessageAtUnixMs = item.lastMessageAtUnixMs,
                            unreadCount = item.unreadCount
                        )
                    }
                    DialogListResult.Success(items)
                }
                response.error != null -> DialogListResult.Failure(mapServerErrorToRuMessage(response.error.code))
                else -> DialogListResult.Failure("Внутренняя ошибка сервера")
            }
        } catch (ex: Exception) {
            Log.e(tag, "TCP list dialogs failed", ex)
            DialogListResult.Failure("Ошибка сети. Проверьте соединение")
        }
    }

    suspend fun openDialog(peerUserId: String, beforeUnixMs: Long? = null, limit: Int = 40): DialogThreadResult {
        val token = sessionStore.getToken() ?: return DialogThreadResult.Failure("Нужна авторизация")

        return try {
            val response = tcpClient.openDialog(
                token = token,
                peerUserId = peerUserId,
                beforeUnixMs = beforeUnixMs ?: 0L,
                limit = limit
            )
            when {
                response.dialogData != null -> {
                    val peer = response.dialogData.peer
                        ?: return DialogThreadResult.Failure("Пользователь не найден")
                    DialogThreadResult.Success(
                        DialogThreadModel(
                            dialogId = response.dialogData.dialogId.ifBlank { null },
                            peer = UserSearchModel(
                                userId = peer.userId,
                                displayName = peer.displayName,
                                username = peer.username,
                                lastSeenAtUnixMs = peer.lastSeenAtUnixMs,
                                isOnline = peer.isOnline
                            ),
                            messages = response.dialogData.messages.map {
                                DialogMessageModel(
                                    messageId = it.messageId,
                                    dialogId = it.dialogId,
                                    senderUserId = it.senderUserId,
                                    text = it.text,
                                    createdAtUnixMs = it.createdAtUnixMs,
                                    readByRecipientAtUnixMs = it.readByRecipientAtUnixMs
                                )
                            },
                            hasMoreBefore = response.dialogData.hasMoreBefore,
                            oldestLoadedUnixMs = response.dialogData.oldestLoadedUnixMs
                        )
                    )
                }
                response.error != null -> DialogThreadResult.Failure(mapServerErrorToRuMessage(response.error.code))
                else -> DialogThreadResult.Failure("Внутренняя ошибка сервера")
            }
        } catch (ex: Exception) {
            Log.e(tag, "TCP open dialog failed", ex)
            DialogThreadResult.Failure("Ошибка сети. Проверьте соединение")
        }
    }

    suspend fun sendMessage(peerUserId: String, text: String): SendMessageResult {
        val token = sessionStore.getToken() ?: return SendMessageResult.Failure("Нужна авторизация")

        return try {
            val response = tcpClient.sendMessage(token, peerUserId, text)
            when {
                response.messageSent?.message != null -> {
                    val msg = response.messageSent.message
                    SendMessageResult.Success(
                        DialogMessageModel(
                            messageId = msg.messageId,
                            dialogId = msg.dialogId,
                            senderUserId = msg.senderUserId,
                            text = msg.text,
                            createdAtUnixMs = msg.createdAtUnixMs,
                            readByRecipientAtUnixMs = msg.readByRecipientAtUnixMs
                        )
                    )
                }
                response.error != null -> SendMessageResult.Failure(mapServerErrorToRuMessage(response.error.code))
                else -> SendMessageResult.Failure("Внутренняя ошибка сервера")
            }
        } catch (ex: Exception) {
            Log.e(tag, "TCP send message failed", ex)
            SendMessageResult.Failure("Ошибка сети. Проверьте соединение")
        }
    }

    suspend fun markDialogRead(peerUserId: String): Boolean {
        val token = sessionStore.getToken() ?: return false
        return try {
            val response = tcpClient.openDialog(
                token = token,
                peerUserId = peerUserId,
                limit = 1
            )
            response.dialogData != null
        } catch (ex: Exception) {
            Log.e(tag, "TCP mark dialog read failed", ex)
            false
        }
    }

    private suspend fun authInternal(mnemonicInput: String, isRegister: Boolean): AuthResult {
        val normalized = MnemonicUtils.normalizeMnemonic(mnemonicInput)

        try {
            MnemonicUtils.ensureValid24Words(normalized)
        } catch (_: Exception) {
            return AuthResult.Failure("Неверная секретная фраза")
        }

        return try {
            val response = if (isRegister) {
                tcpClient.register(normalized)
            } else {
                tcpClient.login(normalized)
            }

            when {
                response.authSuccess != null -> {
                    val success = response.authSuccess
                    sessionStore.saveSession(success.accessToken, success.userId)
                    AuthResult.Success(success.userId)
                }
                response.error != null -> {
                    Log.w(tag, "Auth server error: code=${response.error.code}, message=${response.error.message}")
                    AuthResult.Failure(mapServerErrorToRuMessage(response.error.code))
                }
                else -> {
                    Log.w(tag, "Unexpected auth response without payload: $response")
                    AuthResult.Failure("Внутренняя ошибка сервера")
                }
            }
        } catch (ex: Exception) {
            Log.e(tag, "TCP auth request failed", ex)
            AuthResult.Failure("Ошибка сети. Проверьте соединение")
        }
    }
}
