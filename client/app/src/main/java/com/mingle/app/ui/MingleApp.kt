package com.mingle.app.ui

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.Checkbox
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateListOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.input.KeyboardCapitalization
import androidx.compose.ui.unit.dp
import com.mingle.app.data.repository.AuthRepository
import com.mingle.app.data.repository.AuthResult
import com.mingle.app.data.storage.SecureSessionStore
import com.mingle.app.data.tcp.TcpAuthClient
import kotlinx.coroutines.launch

private enum class Screen {
    LOADING,
    WELCOME,
    LOGIN,
    REGISTER,
    HOME
}

@Composable
fun MingleApp() {
    val context = LocalContext.current
    val sessionStore = remember { SecureSessionStore(context) }
    val tcpClient = remember { TcpAuthClient() }
    val repository = remember { AuthRepository(tcpClient, sessionStore) }

    var screen by remember { mutableStateOf(Screen.LOADING) }

    LaunchedEffect(Unit) {
        screen = if (repository.hasValidSession()) Screen.HOME else Screen.WELCOME
    }

    MaterialTheme {
        when (screen) {
            Screen.LOADING -> LoadingScreen()
            Screen.WELCOME -> WelcomeScreen(
                onLoginClick = { screen = Screen.LOGIN },
                onRegisterClick = { screen = Screen.REGISTER }
            )
            Screen.LOGIN -> MnemonicAuthScreen(
                isRegisterMode = false,
                repository = repository,
                onBack = { screen = Screen.WELCOME },
                onSuccess = { screen = Screen.HOME }
            )
            Screen.REGISTER -> MnemonicAuthScreen(
                isRegisterMode = true,
                repository = repository,
                onBack = { screen = Screen.WELCOME },
                onSuccess = { screen = Screen.HOME }
            )
            Screen.HOME -> HomeScreen()
        }
    }
}

@Composable
private fun LoadingScreen() {
    Scaffold { padding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding),
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.Center
        ) {
            CircularProgressIndicator()
            Spacer(modifier = Modifier.height(12.dp))
            Text("Проверяем сессию...")
        }
    }
}

@Composable
private fun WelcomeScreen(
    onLoginClick: () -> Unit,
    onRegisterClick: () -> Unit
) {
    Scaffold { padding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .padding(20.dp),
            verticalArrangement = Arrangement.Center,
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            Text("Mingle", style = MaterialTheme.typography.headlineMedium)
            Spacer(modifier = Modifier.height(12.dp))
            Text("Вход по секретной фразе из 24 слов")
            Spacer(modifier = Modifier.height(24.dp))
            Button(onClick = onLoginClick, modifier = Modifier.fillMaxWidth()) {
                Text("Войти по 24 словам")
            }
            Spacer(modifier = Modifier.height(12.dp))
            Button(onClick = onRegisterClick, modifier = Modifier.fillMaxWidth()) {
                Text("Создать новый аккаунт")
            }
        }
    }
}

@Composable
private fun MnemonicAuthScreen(
    isRegisterMode: Boolean,
    repository: AuthRepository,
    onBack: () -> Unit,
    onSuccess: () -> Unit
) {
    val scope = rememberCoroutineScope()
    val words = remember { mutableStateListOf(*Array(24) { "" }) }

    var pasteInput by remember { mutableStateOf("") }
    var savedPhraseConfirmed by remember { mutableStateOf(false) }
    var isLoading by remember { mutableStateOf(false) }
    var errorMessage by remember { mutableStateOf<String?>(null) }
    var generatedMnemonic by remember { mutableStateOf<String?>(null) }

    LaunchedEffect(isRegisterMode) {
        if (isRegisterMode) {
            generatedMnemonic = repository.generateMnemonic()
            val generatedWords = repository.loadMnemonicWordsFromInput(generatedMnemonic.orEmpty())
            for (i in generatedWords.indices) {
                words[i] = generatedWords[i]
            }
        }
    }

    Scaffold { padding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .padding(16.dp)
                .verticalScroll(rememberScrollState())
        ) {
            Text(
                if (isRegisterMode) "Создание аккаунта" else "Вход в аккаунт",
                style = MaterialTheme.typography.headlineSmall
            )
            Spacer(modifier = Modifier.height(12.dp))

            if (isRegisterMode) {
                Card(modifier = Modifier.fillMaxWidth()) {
                    Column(modifier = Modifier.padding(12.dp)) {
                        Text("Секретная фраза (сохраните обязательно):")
                        Spacer(modifier = Modifier.height(8.dp))
                        Text(generatedMnemonic.orEmpty())
                        Spacer(modifier = Modifier.height(8.dp))
                        Button(onClick = {
                            generatedMnemonic = repository.generateMnemonic()
                            val generatedWords = repository.loadMnemonicWordsFromInput(generatedMnemonic.orEmpty())
                            for (i in 0 until 24) {
                                words[i] = generatedWords.getOrElse(i) { "" }
                            }
                            savedPhraseConfirmed = false
                        }) {
                            Text("Сгенерировать заново")
                        }
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Checkbox(
                                checked = savedPhraseConfirmed,
                                onCheckedChange = { savedPhraseConfirmed = it }
                            )
                            Text("Я сохранил фразу")
                        }
                    }
                }
                Spacer(modifier = Modifier.height(12.dp))
            }

            OutlinedTextField(
                value = pasteInput,
                onValueChange = { pasteInput = it },
                modifier = Modifier.fillMaxWidth(),
                label = { Text("Вставить всю фразу") }
            )
            Spacer(modifier = Modifier.height(8.dp))
            Button(onClick = {
                val parsed = repository.loadMnemonicWordsFromInput(pasteInput)
                for (i in 0 until 24) {
                    words[i] = parsed.getOrElse(i) { "" }
                }
            }) {
                Text("Разложить по полям")
            }

            Spacer(modifier = Modifier.height(16.dp))
            Text("24 слова")
            Spacer(modifier = Modifier.height(8.dp))

            for (row in 0 until 12) {
                Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    val firstIndex = row * 2
                    val secondIndex = row * 2 + 1

                    OutlinedTextField(
                        value = words[firstIndex],
                        onValueChange = { words[firstIndex] = it.trim() },
                        modifier = Modifier.weight(1f),
                        label = { Text("${firstIndex + 1}") },
                        singleLine = true,
                        keyboardOptions = KeyboardOptions(capitalization = KeyboardCapitalization.None)
                    )
                    OutlinedTextField(
                        value = words[secondIndex],
                        onValueChange = { words[secondIndex] = it.trim() },
                        modifier = Modifier.weight(1f),
                        label = { Text("${secondIndex + 1}") },
                        singleLine = true,
                        keyboardOptions = KeyboardOptions(capitalization = KeyboardCapitalization.None)
                    )
                }
                Spacer(modifier = Modifier.height(6.dp))
            }

            errorMessage?.let {
                Spacer(modifier = Modifier.height(8.dp))
                Text(it, color = MaterialTheme.colorScheme.error)
            }

            Spacer(modifier = Modifier.height(12.dp))
            Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                Button(onClick = onBack, enabled = !isLoading) {
                    Text("Назад")
                }

                val canSubmit = if (isRegisterMode) savedPhraseConfirmed else true
                Button(
                    onClick = {
                        val phrase = words.joinToString(" ")
                        isLoading = true
                        errorMessage = null

                        scope.launch {
                            val result = if (isRegisterMode) {
                                repository.register(phrase)
                            } else {
                                repository.login(phrase)
                            }
                            isLoading = false
                            when (result) {
                                is AuthResult.Success -> onSuccess()
                                is AuthResult.Failure -> errorMessage = result.message
                            }
                        }
                    },
                    enabled = !isLoading && canSubmit
                ) {
                    if (isLoading) {
                        CircularProgressIndicator(modifier = Modifier.height(18.dp), strokeWidth = 2.dp)
                    } else {
                        Text(if (isRegisterMode) "Создать" else "Войти")
                    }
                }
            }
        }
    }
}

@Composable
private fun HomeScreen() {
    Scaffold { padding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .padding(20.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.Center
        ) {
            Text(
                text = "Мессенджер в разработке",
                style = MaterialTheme.typography.headlineSmall
            )
        }
    }
}
