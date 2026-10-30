# 3x-ui Telegram Bot

Закрытый Telegram-бот для раздачи и обслуживания VPN-подключений. Пользователь сам создаёт себе соединение, получает QR-код, ссылку `vless://` или конфиг AmneziaWG и видит свой трафик. Администратор управляет пользователями, лимитами и соединениями прямо из Telegram.

- **VLESS + Reality** через панель [3x-ui](https://github.com/MHSanaei/3x-ui) v3: профили Raw, XHTTP и XHTTP за CDN, каждый — отдельный inbound.
- **AmneziaWG** через [amnezia-wg-easy](https://github.com/w0rng/amnezia-wg-easy) (необязательно) — рекомендуется для мобильного интернета, подключение через приложение AmneziaWG.
- **.NET 10**, SQLite (EF Core), Telegram long polling, разворачивается в Docker рядом с панелью.

Подробное описание всех экранов, ролей и фоновых процессов — в [FEATURES.md](FEATURES.md).

## Возможности

**Пользователь**
- Свои соединения: QR-код, ссылка или конфиг, инструкция по подключению, трафик за сутки / 7 / 30 дней / всё время.
- Ссылка-подписка: одна ссылка на все свои VLESS-соединения, приложение само подтягивает новые и изменённые (через сервер подписок 3x-ui).
- Создать соединение в пределах лимита, переименовать, включить/отключить, удалить.

**Администратор**
- Все клиенты серверов, пользователи бота, роли и лимиты соединений, привязка соединений к пользователям.
- Приглашение по одноразовой ссылке или добавление по контакту.
- Статистика: кто онлайн и топ по трафику.
- Рассылка сообщения всем пользователям.
- Журнал действий: кто, что и когда менял.
- `/status` — состояние серверов и сводка.

**Фоном**
- Снимки трафика и синхронизация соединений с панелями раз в час.
- Мониторинг серверов: уведомление админам, когда сервер перестал отвечать и когда восстановился.
- Напоминания об оплате хостинга (по желанию).

Доступ — только по приглашению: незнакомым бот отвечает «Доступ запрещён».

## Быстрый старт (Docker / Portainer)

### 1. Подготовьте панели

- **Бот в Telegram:** создайте его у [@BotFather](https://t.me/BotFather) и сохраните токен. Свой Telegram ID можно узнать, например, у [@userinfobot](https://t.me/userinfobot).
- **3x-ui:** создайте inbound'ы VLESS + Reality (Raw, XHTTP, XHTTP CDN — нужные из них) и запишите их ID из колонки ID в панели. API-токен: Settings → Security → API Token.
- **amnezia-wg-easy** (если нужен): адрес веб-интерфейса и его пароль (`PASSWORD` контейнера).

### 2. Задайте переменные

Секреты в `docker-compose.yml` не хранятся — там только ссылки `${...}`. Образец со всеми переменными — [.env.example](.env.example).

- **Portainer:** Stacks → стек → **Environment variables** → **Advanced mode**, вставьте строки из `.env.example` со своими значениями. Portainer сохраняет их между redeploy.
- **Локально:** скопируйте `.env.example` в `.env` рядом с `docker-compose.yml` (в git он не попадает).

| Переменная | Обязательна | Назначение |
| --- | --- | --- |
| `TELEGRAM_TOKEN` | да | Токен бота от @BotFather |
| `TELEGRAM_ADMIN_ID` | да | Telegram ID суперадмина |
| `XUI_API_URL` | да | Адрес панели 3x-ui вместе с webBasePath, со слэшем в конце |
| `XUI_API_TOKEN` | да | API-токен панели |
| `XUI_INBOUND_RAW_ID` | нет* | ID inbound'а VLESS Raw |
| `XUI_INBOUND_XHTTP_ID` | нет* | ID inbound'а VLESS XHTTP |
| `XUI_INBOUND_XHTTP_CDN_ID` | нет* | ID inbound'а VLESS XHTTP за CDN |
| `XUI_INBOUND_XHTTP_CDN_PORT` | нет | Порт в ссылке для XHTTP CDN (по умолчанию 443) |
| `XUI_SUBSCRIPTION_URL` | нет | Адрес сервера подписок 3x-ui с путём, без subId (например `https://vpn.example.com:2096/sub/`). Не задан — кнопки «Подписка» нет |
| `AMNEZIA_ENABLED` | нет | `true` — включить AmneziaWG (по умолчанию `true`) |
| `AMNEZIA_API_URL` | нет | Адрес amnezia-wg-easy |
| `AMNEZIA_PASSWORD` | нет | Пароль amnezia-wg-easy |

\* Нужен хотя бы один ID inbound'а. Не заданный inbound просто не показывается в боте.

Без обязательной переменной стек не развернётся и покажет, какой не хватает.

### 3. Запустите

```bash
docker compose up -d --build
```

В Portainer — **Deploy the stack** (или **Pull and redeploy** для стека из git). Миграции базы применяются автоматически при старте, база хранится в `./data`.

### 4. Первый вход

1. Напишите боту `/start` с аккаунта из `TELEGRAM_ADMIN_ID`.
2. Админ панель → **Обновить соединения** — бот подтянет клиентов, уже созданных в панелях (дальше это делается автоматически раз в час).
3. Пригласите пользователей: **Пригласить по ссылке** или **Добавить пользователя**.

## Остальные настройки

Всё, что не вынесено в переменные compose, задаётся в [Host/appsettings.json](Host/appsettings.json) или переменной окружения вида `Раздел__Ключ` (например, `Monitoring__IntervalMinutes=10`):

| Ключ | По умолчанию | Назначение |
| --- | --- | --- |
| `Telegram:DefaultConnectionLimit` | 3 | Лимит соединений новых пользователей |
| `Telegram:InviteLifetimeDays` | 7 | Срок действия ссылки-приглашения, дней |
| `XUi:Flow` | `xtls-rprx-vision` | XTLS flow новых клиентов (для XHTTP — без flow) |
| `XUi:AllowInvalidCertificate` | `false` | Не проверять TLS-сертификат панели |
| `Monitoring:Enabled` | `true` | Проверять серверы и уведомлять админов |
| `Monitoring:IntervalMinutes` | 5 | Интервал проверки, минут |
| `Monitoring:FailureThreshold` | 2 | Сколько неудачных проверок подряд до уведомления |
| `HostingReminder:Enabled` | `false` | Напоминания об оплате хостинга |
| `HostingReminder:LastPaymentDate` | — | Дата последней оплаты, точка отсчёта |
| `HostingReminder:PeriodMonths` | 2 | Период оплаты, месяцев |
| `HostingReminder:Message` | — | Текст напоминания |

Полный список — в разделе «Настройка и развёртывание» [FEATURES.md](FEATURES.md).

## Разработка

Нужен [.NET SDK 10](https://dotnet.microsoft.com/download).

Для локального запуска положите секреты в `Host/appsettings.secret.json` — он подключается только в окружении Development и не попадает в git:

```json
{
  "Telegram": { "Token": "...", "AdminIds": [ 111111111 ] },
  "XUi": { "ApiUrl": "http://адрес-панели/путь/", "ApiToken": "..." }
}
```

```bash
dotnet run --project Host
```

Новая миграция после изменения сущностей:

```bash
dotnet ef migrations add ИмяМиграции --project Src/EFCore --startup-project Host
```

### Структура

| Проект | Что внутри |
| --- | --- |
| `Host` | Точка входа, конфигурация, применение миграций |
| `Src/Domain` | Сущности, перечисления, интерфейсы репозиториев и VPN-серверов |
| `Src/Application` | Репозитории, синхронизация соединений, приглашения, журнал действий |
| `Src/EFCore` | `AppDbContext`, конфигурации таблиц, миграции SQLite |
| `Src/XUi` | Клиент API 3x-ui (по сервису на каждый inbound, общий кэш списка клиентов) |
| `Src/AmneziaWg` | Клиент API amnezia-wg-easy |
| `Src/Telegram` | Обработчики команд, кнопок и текстовых сценариев, фоновые сервисы |
