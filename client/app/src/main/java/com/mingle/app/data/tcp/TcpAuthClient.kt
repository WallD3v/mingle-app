@file:OptIn(kotlinx.serialization.ExperimentalSerializationApi::class)

package com.mingle.app.data.tcp

import com.mingle.app.BuildConfig
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.coroutines.withContext
import kotlinx.serialization.protobuf.ProtoBuf
import java.net.Socket

class TcpAuthClient(
    private val host: String = BuildConfig.TCP_HOST,
    private val port: Int = BuildConfig.TCP_PORT
) {
    private val mutex = Mutex()
    private var socket: Socket? = null

    suspend fun register(mnemonic: String): ServerMessage {
        return send(ClientMessage(register = AuthRequest(mnemonic = mnemonic)))
    }

    suspend fun login(mnemonic: String): ServerMessage {
        return send(ClientMessage(login = AuthRequest(mnemonic = mnemonic)))
    }

    suspend fun me(token: String): ServerMessage {
        return send(ClientMessage(me = MeRequest(token = token)))
    }

    suspend fun ping(): ServerMessage {
        return send(ClientMessage(ping = PingRequest(System.currentTimeMillis())))
    }

    suspend fun getProfile(token: String): ServerMessage {
        return send(ClientMessage(profileGet = ProfileGetRequest(token = token)))
    }

    suspend fun updateProfile(token: String, displayName: String, username: String): ServerMessage {
        return send(
            ClientMessage(
                profileUpdate = ProfileUpdateRequest(
                    token = token,
                    displayName = displayName,
                    username = username
                )
            )
        )
    }

    suspend fun searchUsers(token: String, query: String): ServerMessage {
        return send(
            ClientMessage(
                userSearch = UserSearchRequest(
                    token = token,
                    query = query
                )
            )
        )
    }

    suspend fun close() {
        mutex.withLock {
            socket?.close()
            socket = null
        }
    }

    private suspend fun send(message: ClientMessage): ServerMessage {
        return withContext(Dispatchers.IO) {
            mutex.withLock {
                try {
                    val connection = ensureSocket()
                    writeMessage(connection, message)
                    return@withContext readMessage(connection)
                } catch (_: Exception) {
                    reconnectSocket()
                    val connection = ensureSocket()
                    writeMessage(connection, message)
                    return@withContext readMessage(connection)
                }
            }
        }
    }

    private fun ensureSocket(): Socket {
        val existing = socket
        if (existing != null && existing.isConnected && !existing.isClosed) {
            return existing
        }

        val created = Socket(host, port)
        created.soTimeout = 12_000
        socket = created
        return created
    }

    private fun reconnectSocket() {
        socket?.close()
        socket = null
    }

    private fun writeMessage(connection: Socket, message: ClientMessage) {
        val payload = ProtoBuf.encodeToByteArray(ClientMessage.serializer(), message)
        TcpFrameCodec.writeFrame(connection.getOutputStream(), payload)
    }

    private fun readMessage(connection: Socket): ServerMessage {
        val payload = TcpFrameCodec.readFrame(connection.getInputStream())
        return ProtoBuf.decodeFromByteArray(ServerMessage.serializer(), payload)
    }
}
