using System.Net;
using Application.Services;
using Domain.Extensions;
using Domain.Interfaces;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
/// Ссылка-подписка соединения <see cref="TelegramCallbackEvent.ClientSubscription"/>: ссылка для копирования и QR-код
/// </summary>
[UsedImplicitly]
public sealed class ClientSubscriptionTelegramCallbackHandler : TelegramCallbackHandlerBase
{
	// Лимит Telegram на подпись к фото
	private const int MaxCaptionLength = 1024;

	private readonly ITelegramBotClient _telegramBot;
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly IVpnApiService _vpnApiService;
	private readonly TelegramAccessService _access;
	private readonly SubscriptionOptions _options;
	private readonly ILogger<ClientSubscriptionTelegramCallbackHandler> _logger;

	public ClientSubscriptionTelegramCallbackHandler(
		ITelegramBotClient telegramBot,
		IServiceScopeFactory scopeFactory,
		IVpnApiService vpnApiService,
		TelegramAccessService access,
		IOptions<SubscriptionOptions> options,
		ILogger<ClientSubscriptionTelegramCallbackHandler> logger)
		: base(telegramBot)
	{
		_telegramBot = telegramBot;
		_scopeFactory = scopeFactory;
		_vpnApiService = vpnApiService;
		_access = access;
		_options = options.Value;
		_logger = logger;
	}

	public override TelegramCallbackEvent CallbackEvent => TelegramCallbackEvent.ClientSubscription;

	public override async Task CallbackHandlerAsync(Update update, TelegramRequest request, CancellationToken cancellationToken)
	{
		var (chatId, _, _) = GetCallbackContext(update);

		if (!_options.Enabled)
		{
			await DenyAsync(update, "Подписки не настроены.", cancellationToken);
			return;
		}

		if (!await _access.CanManagePeerAsync(chatId, request.Id, cancellationToken))
		{
			await DenyAsync(update, "Это соединение вам не принадлежит.", cancellationToken);
			return;
		}

		// Карточку соединения не удаляем: подписка приходит отдельным сообщением, как и скачанная ссылка
		await AnswerAsync(update, cancellationToken);

		var backToCard = new TelegramRequest(request.Id, TelegramCallbackEvent.GetFullInfo, request.SourceEvent, request.Page);
		var backButton = InlineKeyboardButton.WithCallbackData("⬅ К соединению", Encode(backToCard));

		string? subscriptionId;
		try
		{
			using var scope = _scopeFactory.CreateScope();
			subscriptionId = await scope.ServiceProvider
				.GetRequiredService<SubscriptionService>()
				.GetClientSubscriptionIdAsync(request.Id, cancellationToken);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogWarning(ex, "Не удалось получить ссылку-подписку клиента {ClientId}", request.Id);
			subscriptionId = null;
		}

		if (subscriptionId is null)
		{
			await _telegramBot.SendMessage(
				chatId: chatId,
				text: "Не удалось получить подписку: соединение не найдено или сервер недоступен. Попробуйте позже.",
				replyMarkup: new InlineKeyboardMarkup(backButton),
				cancellationToken: cancellationToken);
			return;
		}

		var clientName = (await _vpnApiService.GetClients(cancellationToken)).FirstOrDefault(x => x.Id == request.Id) is { } client
			? $"{client.Name} [{client.Profile.Title()}]"
			: string.Empty;

		var url = _options.BuildUrl(subscriptionId);
		var markup = new InlineKeyboardMarkup([
			[InlineKeyboardButton.WithUrl("🌐 Открыть подписку", url)],
			[backButton],
		]);

		// HTML: ссылка в <code> копируется нажатием
		var caption = $"""
		               🔗 <b>Подписка {WebUtility.HtmlEncode(clientName)}</b>
		               <code>{WebUtility.HtmlEncode(url)}</code>

		               В отличие от обычной ссылки, подписку приложение обновляет само: если поменяются настройки сервера, переимпортировать ничего не придётся.

		               <b>Как добавить:</b> нажмите на ссылку, чтобы скопировать, и в приложении (Happ, Hiddify, v2RayTun, v2rayNG) выберите добавление подписки или импорт из буфера. Или отсканируйте QR-код.

		               Не передавайте ссылку другим: по ней можно подключиться к этому соединению.
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
