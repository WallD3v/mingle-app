package com.mingle.app.data.repository

import com.mingle.app.data.api.AuthApi
import com.mingle.app.data.api.AuthRequest
import com.mingle.app.data.api.AuthResponse
import com.mingle.app.data.api.ErrorResponse
import com.mingle.app.data.mnemonic.MnemonicUtils
import com.mingle.app.data.storage.SecureSessionStore
import com.squareup.moshi.Moshi

sealed class AuthResult {
    data class Success(val userId: String) : AuthResult()
    data class Failure(val message: String) : AuthResult()
}

internal fun mapServerErrorToRuMessage(errorCode: String?, httpCode: Int): String {
    return when {
        errorCode == "INVALID_MNEMONIC" -> "Неверная секретная фраза"
        errorCode == "UNAUTHORIZED" || httpCode == 401 -> "Аккаунт не найден"
        else -> "Внутренняя ошибка сервера"
    }
}

class AuthRepository(
    private val api: AuthApi,
    private val sessionStore: SecureSessionStore
) {
    private val errorAdapter = Moshi.Builder().build().adapter(ErrorResponse::class.java)

    suspend fun register(mnemonicInput: String): AuthResult {
        return authInternal(mnemonicInput, isRegister = true)
    }

    suspend fun login(mnemonicInput: String): AuthResult {
        return authInternal(mnemonicInput, isRegister = false)
    }

    suspend fun hasValidSession(): Boolean {
        val token = sessionStore.getToken() ?: return false
        return try {
            val response = api.me()
            if (response.isSuccessful) {
                true
            } else {
                sessionStore.clear()
                false
            }
        } catch (_: Exception) {
            token.isNotBlank()
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
            val request = AuthRequest(normalized)
            val response = if (isRegister) api.register(request) else api.login(request)
            if (response.isSuccessful) {
                val body = response.body() ?: return AuthResult.Failure("Пустой ответ сервера")
                persistSession(body)
                AuthResult.Success(body.userId)
            } else {
                AuthResult.Failure(mapErrorMessage(response.code(), response.errorBody()?.string()))
            }
        } catch (_: Exception) {
            AuthResult.Failure("Ошибка сети. Проверьте соединение")
        }
    }

    private fun persistSession(body: AuthResponse) {
        sessionStore.saveSession(body.accessToken, body.userId)
    }

    private fun mapErrorMessage(httpCode: Int, body: String?): String {
        val parsed = body?.let { errorAdapter.fromJson(it) }
        return mapServerErrorToRuMessage(parsed?.code, httpCode)
    }
}
