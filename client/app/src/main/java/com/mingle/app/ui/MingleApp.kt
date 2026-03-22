package com.mingle.app.ui

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Card
import androidx.compose.material3.Checkbox
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.IconButton
import androidx.compose.material3.TextField
import androidx.compose.material3.TextFieldDefaults
import androidx.compose.material3.Text
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateListOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardCapitalization
import androidx.compose.ui.unit.dp
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.ui.unit.IntOffset
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.ArrowBack
import androidx.compose.material.icons.filled.MoreVert
import androidx.compose.material.icons.filled.Person
import androidx.compose.material.icons.filled.Search
import androidx.compose.material.icons.outlined.ChatBubbleOutline
import androidx.compose.material3.Icon
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.TextButton
import androidx.compose.ui.window.Popup
import com.mingle.app.data.repository.AuthRepository
import com.mingle.app.data.repository.AuthResult
import com.mingle.app.data.repository.UserSearchModel
import com.mingle.app.data.storage.SecureSessionStore
import com.mingle.app.data.tcp.TcpAuthClient
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch

private enum class Screen {
    LOADING,
    WELCOME,
    LOGIN,
    REGISTER,
    HOME
}

private enum class HomeTab {
    CHATS,
    PROFILE
}

private data class HomePalette(
    val screenBackground: Color,
    val panelBackground: Color,
    val primaryText: Color,
    val secondaryText: Color,
    val mutedText: Color,
    val chipBackground: Color,
    val footerBackground: Color,
    val footerBorder: Color,
    val selectedTabBackground: Color
)

@Composable
private fun rememberHomePalette(): HomePalette {
    val dark = isSystemInDarkTheme()
    return if (dark) {
        HomePalette(
            screenBackground = Color(0xFF101010),
            panelBackground = Color(0xFF0E0E0E),
            primaryText = Color(0xFFF1F1F1),
            secondaryText = Color(0xFF7E7E7E),
            mutedText = Color(0xFF5A5A5A),
            chipBackground = Color(0xFF0F0F0F),
            footerBackground = Color(0xFF121212),
            footerBorder = Color(0xFF191919),
            selectedTabBackground = Color(0xFF222222)
        )
    } else {
        HomePalette(
            screenBackground = Color(0xFFF4F4F4),
            panelBackground = Color(0xFFE9E9E9),
            primaryText = Color(0xFF171717),
            secondaryText = Color(0xFF4B4B4B),
            mutedText = Color(0xFF676767),
            chipBackground = Color(0xFFE1E1E1),
            footerBackground = Color(0xFFEEEEEE),
            footerBorder = Color(0xFFD0D0D0),
            selectedTabBackground = Color(0xFFD6D6D6)
        )
    }
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

    val darkMode = isSystemInDarkTheme()
    val appColorScheme = if (darkMode) {
        darkColorScheme(
            primary = Color(0xFF4DA3FF),
            secondary = Color(0xFF7AB9FF),
            background = Color(0xFF101010),
            surface = Color(0xFF171717),
            onPrimary = Color(0xFF041A33),
            onSecondary = Color(0xFF0A1B2E),
            onBackground = Color(0xFFF0F0F0),
            onSurface = Color(0xFFF0F0F0)
        )
    } else {
        lightColorScheme(
            primary = Color(0xFF1F6FD6),
            secondary = Color(0xFF2D83F2),
            background = Color(0xFFF5F5F5),
            surface = Color(0xFFFFFFFF),
            onPrimary = Color(0xFFFFFFFF),
            onSecondary = Color(0xFFFFFFFF),
            onBackground = Color(0xFF171717),
            onSurface = Color(0xFF171717)
        )
    }

    MaterialTheme(colorScheme = appColorScheme) {
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
            Screen.HOME -> HomeScreen(repository)
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
private fun HomeScreen(repository: AuthRepository) {
    var selectedTab by remember { mutableStateOf(HomeTab.CHATS) }
    var openedChatUser by remember { mutableStateOf<UserSearchModel?>(null) }
    var isSearchOpen by rememberSaveable { mutableStateOf(false) }
    var searchQuery by rememberSaveable { mutableStateOf("") }
    var isSearching by rememberSaveable { mutableStateOf(false) }
    var searchError by rememberSaveable { mutableStateOf<String?>(null) }
    var searchResults by remember { mutableStateOf<List<UserSearchModel>>(emptyList()) }
    val palette = rememberHomePalette()

    LaunchedEffect(isSearchOpen, searchQuery) {
        if (!isSearchOpen) {
            isSearching = false
            searchError = null
            searchResults = emptyList()
            return@LaunchedEffect
        }

        val query = searchQuery.trim()
        if (query.isEmpty()) {
            isSearching = false
            searchError = null
            searchResults = emptyList()
            return@LaunchedEffect
        }

        delay(250)
        isSearching = true
        when (val result = repository.searchUsersByUsername(query)) {
            is com.mingle.app.data.repository.UserSearchResult.Success -> {
                searchResults = result.items
                searchError = null
            }
            is com.mingle.app.data.repository.UserSearchResult.Failure -> {
                searchResults = emptyList()
                searchError = result.message
            }
        }
        isSearching = false
    }

    Scaffold { padding ->
        Box(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .background(palette.screenBackground)
                .padding(horizontal = 14.dp, vertical = 10.dp)
        ) {
            Column(modifier = Modifier.fillMaxSize()) {
                when (selectedTab) {
                    HomeTab.CHATS -> {
                        if (openedChatUser != null) {
                            ChatStubScreen(
                                palette = palette,
                                user = openedChatUser!!,
                                onBack = { openedChatUser = null }
                            )
                        } else {
                            TopHeader(
                                palette = palette,
                                isSearchOpen = isSearchOpen,
                                searchQuery = searchQuery,
                                onSearchQueryChange = { searchQuery = it },
                                onSearchClick = { isSearchOpen = true },
                                onSearchClose = {
                                    isSearchOpen = false
                                    searchQuery = ""
                                }
                            )
                            Spacer(modifier = Modifier.height(10.dp))
                            EmptyChats(
                                palette = palette,
                                isSearchOpen = isSearchOpen,
                                query = searchQuery,
                                isSearching = isSearching,
                                errorMessage = searchError,
                                results = searchResults,
                                onUserClick = { user ->
                                    openedChatUser = user
                                    isSearchOpen = false
                                    searchQuery = ""
                                }
                            )
                        }
                    }
                    HomeTab.PROFILE -> {
                        ProfileScreen(palette, repository)
                    }
                }
            }

            if (!isSearchOpen && openedChatUser == null) {
                BottomCapsule(
                    modifier = Modifier
                        .align(Alignment.BottomCenter)
                        .padding(bottom = 16.dp),
                    selectedTab = selectedTab,
                    onSelectTab = {
                        selectedTab = it
                        if (it != HomeTab.CHATS) {
                            openedChatUser = null
                            isSearchOpen = false
                            searchQuery = ""
                        }
                    },
                    palette = palette
                )
            }
        }
    }
}

@Composable
private fun TopHeader(
    palette: HomePalette,
    isSearchOpen: Boolean,
    searchQuery: String,
    onSearchQueryChange: (String) -> Unit,
    onSearchClick: () -> Unit,
    onSearchClose: () -> Unit
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(14.dp))
            .background(palette.panelBackground)
            .padding(horizontal = 12.dp, vertical = 10.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.SpaceBetween
    ) {
        if (!isSearchOpen) {
            Text(
                text = "Mingle",
                color = palette.primaryText,
                style = MaterialTheme.typography.titleMedium,
                fontWeight = FontWeight.Medium
            )
            IconButton(
                onClick = onSearchClick,
                modifier = Modifier
                    .size(30.dp)
                    .clip(RoundedCornerShape(8.dp))
                    .background(palette.chipBackground)
            ) {
                Icon(
                    imageVector = Icons.Filled.Search,
                    contentDescription = "Search",
                    tint = palette.primaryText,
                    modifier = Modifier.size(18.dp)
                )
            }
        } else {
            IconButton(
                onClick = onSearchClose,
                modifier = Modifier
                    .size(width = 44.dp, height = 30.dp)
                    .clip(RoundedCornerShape(8.dp))
                    .background(palette.chipBackground)
            ) {
                Icon(
                    imageVector = Icons.Filled.ArrowBack,
                    contentDescription = "Back",
                    tint = palette.primaryText,
                    modifier = Modifier.size(18.dp)
                )
            }
            TextField(
                value = searchQuery,
                onValueChange = onSearchQueryChange,
                modifier = Modifier.weight(1f),
                label = { Text("Поиск") },
                singleLine = true,
                colors = TextFieldDefaults.colors(
                    focusedContainerColor = palette.panelBackground,
                    unfocusedContainerColor = palette.panelBackground,
                    disabledContainerColor = palette.panelBackground,
                    errorContainerColor = palette.panelBackground,
                    focusedIndicatorColor = Color.Transparent,
                    unfocusedIndicatorColor = Color.Transparent,
                    disabledIndicatorColor = Color.Transparent,
                    errorIndicatorColor = Color.Transparent
                )
            )
        }
    }
}

@Composable
private fun EmptyChats(
    palette: HomePalette,
    isSearchOpen: Boolean,
    query: String,
    isSearching: Boolean,
    errorMessage: String?,
    results: List<UserSearchModel>,
    onUserClick: (UserSearchModel) -> Unit
) {
    if (!isSearchOpen || query.trim().isEmpty()) {
        Box(
            modifier = Modifier
                .fillMaxSize()
                .padding(bottom = 84.dp),
            contentAlignment = Alignment.Center
        ) {
            Text(
                text = "Чатов пока нет",
                color = palette.secondaryText,
                style = MaterialTheme.typography.bodyLarge
            )
        }
        return
    }

    if (isSearching) {
        Box(
            modifier = Modifier
                .fillMaxSize()
                .padding(bottom = 84.dp),
            contentAlignment = Alignment.Center
        ) {
            CircularProgressIndicator()
        }
        return
    }

    errorMessage?.let {
        Box(
            modifier = Modifier
                .fillMaxSize()
                .padding(bottom = 84.dp),
            contentAlignment = Alignment.Center
        ) {
            Text(
                text = it,
                color = MaterialTheme.colorScheme.error,
                style = MaterialTheme.typography.bodyMedium
            )
        }
        return
    }

    if (results.isEmpty()) {
        Box(
            modifier = Modifier
                .fillMaxSize()
                .padding(bottom = 84.dp),
            contentAlignment = Alignment.Center
        ) {
            Text(
                text = "Ничего не найдено",
                color = palette.secondaryText,
                style = MaterialTheme.typography.bodyLarge
            )
        }
        return
    }

    LazyColumn(
        modifier = Modifier
            .fillMaxSize()
            .padding(bottom = 84.dp),
        verticalArrangement = Arrangement.spacedBy(8.dp)
    ) {
        items(results) { user ->
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .clip(RoundedCornerShape(12.dp))
                    .background(palette.panelBackground)
                    .clickable { onUserClick(user) }
                    .padding(horizontal = 12.dp, vertical = 10.dp),
                verticalAlignment = Alignment.CenterVertically
            ) {
                Box(
                    modifier = Modifier
                        .size(38.dp)
                        .clip(CircleShape)
                        .background(palette.chipBackground)
                )
                Spacer(modifier = Modifier.size(10.dp))
                Column {
                    Text(
                        text = user.displayName,
                        color = palette.primaryText,
                        style = MaterialTheme.typography.bodyLarge,
                        fontWeight = FontWeight.Medium
                    )
                    Text(
                        text = "@${user.username}",
                        color = palette.secondaryText,
                        style = MaterialTheme.typography.bodySmall
                    )
                }
            }
        }
    }
}

@Composable
private fun ChatStubScreen(
    palette: HomePalette,
    user: UserSearchModel,
    onBack: () -> Unit
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(14.dp))
            .background(palette.panelBackground)
            .padding(horizontal = 12.dp, vertical = 10.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        IconButton(
            onClick = onBack,
            modifier = Modifier
                .size(width = 44.dp, height = 30.dp)
                .clip(RoundedCornerShape(8.dp))
                .background(palette.chipBackground)
        ) {
            Icon(
                imageVector = Icons.Filled.ArrowBack,
                contentDescription = "Back to chats",
                tint = palette.primaryText,
                modifier = Modifier.size(18.dp)
            )
        }
        Spacer(modifier = Modifier.width(10.dp))
        Column {
            Text(
                text = user.displayName,
                color = palette.primaryText,
                style = MaterialTheme.typography.titleMedium,
                fontWeight = FontWeight.Medium
            )
            Text(
                text = "@${user.username}",
                color = palette.secondaryText,
                style = MaterialTheme.typography.bodySmall
            )
        }
    }

    Spacer(modifier = Modifier.height(10.dp))

    Box(
        modifier = Modifier
            .fillMaxSize()
            .padding(bottom = 12.dp),
        contentAlignment = Alignment.Center
    ) {
        Text(
            text = "Диалог с @${user.username} в разработке",
            color = palette.secondaryText,
            style = MaterialTheme.typography.bodyLarge
        )
    }
}

@Composable
private fun ProfileScreen(palette: HomePalette, repository: AuthRepository) {
    val scope = rememberCoroutineScope()

    var displayName by rememberSaveable { mutableStateOf("") }
    var username by rememberSaveable { mutableStateOf("") }
    var lastSeenAtUnixMs by rememberSaveable { mutableStateOf(0L) }

    var isLoading by rememberSaveable { mutableStateOf(true) }
    var errorMessage by rememberSaveable { mutableStateOf<String?>(null) }

    var isMenuOpen by rememberSaveable { mutableStateOf(false) }
    var isNameDialogOpen by rememberSaveable { mutableStateOf(false) }

    var isEditing by rememberSaveable { mutableStateOf(false) }
    var draftUsername by rememberSaveable { mutableStateOf("") }
    var draftDisplayName by rememberSaveable { mutableStateOf("") }
    val isDraftUsernameValid = isValidUsername(draftUsername)

    LaunchedEffect(Unit) {
        isLoading = true
        when (val result = repository.getProfile()) {
            is com.mingle.app.data.repository.ProfileResult.Success -> {
                displayName = result.profile.displayName
                username = result.profile.username
                lastSeenAtUnixMs = result.profile.lastSeenAtUnixMs
                draftUsername = username
                draftDisplayName = displayName
                errorMessage = null
            }
            is com.mingle.app.data.repository.ProfileResult.Failure -> {
                errorMessage = result.message
            }
        }
        isLoading = false
    }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(bottom = 84.dp, top = 28.dp, start = 10.dp, end = 10.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Top
    ) {
        if (isLoading) {
            CircularProgressIndicator()
            return@Column
        }

        Box(modifier = Modifier.fillMaxWidth(), contentAlignment = Alignment.TopEnd) {
            IconButton(
                onClick = { isMenuOpen = !isMenuOpen },
                modifier = Modifier
                    .size(32.dp)
                    .clip(RoundedCornerShape(8.dp))
                    .background(palette.chipBackground)
            ) {
                Icon(
                    imageVector = Icons.Filled.MoreVert,
                    contentDescription = "Profile actions",
                    tint = palette.primaryText
                )
            }

            if (isMenuOpen) {
                Popup(
                    alignment = Alignment.TopEnd,
                    offset = IntOffset(x = -8, y = 44),
                    onDismissRequest = { isMenuOpen = false }
                ) {
                    Box(
                        modifier = Modifier
                            .clip(RoundedCornerShape(10.dp))
                            .background(palette.panelBackground)
                            .border(1.dp, palette.footerBorder, RoundedCornerShape(10.dp))
                    ) {
                        TextButton(
                            onClick = {
                                isMenuOpen = false
                                draftDisplayName = displayName
                                isNameDialogOpen = true
                            }
                        ) {
                            Text("Изменить имя")
                        }
                    }
                }
            }
        }

        Spacer(modifier = Modifier.height(4.dp))

        Box(
            modifier = Modifier
                .size(104.dp)
                .clip(CircleShape)
                .background(Color.Black)
        )
        Spacer(modifier = Modifier.height(16.dp))
        Text(
            text = displayName,
            color = palette.primaryText,
            style = MaterialTheme.typography.headlineSmall,
            fontWeight = FontWeight.SemiBold
        )
        Spacer(modifier = Modifier.height(2.dp))
        Text(
            text = formatLastSeen(lastSeenAtUnixMs),
            color = palette.secondaryText,
            style = MaterialTheme.typography.bodySmall
        )
        Spacer(modifier = Modifier.height(16.dp))

        Button(
            onClick = {
                if (!isEditing) {
                    draftUsername = username
                    isEditing = true
                } else {
                    scope.launch {
                        when (val result = repository.updateProfile(displayName, draftUsername)) {
                            is com.mingle.app.data.repository.ProfileResult.Success -> {
                                val profile = result.profile
                                displayName = profile.displayName
                                username = profile.username
                                lastSeenAtUnixMs = profile.lastSeenAtUnixMs
                                draftUsername = profile.username
                                isEditing = false
                                errorMessage = null
                            }
                            is com.mingle.app.data.repository.ProfileResult.Failure -> {
                                errorMessage = result.message
                            }
                        }
                    }
                }
            },
            modifier = Modifier
                .fillMaxWidth()
                .height(40.dp),
            shape = RoundedCornerShape(8.dp),
            colors = ButtonDefaults.buttonColors(containerColor = palette.chipBackground),
            enabled = !isEditing || isDraftUsernameValid
        ) {
            Text(
                text = if (isEditing) "Save profile" else "Edit profile",
                color = palette.primaryText,
                style = MaterialTheme.typography.bodyMedium,
                fontWeight = FontWeight.Medium
            )
        }

        Spacer(modifier = Modifier.height(8.dp))

        Box(
            modifier = Modifier
                .fillMaxWidth()
                .clip(RoundedCornerShape(8.dp))
                .background(palette.panelBackground)
                .padding(horizontal = 10.dp, vertical = 8.dp)
        ) {
            Column {
                if (isEditing) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Text(
                            text = "@",
                            color = palette.secondaryText,
                            style = MaterialTheme.typography.bodySmall
                        )
                        TextField(
                            value = draftUsername,
                            onValueChange = { draftUsername = sanitizeUsername(it) },
                            modifier = Modifier.weight(1f),
                            singleLine = true,
                            colors = TextFieldDefaults.colors(
                                focusedContainerColor = palette.panelBackground,
                                unfocusedContainerColor = palette.panelBackground,
                                disabledContainerColor = palette.panelBackground,
                                errorContainerColor = palette.panelBackground,
                                focusedIndicatorColor = Color.Transparent,
                                unfocusedIndicatorColor = Color.Transparent,
                                disabledIndicatorColor = Color.Transparent,
                                errorIndicatorColor = Color.Transparent
                            )
                        )
                    }
                    if (!isDraftUsernameValid) {
                        Text(
                            text = "Минимум 5 символов, только A-Z, a-z и 0-9",
                            color = MaterialTheme.colorScheme.error,
                            style = MaterialTheme.typography.labelSmall
                        )
                    }
                } else {
                    Text(
                        text = "@$username",
                        color = palette.secondaryText,
                        style = MaterialTheme.typography.bodySmall
                    )
                }
                Text(
                    text = "username",
                    color = palette.mutedText,
                    style = MaterialTheme.typography.labelSmall
                )
            }
        }

        errorMessage?.let {
            Spacer(modifier = Modifier.height(8.dp))
            Text(
                text = it,
                color = MaterialTheme.colorScheme.error,
                style = MaterialTheme.typography.bodySmall
            )
        }
    }

    if (isNameDialogOpen) {
        AlertDialog(
            onDismissRequest = { isNameDialogOpen = false },
            title = { Text("Изменить имя") },
            text = {
                OutlinedTextField(
                    value = draftDisplayName,
                    onValueChange = { draftDisplayName = it },
                    label = { Text("Имя") },
                    singleLine = true
                )
            },
            confirmButton = {
                TextButton(
                    onClick = {
                        scope.launch {
                            when (val result = repository.updateProfile(draftDisplayName, username)) {
                                is com.mingle.app.data.repository.ProfileResult.Success -> {
                                    val profile = result.profile
                                    displayName = profile.displayName
                                    username = profile.username
                                    lastSeenAtUnixMs = profile.lastSeenAtUnixMs
                                    draftDisplayName = profile.displayName
                                    isNameDialogOpen = false
                                    errorMessage = null
                                }
                                is com.mingle.app.data.repository.ProfileResult.Failure -> {
                                    errorMessage = result.message
                                }
                            }
                        }
                    }
                ) {
                    Text("Сохранить")
                }
            },
            dismissButton = {
                TextButton(onClick = { isNameDialogOpen = false }) {
                    Text("Отмена")
                }
            }
        )
    }
}

private fun sanitizeUsername(input: String): String {
    return input.filter { it.isLetterOrDigit() && it.code < 128 }
}

private fun isValidUsername(username: String): Boolean {
    return username.length >= 5 && username.all { it.isLetterOrDigit() && it.code < 128 }
}

private fun formatLastSeen(lastSeenAtUnixMs: Long): String {
    if (lastSeenAtUnixMs <= 0L) return "last seen recently"

    val diffMinutes = ((System.currentTimeMillis() - lastSeenAtUnixMs) / 60000).coerceAtLeast(0)
    return when {
        diffMinutes < 1 -> "online now"
        diffMinutes < 60 -> "last seen ${diffMinutes}m ago"
        diffMinutes < 1440 -> "last seen ${diffMinutes / 60}h ago"
        else -> "last seen ${diffMinutes / 1440}d ago"
    }
}

@Composable
private fun FooterTab(
    selected: Boolean,
    onClick: () -> Unit,
    icon: androidx.compose.ui.graphics.vector.ImageVector,
    contentDescription: String,
    palette: HomePalette
) {
    Button(
        onClick = onClick,
        shape = RoundedCornerShape(12.dp),
        colors = ButtonDefaults.buttonColors(containerColor = palette.chipBackground)
    ) {
        Box(
            modifier = Modifier
                .clip(RoundedCornerShape(8.dp))
                .background(if (selected) palette.selectedTabBackground else Color.Transparent)
                .padding(horizontal = 10.dp, vertical = 6.dp)
        ) {
            Icon(
                imageVector = icon,
                contentDescription = contentDescription,
                tint = if (selected) palette.primaryText else palette.secondaryText,
                modifier = Modifier.size(18.dp)
            )
        }
    }
}

@Composable
private fun BottomCapsule(
    modifier: Modifier = Modifier,
    selectedTab: HomeTab,
    onSelectTab: (HomeTab) -> Unit,
    palette: HomePalette
) {
    Row(
        modifier = modifier
            .clip(RoundedCornerShape(18.dp))
            .background(palette.footerBackground)
            .border(1.dp, palette.footerBorder, RoundedCornerShape(18.dp))
            .padding(horizontal = 8.dp, vertical = 8.dp),
        horizontalArrangement = Arrangement.spacedBy(8.dp)
    ) {
        FooterTab(
            selected = selectedTab == HomeTab.CHATS,
            onClick = { onSelectTab(HomeTab.CHATS) },
            icon = Icons.Outlined.ChatBubbleOutline,
            contentDescription = "Chats",
            palette = palette
        )
        FooterTab(
            selected = selectedTab == HomeTab.PROFILE,
            onClick = { onSelectTab(HomeTab.PROFILE) },
            icon = Icons.Filled.Person,
            contentDescription = "Profile",
            palette = palette
        )
    }
}
