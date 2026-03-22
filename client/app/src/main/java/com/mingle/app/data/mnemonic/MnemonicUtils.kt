package com.mingle.app.data.mnemonic

import org.bitcoinj.crypto.MnemonicCode
import java.security.SecureRandom

object MnemonicUtils {
    fun normalizeMnemonic(value: String): String = value
        .trim()
        .lowercase()
        .split(Regex("\\s+"))
        .filter { it.isNotBlank() }
        .joinToString(" ")

    fun parseWords(value: String): List<String> = normalizeMnemonic(value)
        .split(" ")
        .filter { it.isNotBlank() }

    fun ensureValid24Words(value: String) {
        val words = parseWords(value)
        require(words.size == 24) { "Expected 24 words" }
        MnemonicCode.INSTANCE.check(words)
    }

    fun generate24Words(): String {
        val entropy = ByteArray(32)
        SecureRandom().nextBytes(entropy)
        val words = MnemonicCode.INSTANCE.toMnemonic(entropy)
        return words.joinToString(" ")
    }
}
