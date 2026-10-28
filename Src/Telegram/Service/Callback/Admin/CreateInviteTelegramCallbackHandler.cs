using Application.Services;
using Domain.Enums;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Enums;
using Telegram.Models;

namespace Telegram.Service.Callback.Admin;

/// <summary>
/// Создаёт одноразовую ссылку-приглашение <see cref="TelegramCallbackEvent.CreateInvite"/>
/// </summary>
[UsedImplicitly]
public sealed class CreateInviteTelegramCallbackHandler : TelegramAdminCallbackHandlerBase
{
	private readonly ITelegramBotClient _telegramBot;
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly AuditLogger _audit;
	private readonly TelegramOptions _telegramOptions;

	public CreateInviteTelegramCallbackHandler(
		ITelegramBotClient telegramBot,
		IOptions<TelegramOptions> telegramOptions,
		IServiceScopeFactory scopeFactory,
		AuditLogger audit)
		: base(telegramBot, telegramOptions, scopeFactory)
	{
		_audit = audit;
		_telegramBot = telegramBot;
		_scopeFactory = scopeFactory;
		_telegramOptions = telegramOptions.Value;
	}

	public override TelegramCallbackEvent CallbackEvent => TelegramCallbackEvent.CreateInvite;

	public override async Task CallbackHandlerAsync(Update update, TelegramRequest request, CancellationToken cancellationToken)
	{
		var chatId = update.CallbackQuery?.Message?.Chat.Id;
		if (chatId is null || !await IsAdminAsync(chatId.Value, cancellationToken))
		{
			return;
		}

		var lifetime = TimeSpan.FromDays(Math.Max(1, _telegramOptions.InviteLifetimeDays));

		string token;
		using (var scope = _scopeFactory.CreateScope())
		{
			var inviteService = scope.ServiceProvider.GetRequiredService<InviteService>();
			token = await inviteService.CreateAsync(chatId.Value, lifetime, cancellationToken);
		}

		await _audit.LogAsync(chatId.Value, AuditAction.InviteCreated, $"срок действия {lifetime.TotalDays:0} дн.", cancellationToken);

		var me = await _telegramBot.GetMe(cancellationToken);
		var link = $"https://t.me/{me.Username}?start=inv_{token}";

		var backRequest = BackTo(TelegramCallbackEvent.AdminPanel, sourceEvent: TelegramCallbackEvent.AdminPanel);

		await _telegramBot.SendMessage(
			chatId: chatId.Value,
			text: $"""
			       🔗 Ссылка-приглашение:

			       {link}

			       Сработает один раз и действует {lifetime.TotalDays:0} дн. Тот, кто перейдёт по ней, получит доступ с ролью «Пользователь».
			       """,
			replyMarkup: new InlineKeyboardMarkup([
				[InlineKeyboardButton.WithCallbackData("⬅ В админ панель", Encode(backRequest))]
			]),
			cancellationToken: cancellationToken);

		await AnswerAsync(update, cancellationToken);
	}
}
