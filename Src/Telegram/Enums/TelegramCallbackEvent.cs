using Telegram.Common.Attributes;

namespace Telegram.Enums;

/// <summary>
/// События, возникающие при callback от Telegram
/// </summary>
public enum TelegramCallbackEvent
{
    /// <summary>
    /// Получить информацию о клиенте
    /// </summary>
    [CallbackCode("gi")]
    GetFullInfo = 0,

    /// <summary>
    /// Получить список клиентов
    /// </summary>
    [CallbackCode("ls")]
    List = 1,

    /// <summary>
    /// Скачать конфиг=
    /// </summary>
    [CallbackCode("dc")]
    DownloadConfig = 2,

    /// <summary>
    /// Показать подтверждение удаления клиента
    /// </summary>
    [CallbackCode("dx")]
    DeleteClientAskConfirm = 3,

    /// <summary>
    /// Подтвердить удаление клиента
    /// </summary>
    [CallbackCode("dd")]
    DeleteClientConfirm = 4,

    /// <summary>
    /// Главное меню
    /// </summary>
    [CallbackCode("mm")]
    MainMenu = 10,

    /// <summary>
    /// Добавить клиента
    /// </summary>
    [CallbackCode("ac")]
    AddClient = 11,

    /// <summary>
    /// Отмена добавления клиента
    /// </summary>
    [CallbackCode("ax")]
    AddClientCancel = 12,

    /// <summary>
    /// Мои соединения (для текущего пользователя)
    /// </summary>
    [CallbackCode("mc")]
    MyConnections = 13,

    #region Admin

    /// <summary>
    /// Админ панель
    /// </summary>
    [CallbackCode("ap")]
    AdminPanel = 20,

    /// <summary>
    /// Добавить пользователя
    /// </summary>
    [CallbackCode("au")]
    AddUser = 21,

    /// <summary>
    /// Список пользователей
    /// </summary>
    [CallbackCode("ul")]
    UserList = 22,

    /// <summary>
    /// Подробная информация о пользователе
    /// </summary>
    [CallbackCode("ui")]
    UserInfo = 23,

    /// <summary>
    /// Посмотреть соединения пользователя
    /// </summary>
    [CallbackCode("uc")]
    UserConnections = 24,

    /// <summary>
    /// Добавить соединение пользователю
    /// </summary>
    [CallbackCode("ua")]
    UserAddConnection = 25,

    /// <summary>
    /// Удалить пользователя (запрос подтверждения)
    /// </summary>
    [CallbackCode("ud")]
    UserDeleteAskConfirm = 26,

    /// <summary>
    /// Изменить роль пользователя
    /// </summary>
    [CallbackCode("ur")]
    UserChangeRole = 27,

    /// <summary>
    /// Обновить соединения (синхронизация с 3x-ui API)
    /// </summary>
    [CallbackCode("rc")]
    RefreshConnections = 28,

    /// <summary>
    /// Обновить соединения (синхронизация с 3x-ui API)
    /// </summary>
    [CallbackCode("uf")]
    UserDeleteConfirm = 29,

    /// <summary>
    /// Добавить соединение пользователю (выбор конкретного соединения)
    /// </summary>
    [CallbackCode("ub")]
    UserAddConnectionSelect = 30,

    /// <summary>
    /// Разорвать связь пользователь↔соединение (VpnUsers)
    /// </summary>
    [CallbackCode("ux")]
    UserUnlinkConnection = 31,

    /// <summary>
    /// Включить/выключить напоминания об оплате хостинга для пользователя
    /// </summary>
    [CallbackCode("rt")]
    UserToggleReminderOptOut = 35,

    /// <summary>
    /// Включить/выключить клиента 3x-ui
    /// </summary>
    [CallbackCode("pt")]
    TogglePeerEnabled = 36,

    /// <summary>
    /// Изменить лимит соединений пользователя
    /// </summary>
    [CallbackCode("ul2")]
    UserChangeConnectionLimit = 37,

    #endregion
}