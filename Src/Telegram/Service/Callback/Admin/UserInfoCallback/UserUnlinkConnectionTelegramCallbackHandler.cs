using Domain.Interfaces.Repository;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Common.Helper;
using Telegram.Enums;
using Telegram.Models;

namespace Telegram.Service.Callback.Admin.UserInfoCallback;

[UsedImplicitly]
public sealed class UserUnlinkConnectionTelegramCallbackHandler : TelegramAdminCallbackHandlerBase
{
	private readonly ITelegramBotClient _bot;
	private readonly IServiceScopeFactory _scopeFactory;

	public UserUnlinkConnectionTelegramCallbackHandler(
		ITelegramBotClient telegramBot,
		IOptions<TelegramOptions> telegramOptions,
		IServiceScopeFactory scopeFactory)
		: base(telegramBot, telegramOptions, scopeFactory)
	{
		_bot = telegramBot;
		_scopeFactory = scopeFactory;
	}

	public override TelegramCallbackEvent CallbackEvent => TelegramCallbackEvent.UserUnlinkConnection;

	public override async Task CallbackHandlerAsync(Update update, TelegramRequest request, CancellationToken cancellationToken)
	{
		var (chatId, _, _) = GetCallbackContext(update);

		if (!await IsAdminAsync(chatId, cancellationToken))
		{
			return;
		}

		await TryDeleteOriginMessageAsync(update, cancellationToken);

		var targetTelegramId = LongGuidCodec.Decode(request.Id);
		var peerId = request.Page;

		using var scope = _scopeFactory.CreateScope();
		var vpnUserRepo = scope.ServiceProvider.GetRequiredService<IVpnUserRepository>();
		var telegramUserRepo = scope.ServiceProvider.GetRequiredService<ITelegramUserRepository>();

		var user = await telegramUserRepo.GetUserByTelegramIdAsync(targetTelegramId, cancellationToken);
		if (user is null)
		{
			await _bot.SendMessage(
				chatId: chatId,
				text: $"Не удалось найти пользователя: {chatId}",
				cancellationToken: cancellationToken);

			return;
		}

		var deleted = await vpnUserRepo.RemoveLinkAsync(user.Id, peerId, cancellationToken);

		// Возвращаемся к списку соединений пользователя (страницу восстановить нельзя без доп. поля — вернёмся на 0)
		var backToConnections = new TelegramRequest(
			Id: request.Id,
			CallbackEvent: TelegramCallbackEvent.UserConnections,
			SourceEvent: TelegramCallbackEvent.UserInfo,
			Page: 0);

		await _bot.SendMessage(
			chatId: chatId,
			text: deleted > 0 ? "Связь разорвана." : "Связь не найдена (возможно, уже удалена).",
			replyMarkup: new InlineKeyboardMarkup(
				InlineKeyboardButton.WithCallbackData("⬅ К соединениям", Encode(backToConnections))),
			cancellationToken: cancellationToken);

		await AnswerAsync(update, cancellationToken);
	}
}