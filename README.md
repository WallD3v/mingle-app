# Mingle Repo

Базовый инфраструктурный стек для мессенджера:
- PostgreSQL (хранение пользователей, чатов, сообщений, статусов доставки)
- Redis (кэш, presence, очереди, временные данные)
- MinIO/S3 (вложения: фото, видео, документы)
- ASP.NET Core TCP сервер (регистрация/логин по 24 словам + JWT)
- Android клиент (Kotlin + Jetpack Compose)

Порты уже зафиксированы в `.env` (нестандартные, чтобы снизить риск конфликтов):
- PostgreSQL: `55432`
- Redis: `56379`
- MinIO API: `59000`
- MinIO Console: `59001`
- Server TCP: `58081`

## Быстрый старт (Windows 11 / Ubuntu)

1. Установить Docker Desktop (Windows) или Docker Engine + Compose plugin (Ubuntu).
2. В корне проекта запустить:

```bash
docker compose up -d
```

Это поднимет инфраструктуру (`postgres`, `redis`, `minio`, `minio_init`).

Проверка:

```bash
docker compose ps
```

Остановка:

```bash
docker compose down
```

Полная очистка томов:

```bash
docker compose down -v
```

## Запуск server-контейнера

Сервис `server` в `docker-compose.yml` запускается через profile `app`:

```bash
docker compose --profile app up -d --build
```

## TCP протокол

Формат кадра:
- `uint32_be length` + `protobuf payload`

Файл схемы:
- `server/Protocol/mingle.proto`

Сообщения клиента:
- `register { mnemonic }`
- `login { mnemonic }`
- `me { token }`
- `ping`

Сообщения сервера:
- `auth_success { access_token, user_id }`
- `me_success { user_id }`
- `error { code, message }`
- `pong`

Коды ошибок:
- `INVALID_MNEMONIC`
- `UNAUTHORIZED`
- `SERVER_ERROR`

Важно: текущий MVP работает по TCP **без TLS** (только для dev/test в доверенной сети).

## Прод (Ubuntu)

- Используй отдельный `.env` с безопасными паролями.
- Открой только нужные порты в фаерволе.
- Для HTTPS перед API обычно ставят reverse proxy (`nginx`/`traefik`).
- Для бэкапов: регулярный дамп Postgres + snapshot/replication для S3-хранилища.
