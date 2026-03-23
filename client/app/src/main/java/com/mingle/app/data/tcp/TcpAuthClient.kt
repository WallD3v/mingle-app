@file:OptIn(kotlinx.serialization.ExperimentalSerializationApi::class)

package com.mingle.app.data.tcp

import com.mingle.app.BuildConfig
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.coroutines.withContext
import kotlinx.coroutines.withTimeout
import kotlinx.serialization.protobuf.ProtoBuf
import java.net.Socket

class TcpAuthClient(
    private val host: String = BuildConfig.TCP_HOST,
    private val port: Int = BuildConfig.TCP_PORT
) {
    private val mutex = Mutex()
    private var socket: Socket? = null
    private val ioScope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
    private var readJob: kotlinx.coroutines.Job? = null
    private val responseChannel = Channel<ServerMessage>(Channel.UNLIMITED)

    @Volatile
    private var messageListener: ((MessageReceived) -> Unit)? = null

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

    suspend fun listDialogs(token: String): ServerMessage {
        return send(ClientMessage(dialogsList = DialogsListRequest(token = token)))
    }

    suspend fun openDialog(token: String, peerUserId: String, beforeUnixMs: Long = 0L, limit: Int = 0): ServerMessage {
        return send(
            ClientMessage(
                dialogOpen = DialogOpenRequest(
                    token = token,
                    peerUserId = peerUserId,
                    beforeUnixMs = beforeUnixMs,
                    limit = limit
                )
            )
        )
    }

    suspend fun sendMessage(token: String, peerUserId: String, text: String): ServerMessage {
        return send(
            ClientMessage(
                messageSend = MessageSendRequest(
                    token = token,
                    peerUserId = peerUserId,
                    text = text
                )
            )
        )
    }

    suspend fun subscribeUpdates(token: String): ServerMessage {
        return send(
            ClientMessage(
                subscribeUpdates = SubscribeUpdatesRequest(token = token)
            )
        )
    }

    fun setMessageListener(listener: ((MessageReceived) -> Unit)?) {
        messageListener = listener
    }

    suspend fun close() {
        mutex.withLock {
            readJob?.cancel()
            readJob = null
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
                    return@withContext withTimeout(15_000) { responseChannel.receive() }
                } catch (_: Exception) {
                    reconnectSocket()
                    val connection = ensureSocket()
                    writeMessage(connection, message)
                    return@withContext withTimeout(15_000) { responseChannel.receive() }
                }
            }
        }
    }

    private fun ensureSocket(): Socket {
        val existing = socket
        if (existing != null && existing.isConnected && !existing.isClosed) {
            ensureReaderLoop(existing)
            return existing
        }

        val created = Socket(host, port)
        created.soTimeout = 0
        socket = created
        ensureReaderLoop(created)
        return created
    }

    private fun reconnectSocket() {
        readJob?.cancel()
        readJob = null
        socket?.close()
        socket = null
    }

    private fun writeMessage(connection: Socket, message: ClientMessage) {
        val payload = ProtoBuf.encodeToByteArray(ClientMessage.serializer(), message)
        TcpFrameCodec.writeFrame(connection.getOutputStream(), payload)
    }

    private fun ensureReaderLoop(connection: Socket) {
        if (readJob?.isActive == true) {
            return
        }

        readJob = ioScope.launch {
            try {
                while (isActive && !connection.isClosed) {
                    val payload = TcpFrameCodec.readFrame(connection.getInputStream())
                    val message = ProtoBuf.decodeFromByteArray(ServerMessage.serializer(), payload)
                    val incoming = message.messageReceived
                    if (incoming != null) {
                        messageListener?.invoke(incoming)
                    } else {
                        responseChannel.send(message)
                    }
                }
            } catch (_: Exception) {
                reconnectSocket()
            }
        }
    }
}
