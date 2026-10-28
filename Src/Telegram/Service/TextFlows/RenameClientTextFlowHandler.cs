using Domain.Enums;
using Domain.Extensions;
using Domain.Interfaces;
using Domain.Interfaces.Repository;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Common.Helper;
using Telegram.Enums;
using Telegram.Models;
using Telegram.Service.Interface;
using Telegram.Store;

namespace Telegram.Service.TextFlows;

/// <summary>
/// Ввод нового имени соединения (после кнопки «Переименовать»)
/// </summary>
public sealed class RenameClientTextFlowHandler : ITelegramTextFlowHandler
{
	private readonly ITelegramBotClient _bot;
	private readonly ExpiringStateStore<RenameClientState> _store;
	private readonly IVpnApiService _vpnApiService;
	private readonly TelegramAccessService _access;
	private readonly AuditLogger _audit;
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly ILogger<RenameClientTextFlowHandler> _logger;

	public RenameClientTextFlowHandler(
		ITelegramBotClient bot,
		ExpiringStateStore<RenameClientState> store,
		IVpnApiService vpnApiService,
		TelegramAccessService access,
		AuditLogger audit,
		IServiceScopeFactory scopeFactory,
		ILogger<RenameClientTextFlowHandler> logger)
	{
		_bot = bot;
		_store = store;
		_vpnApiService = vpnApiService;
		_access = access;
		_audit = audit;
		_scopeFactory = scopeFactory;
		_logger = logger;
	}

	public async Task<bool> TryHandleAsync(Update update, CancellationToken cancellationToken)
	{
		var message = update.Message;
		if (message?.Text is null || message.Text.StartsWith('/'))
		{
			return false;
		}

		var chatId = message.Chat.Id;
		if (!_store.TryGet(chatId, out var state))
		{
			return false;
		}

		var newName = message.Text.Trim();
		if (!ClientNameRules.IsValid(newName))
		{
			await _bot.SendMessage(chatId, $"{ClientNameRules.Hint} Введите другое или нажмите «Отмена».", cancellationToken: cancellationToken);
			return true;
		}

		// Права проверяем ещё раз: соединение могли отвязать, пока пользователь набирал имя
		if (!await _access.CanManagePeerAsync(chatId, state.ClientId, cancellationToken))
		{
			_store.Clear(chatId);
			await _bot.SendMessage(chatId, "Это соединение вам не принадлежит.", cancellationToken: cancellationToken);
			return true;
		}

		var backToCard = new TelegramRequest(state.ClientId, TelegramCallbackEvent.GetFullInfo, state.SourceEvent, state.Page);
		var backMarkup = new InlineKeyboardMarkup(
			InlineKeyboardButton.WithCallbackData("⬅ К соединению", TelegramCallbackDataCodec.Encode(backToCard)));

		string oldName;
		try
		{
			var clients = await _vpnApiService.GetClients(cancellationToken);
			var client = clients.FirstOrDefault(x => x.Id == state.ClientId);
			if (client is null)
			{
				_store.Clear(chatId);
				await _bot.SendMessage(chatId, "Не удалось найти соединение на сервере.", cancellationToken: cancellationToken);
				return true;
			}

			oldName = client.Name;
			if (string.Equals(oldName, newName, StringComparison.Ordinal))
			{
				_store.Clear(chatId);
				await _bot.SendMessage(chatId, "Имя не изменилось.", replyMarkup: backMarkup, cancellationToken: cancellationToken);
				return true;
			}

			// Имя уникально в пределах сервера протокола (у всех профилей VLESS общий 3x-ui)
			var duplicate = clients.Any(c => c.Id != client.Id
			                                 && c.Profile.IsVless() == client.Profile.IsVless()
			                                 && string.Equals(c.Name, newName, StringComparison.OrdinalIgnoreCase));
			if (duplicate)
			{
				await _bot.SendMessage(
					chatId,
					$"Соединение с именем '{newName}' уже существует. Введите другое имя или нажмите «Отмена».",
					cancellationToken: cancellationToken);
				return true;
			}

			await _vpnApiService.RenameClientAsync(state.ClientId, newName, cancellationToken);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_store.Clear(chatId);
			_logger.LogError(ex, "Не удалось переименовать клиента {ClientId}", state.ClientId);
			await _bot.SendMessage(
				chatId,
				"Не удалось переименовать соединение: сервер недоступен или вернул ошибку. Попробуйте позже.",
				replyMarkup: backMarkup,
				cancellationToken: cancellationToken);
			return true;
		}

		_store.Clear(chatId);

		await UpdateLocalNameAsync(state.ClientId, newName, cancellationToken);
		await _audit.LogAsync(chatId, AuditAction.ClientRenamed, $"'{oldName}' → '{newName}'", cancellationToken);

		await _bot.SendMessage(
			chatId,
			$"Соединение переименовано: '{oldName}' → '{newName}'.",
			replyMarkup: backMarkup,
			cancellationToken: cancellationToken);

		return true;
	}

	/// <summary>
	/// Локальная копия — best-effort: сервер уже переименовал клиента, а синхронизация поправит БД в течение часа
	/// </summary>
	private async Task UpdateLocalNameAsync(Guid clientId, string newName, CancellationToken cancellationToken)
	{
		try
		{
			using var scope = _scopeFactory.CreateScope();
			var peerRepo = scope.ServiceProvider.GetRequiredService<IVpnPeerRepository>();
			var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

			var peer = await peerRepo.GetByClientIdAsync(clientId, cancellationToken);
			if (peer is not null)
			{
				peer.Name = newName;
				await uow.SaveChangesAsync(cancellationToken);
			}
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogWarning(ex, "Клиент {ClientId} переименован на сервере, но не удалось обновить локальную копию", clientId);
		}
	}
}
