using Domain.Entities;
using Domain.Interfaces;
using Domain.Interfaces.Repository;
using Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Common.Helper;
using Telegram.Enums;
using Telegram.Models;
using Telegram.Service.Interface;
using Telegram.Store.Interfaces;
using XUi.Services.Interface;

namespace Telegram.Service.TextFlows;

public sealed class AddClientNameTextFlowHandler : ITelegramTextFlowHandler
{
    private readonly ITelegramBotClient _bot;
    private readonly IAddClientStateStore _store;
    private readonly IXUiApiService _xuiApi;
    private readonly IServiceScopeFactory _scopeFactory;

    public AddClientNameTextFlowHandler(
        ITelegramBotClient bot,
        IAddClientStateStore store,
        IXUiApiService xuiApi,
        IServiceScopeFactory scopeFactory)
    {
        _bot = bot;
        _store = store;
        _xuiApi = xuiApi;
        _scopeFactory = scopeFactory;
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

        var clientName = text;

        if (clientName.Length < 3)
        {
            await _bot.SendMessage(
                chatId: chatId,
                text: "Имя слишком короткое. Введите другое (минимум 3 символа) или нажмите «Отмена».",
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
            // Проверка дубликатов в 3x-ui
            var clients = await _xuiApi.GetClients(cancellationToken);
            if (clients is not null && clients.Any(c => string.Equals(c.Name, clientName, StringComparison.OrdinalIgnoreCase)))
            {
                await _bot.SendMessage(
                    chatId: chatId,
                    text: $"Клиент с именем '{clientName}' уже существует. Введите другое имя или нажмите «Отмена».",
                    cancellationToken: cancellationToken);
                return true;
            }

            // Создаём в 3x-ui
            await _xuiApi.AddClientAsync(clientName, cancellationToken);

            // Сразу подтягиваем созданного клиента (чтобы получить Guid Id и остальные поля)
            var updatedClients = await _xuiApi.GetClients(cancellationToken) ?? [];
            var createdClient = updatedClients.FirstOrDefault(c => string.Equals(c.Name, clientName, StringComparison.OrdinalIgnoreCase));
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
                    CreatedAt: DateOnly.FromDateTime(createdClient.CreatedAt.UtcDateTime));

                var peerEntity = await peerRepo.GetByClientIdAsync(createdClient.Id, cancellationToken);
                if (peerEntity is null)
                {
                    peerEntity = new VpnPeer
                    {
                        ClientId = createdClient.Id,
                        Name = domainClient.Name,
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
        catch (Exception ex)
        {
            _store.Clear(chatId);

            await _bot.SendMessage(
                chatId: chatId,
                text: $"Не удалось добавить клиента: {ex.Message}",
                cancellationToken: cancellationToken);

            return true;
        }
    }
}