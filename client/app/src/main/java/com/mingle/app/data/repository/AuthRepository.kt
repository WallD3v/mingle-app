package com.mingle.app.data.repository

import com.mingle.app.data.mnemonic.MnemonicUtils
import com.mingle.app.data.storage.SecureSessionStore
import com.mingle.app.data.tcp.TcpAuthClient

sealed class AuthResult {
    data class Success(val userId: String) : AuthResult()
    data class Failure(val message: String) : AuthResult()
}

internal fun mapServerErrorToRuMessage(errorCode: String?): String {
    return when (errorCode) {
        "INVALID_MNEMONIC" -> "Неверная секретная фраза"
        "UNAUTHORIZED" -> "Аккаунт не найден"
        else -> "Внутренняя ошибка сервера"
    }
}

class AuthRepository(
    private val tcpClient: TcpAuthClient,
    private val sessionStore: SecureSessionStore
) {
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
                    sessionStore.clear()
                    false
                }
                else -> false
            }
        } catch (_: Exception) {
            false
        }
    }

    fun loadMnemonicWordsFromInput(input: String): List<String> {
        return MnemonicUtils.parseWords(input).take(24)
    }

    fun generateMnemonic(): String = MnemonicUtils.generate24Words()

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
                    AuthResult.Failure(mapServerErrorToRuMessage(response.error.code))
                }
                else -> {
                    AuthResult.Failure("Внутренняя ошибка сервера")
                }
            }
        } catch (_: Exception) {
            AuthResult.Failure("Ошибка сети. Проверьте соединение")
        }
    }
}
