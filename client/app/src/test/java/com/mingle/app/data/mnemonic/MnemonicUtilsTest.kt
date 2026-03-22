package com.mingle.app.data.mnemonic

import org.junit.Assert.assertEquals
import org.junit.Assert.assertThrows
import org.junit.Test

class MnemonicUtilsTest {
    @Test
    fun normalize_RemovesExtraSpacesAndLowercase() {
        val normalized = MnemonicUtils.normalizeMnemonic("  ABANDON   abandon   Abandon  ")
        assertEquals("abandon abandon abandon", normalized)
    }

    @Test
    fun ensureValid24Words_ThrowsOnInvalidWordCount() {
        assertThrows(IllegalArgumentException::class.java) {
            MnemonicUtils.ensureValid24Words("abandon abandon")
        }
    }
}
