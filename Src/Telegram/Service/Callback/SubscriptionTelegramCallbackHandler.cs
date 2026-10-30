using System.Net;
using Application.Services;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Common.Helper;
using Telegram.Enums;
using Telegram.Models;

namespace Telegram.Service.Callback;

/// <summary>
/// Ссылка-подписка пользователя <see cref="TelegramCallbackEvent.Subscription"/>
/// </summary>
[UsedImplicitly]
public sealed class SubscriptionTelegramCallbackHandler : TelegramCallbackHandlerBase
{
	// Лимит Telegram на подпись к фото
	private const int MaxCaptionLength = 1024;

	private readonly ITelegramBotClient _telegramBot;
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly SubscriptionOptions _options;

	public SubscriptionTelegramCallbackHandler(
		ITelegramBotClient telegramBot,
		IServiceScopeFactory scopeFactory,
		IOptions<SubscriptionOptions> options)
		: base(telegramBot)
	{
		_telegramBot = telegramBot;
		_scopeFactory = scopeFactory;
		_options = options.Value;
	}

	public override TelegramCallbackEvent CallbackEvent => TelegramCallbackEvent.Subscription;

	public override async Task CallbackHandlerAsync(Update update, TelegramRequest request, CancellationToken cancellationToken)
	{
		var (chatId, _, _) = GetCallbackContext(update);

		if (!_options.Enabled)
		{
			await DenyAsync(update, "Подписки не настроены.", cancellationToken);
			return;
		}

		await TryDeleteOriginMessageAsync(update, cancellationToken);
		await AnswerAsync(update, cancellationToken);

		var backMarkup = new InlineKeyboardMarkup(
			InlineKeyboardButton.WithCallbackData("⬅ В главное меню", Encode(BackTo(TelegramCallbackEvent.MainMenu))));

		// Каждый раз сверяем соединения с подпиской: так в неё попадают и соединения, привязанные админом
		SubscriptionSyncResult? result;
		using (var scope = _scopeFactory.CreateScope())
		{
			result = await scope.ServiceProvider.GetRequiredService<SubscriptionService>().SyncAsync(chatId, cancellationToken);
		}

		if (result is null or { VlessConnections: 0 })
		{
			await _telegramBot.SendMessage(
				chatId: chatId,
				text: "В подписку попадают соединения VLESS, а у вас их пока нет. Создайте соединение VLESS — и подписка появится здесь.",
				replyMarkup: backMarkup,
				cancellationToken: cancellationToken);
			return;
		}

		var url = _options.BuildUrl(result.SubscriptionId);
		var markup = new InlineKeyboardMarkup([
			[InlineKeyboardButton.WithUrl("🌐 Открыть подписку", url)],
			[InlineKeyboardButton.WithCallbackData("⬅ В главное меню", Encode(BackTo(TelegramCallbackEvent.MainMenu)))],
		]);

		var warning = result.Failed > 0
			? $"\n\n⚠ {result.Failed} из {result.VlessConnections} соединений пока не в подписке — откройте её ещё раз позже."
			: string.Empty;

		// HTML: ссылка в <code> копируется нажатием. Подпись к фото — не длиннее 1024 символов
		var caption = $"""
		               🔗 <b>Ваша подписка</b>
		               <code>{WebUtility.HtmlEncode(url)}</code>

		               Все ваши соединения VLESS ({result.VlessConnections}) одной ссылкой. Новые и изменённые приложение подтянет само.

		               <b>Как добавить:</b> скопируйте ссылку нажатием и в Happ, Hiddify, v2RayTun или v2rayNG выберите импорт подписки из буфера. Или отсканируйте QR-код.
		               Hiddify сам переключается на работающее соединение, в остальных выбирайте вручную.

		               AmneziaWG в подписку не входит. Не передавайте ссылку другим.{warning}
		               """;

		var qrPng = QrCodeHelper.TryBuildPng(url);

		// Длина с HTML-тегами — оценка сверху: Telegram считает подпись уже без них
		if (qrPng is null || caption.Length > MaxCaptionLength)
		{
			await _telegramBot.SendMessage(chatId, caption, parseMode: ParseMode.Html, replyMarkup: markup, cancellationToken: cancellationToken);
			return;
		}

		await using var ms = new MemoryStream(qrPng, writable: false);
		await _telegramBot.SendPhoto(
			chatId: chatId,
			photo: InputFile.FromStream(ms, fileName: "subscription.png"),
			caption: caption,
			parseMode: ParseMode.Html,
			replyMarkup: markup,
			cancellationToken: cancellationToken);
	}
}
