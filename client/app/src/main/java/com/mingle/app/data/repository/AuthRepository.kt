package com.mingle.app.data.repository

import android.util.Log
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
    private val tag = "AuthRepository"
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
