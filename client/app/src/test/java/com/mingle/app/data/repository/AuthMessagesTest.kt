package com.mingle.app.data.repository

import org.junit.Assert.assertEquals
import org.junit.Test

class AuthMessagesTest {
    @Test
    fun mapsInvalidMnemonic() {
        assertEquals("Неверная секретная фраза", mapServerErrorToRuMessage("INVALID_MNEMONIC", 400))
    }

    @Test
    fun mapsUnauthorized() {
        assertEquals("Аккаунт не найден", mapServerErrorToRuMessage("UNAUTHORIZED", 401))
    }

    @Test
    fun mapsServerErrorFallback() {
        assertEquals("Внутренняя ошибка сервера", mapServerErrorToRuMessage(null, 500))
    }
}
