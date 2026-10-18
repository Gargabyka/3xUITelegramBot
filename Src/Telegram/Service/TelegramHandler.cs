using Domain.Interfaces;
using Domain.Interfaces.Repository;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Common.Helper;
using Telegram.Models;
using Telegram.Service.Factory;
using Telegram.Service.Interface;

namespace Telegram.Service;

public sealed class TelegramHandler : ITelegramHandler
{
    private readonly ITelegramBotClient _telegramBot;
    private readonly TelegramOptions _telegramOptions;
    private readonly TelegramUpdateFactory _updateFactory;
    private readonly TelegramCallbackFactory _callbackFactory;
    private readonly IReadOnlyList<ITelegramTextFlowHandler> _textFlows;
    private readonly IServiceScopeFactory _scopeFactory;

    public TelegramHandler(
        ITelegramBotClient telegramBot,
        IOptions<TelegramOptions> telegramOptions,
        TelegramUpdateFactory updateFactory,
        TelegramCallbackFactory callbackFactory,
        IEnumerable<ITelegramTextFlowHandler> textFlows,
        IServiceScopeFactory scopeFactory)
    {
        _telegramBot = telegramBot;
        _updateFactory = updateFactory;
        _callbackFactory = callbackFactory;
        _telegramOptions = telegramOptions.Value;
        _textFlows = textFlows.ToList();
        _scopeFactory = scopeFactory;
    }

    public async Task HandleAsync(Update update, CancellationToken cancellationToken)
    {
        switch (update.Type)
        {
            case UpdateType.Message:
                await HandleUpdateAsync(update, cancellationToken);
                break;
            case UpdateType.CallbackQuery:
                await HandleCallbackQueryAsync(update, cancellationToken);
                break;
            default:
                return;
        }
    }

    private async Task HandleUpdateAsync(Update update, CancellationToken cancellationToken)
    {
	    var message = update.Message;
	    if (message is null)
	    {
		    return;
	    }

	    var chatId = message.Chat.Id;

	    if (!await IsAllowedAsync(chatId, cancellationToken))
	    {
		    await _telegramBot.SendMessage(chatId, "Доступ запрещён.", cancellationToken: cancellationToken);
		    return;
	    }

	    // Если пользователь есть в таблице — обновим профильные данные при любом использовании бота
	    await TrySyncTelegramUserProfileAsync(chatId, message.From, cancellationToken);

	    var text = message.Text?.Trim();

	    if (text is null || !text.StartsWith('/'))
	    {
		    foreach (var flow in _textFlows)
		    {
			    if (await flow.TryHandleAsync(update, cancellationToken))
			    {
				    return;
			    }
		    }
	    }

	    if (text is null)
	    {
		    return;
	    }

	    var handler = _updateFactory.CreateHandler(text);
	    await handler.UpdateHandlerAsync(update, cancellationToken);
    }

    private async Task HandleCallbackQueryAsync(Update update, CancellationToken cancellationToken)
    {
        var callbackQuery = update.CallbackQuery;

        if (callbackQuery?.Data is null)
        {
            return;
        }

        var chatId = callbackQuery.Message?.Chat.Id;
        if (chatId is null)
        {
            return;
        }

        if (!await IsAllowedAsync(chatId.Value, cancellationToken))
        {
            await _telegramBot.AnswerCallbackQuery(
                callbackQueryId: callbackQuery.Id,
                text: "Доступ запрещён.",
                cancellationToken: cancellationToken);
            return;
        }

        // Синхронизация профиля при нажатии любых кнопок
        await TrySyncTelegramUserProfileAsync(chatId.Value, callbackQuery.From, cancellationToken);

        if (!TelegramCallbackDataCodec.TryDecode(callbackQuery.Data, out var request))
        {
            return;
        }

        var handler = _callbackFactory.CreateHandler(request);
        await handler.CallbackHandlerAsync(update, request, cancellationToken);
    }

    private async Task<bool> IsAllowedAsync(long telegramId, CancellationToken cancellationToken)
    {
        if (_telegramOptions.AdminIds.Contains(telegramId))
        {
            return true;
        }

        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetService<ITelegramUserRepository>();
        if (repo is null)
        {
            return false;
        }

        var user = await repo.GetUserByTelegramIdAsync(telegramId, cancellationToken);
        return user is not null;
    }

    private async Task TrySyncTelegramUserProfileAsync(long telegramId, User? from, CancellationToken cancellationToken)
    {
        if (from is null)
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var userRepository = scope.ServiceProvider.GetService<ITelegramUserRepository>();
        var unitOfWork = scope.ServiceProvider.GetService<IUnitOfWork>();
        if (userRepository is null || unitOfWork is null)
        {
            return;
        }

        var user = await userRepository.GetUserByTelegramIdForUpdateAsync(telegramId, cancellationToken);
        if (user != null)
        {
	        user.Update(from.FirstName, from.LastName, from.Username);
	        await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}