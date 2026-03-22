package com.mingle.app.data.tcp

import java.io.EOFException
import java.io.InputStream
import java.io.OutputStream
import java.nio.ByteBuffer
import java.nio.ByteOrder

object TcpFrameCodec {
    private const val MAX_FRAME_SIZE = 1024 * 1024

    fun writeFrame(output: OutputStream, payload: ByteArray) {
        require(payload.size in 1..MAX_FRAME_SIZE) { "Invalid frame size" }

        val prefix = ByteBuffer.allocate(4)
            .order(ByteOrder.BIG_ENDIAN)
            .putInt(payload.size)
            .array()

        output.write(prefix)
        output.write(payload)
        output.flush()
    }

    fun readFrame(input: InputStream): ByteArray {
        val prefix = readExactly(input, 4)
        val length = ByteBuffer.wrap(prefix).order(ByteOrder.BIG_ENDIAN).int
        require(length in 1..MAX_FRAME_SIZE) { "Invalid frame size" }
        return readExactly(input, length)
    }

    private fun readExactly(input: InputStream, size: Int): ByteArray {
        val buffer = ByteArray(size)
        var offset = 0

        while (offset < size) {
            val read = input.read(buffer, offset, size - offset)
            if (read == -1) throw EOFException("Unexpected EOF")
            offset += read
        }

        return buffer
    }
}
