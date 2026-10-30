using Application.Services;
using Domain.Entities;
using Domain.Enums;
using Domain.Extensions;
using Domain.Interfaces;
using Domain.Interfaces.Repository;
using Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Common.Helper;
using Telegram.Enums;
using Telegram.Models;
using Telegram.Service.Interface;
using Telegram.Store.Interfaces;

namespace Telegram.Service.TextFlows;

public sealed class AddClientNameTextFlowHandler : ITelegramTextFlowHandler
{
    private readonly ITelegramBotClient _bot;
    private readonly IAddClientStateStore _store;
    private readonly IVpnApiService _xuiApi;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AuditLogger _audit;
    private readonly ILogger<AddClientNameTextFlowHandler> _logger;

    public AddClientNameTextFlowHandler(
        ITelegramBotClient bot,
        IAddClientStateStore store,
        IVpnApiService xuiApi,
        IServiceScopeFactory scopeFactory,
        AuditLogger audit,
        ILogger<AddClientNameTextFlowHandler> logger)
    {
        _audit = audit;
        _bot = bot;
        _store = store;
        _xuiApi = xuiApi;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<bool> TryHandleAsync(Update update, CancellationToken cancellationToken)
    {
        var message = update.Message;
        if (message?.Text is null)
        {
            return false;
        }

        var chatId = message.Chat.Id;
        var text = message.Text.Trim();

        // Команды не перехватываем
        if (text.StartsWith('/'))
        {
            return false;
        }

        // Этот handler активен только когда store "ждёт имя"
        if (!_store.IsWaitingName(chatId))
        {
            return false;
        }

        if (!_store.TryGetProfile(chatId, out var profile))
        {
            return false;
        }

        var clientName = text;

        if (!ClientNameRules.IsValid(clientName))
        {
            await _bot.SendMessage(
                chatId: chatId,
                text: $"{ClientNameRules.Hint} Введите другое или нажмите «Отмена».",
                cancellationToken: cancellationToken);
            return true;
        }

        using (var limitCheckScope = _scopeFactory.CreateScope())
        {
            var telegramUserRepo = limitCheckScope.ServiceProvider.GetRequiredService<ITelegramUserRepository>();
            var vpnUserRepo = limitCheckScope.ServiceProvider.GetRequiredService<IVpnUserRepository>();

            var currentUser = await telegramUserRepo.GetUserByTelegramIdAsync(chatId, cancellationToken);
            if (currentUser is null)
            {
                _store.Clear(chatId);

                await _bot.SendMessage(
                    chatId: chatId,
                    text: $"Не удалось найти пользователя: {chatId}",
                    cancellationToken: cancellationToken);

                return true;
            }

            if (currentUser.MaxConnections is { } limit)
            {
                var currentCount = (await vpnUserRepo.GetConnectionsByTelegramIdAsync(currentUser.Id, cancellationToken)).Length;
                if (currentCount >= limit)
                {
                    _store.Clear(chatId);

                    await _bot.SendMessage(
                        chatId: chatId,
                        text: $"Достигнут лимит соединений: {currentCount}/{limit}. Обратитесь к администратору, чтобы увеличить лимит.",
                        cancellationToken: cancellationToken);

                    return true;
                }
            }
        }

        try
        {
            // Проверка дубликатов на сервере выбранного протокола
            var clients = await _xuiApi.GetClients(cancellationToken);
            if (clients.Any(c => c.Profile.IsVless() == profile.Type.IsVless() && string.Equals(c.Name, clientName, StringComparison.OrdinalIgnoreCase)))
            {
                await _bot.SendMessage(
                    chatId: chatId,
                    text: $"Клиент с именем '{clientName}' уже существует. Введите другое имя или нажмите «Отмена».",
                    cancellationToken: cancellationToken);
                return true;
            }

            // Создаём на сервере
            // Новое соединение сразу попадает в ссылку-подписку пользователя
            string? subscriptionId;
            using (var subscriptionScope = _scopeFactory.CreateScope())
            {
                subscriptionId = await subscriptionScope.ServiceProvider
                    .GetRequiredService<SubscriptionService>()
                    .GetOrCreateIdAsync(chatId, cancellationToken);
            }

            await _xuiApi.AddClientAsync(profile.Type, clientName, subscriptionId, cancellationToken);

            // Сразу подтягиваем созданного клиента (чтобы получить Guid Id и остальные поля)
            var updatedClients = await _xuiApi.GetClients(cancellationToken);
            var createdClient = updatedClients.FirstOrDefault(c => c.Profile.IsVless() == profile.Type.IsVless() && string.Equals(c.Name, clientName, StringComparison.OrdinalIgnoreCase));
            if (createdClient is null)
            {
                _store.Clear(chatId);

                await _bot.SendMessage(
                    chatId: chatId,
                    text: "Клиент создан, но не удалось получить его данные. Попробуйте позже или попросите администратора обновить соединения.",
                    cancellationToken: cancellationToken);

                return true;
            }

            // Сохраняем/обновляем peer в БД и создаём связь VpnUsers (клиент принадлежит создателю)
            using (var scope = _scopeFactory.CreateScope())
            {
                var peerRepo = scope.ServiceProvider.GetRequiredService<IVpnPeerRepository>();
                var vpnUserRepo = scope.ServiceProvider.GetRequiredService<IVpnUserRepository>();
                var telegramUserRepository = scope.ServiceProvider.GetRequiredService<ITelegramUserRepository>();
                var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                var domainClient = new VpnClient(
                    Name: createdClient.Name,
                    Enabled: createdClient.Enabled,
                    Address: createdClient.Address,
                    PublicKey: createdClient.PublicKey,
                    CreatedAt: DateOnly.FromDateTime(createdClient.CreatedAt.UtcDateTime),
                    Profile: createdClient.Profile);

                var peerEntity = await peerRepo.GetByClientIdAsync(createdClient.Id, cancellationToken);
                if (peerEntity is null)
                {
                    peerEntity = new VpnPeer
                    {
                        ClientId = createdClient.Id,
                        Name = domainClient.Name,
                        Profile = domainClient.Profile,
                        IsEnabled = domainClient.Enabled,
                        Address = domainClient.Address,
                        PublicKey = domainClient.PublicKey,
                        CreatedAt = domainClient.CreatedAt,
                    };

                    peerRepo.Add(peerEntity);
                }
                else
                {
                    // На всякий случай фиксируем ClientId, если запись была заведена криво
                    peerEntity.ClientId = createdClient.Id;
                    peerEntity.Update(domainClient);
                }

                await uow.SaveChangesAsync(cancellationToken);

                var user = await telegramUserRepository.GetUserByTelegramIdAsync(chatId, cancellationToken);
                if (user is null)
                {
	                await _bot.SendMessage(
		                chatId: chatId,
		                text: $"Не удалось найти пользователя: {chatId}",
		                cancellationToken: cancellationToken);

	                return true;
                }

                var exists = await vpnUserRepo.ExistsAsync(user.Id, peerEntity.Id, cancellationToken);
                if (!exists)
                {
                    vpnUserRepo.Add(new VpnUser
                    {
                        TelegramUserId = user.Id,
                        PeerId = peerEntity.Id,
                    });

                    await uow.SaveChangesAsync(cancellationToken);
                }
            }

            _store.Clear(chatId);

            await _audit.LogAsync(chatId, AuditAction.ClientCreated, $"'{clientName}' ({profile.Title})", cancellationToken);

            var toMyConnections = new TelegramRequest(Guid.Empty, TelegramCallbackEvent.MyConnections, TelegramCallbackEvent.MainMenu);
            var toMenu = new TelegramRequest(Guid.Empty, TelegramCallbackEvent.MainMenu, TelegramCallbackEvent.MainMenu);

            var markup = new InlineKeyboardMarkup([
                [InlineKeyboardButton.WithCallbackData("🔌 Список соединений", TelegramCallbackDataCodec.Encode(toMyConnections))],
                [InlineKeyboardButton.WithCallbackData("⬅ В главное меню", TelegramCallbackDataCodec.Encode(toMenu))]
            ]);

            await _bot.SendMessage(
                chatId: chatId,
                text: $"Клиент '{clientName}' добавлен и привязан к вашему аккаунту.",
                replyMarkup: markup,
                cancellationToken: cancellationToken);

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _store.Clear(chatId);
            _logger.LogError(ex, "Не удалось добавить клиента {ClientName} ({Profile})", clientName, profile.Type);

            await _bot.SendMessage(
                chatId: chatId,
                text: "Не удалось добавить клиента: сервер недоступен или вернул ошибку. Попробуйте позже или обратитесь к администратору.",
                cancellationToken: cancellationToken);

            return true;
        }
    }
}