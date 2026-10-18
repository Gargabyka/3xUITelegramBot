using Domain.Entities;
using Domain.Interfaces;
using Domain.Interfaces.Repository;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Enums;
using Telegram.Models;
using XUi.Services.Interface;

namespace Telegram.Service.Callback.Admin;

[UsedImplicitly]
public sealed class RefreshConnectionsTelegramCallbackHandler : TelegramAdminCallbackHandlerBase
{
	private readonly ITelegramBotClient _telegramBot;
	private readonly IServiceScopeFactory _scopeFactory;

	public RefreshConnectionsTelegramCallbackHandler(
		ITelegramBotClient telegramBot,
		IOptions<TelegramOptions> telegramOptions,
		IServiceScopeFactory scopeFactory)
		: base(telegramBot, telegramOptions, scopeFactory)
	{
		_telegramBot = telegramBot;
		_scopeFactory = scopeFactory;
	}

	public override TelegramCallbackEvent CallbackEvent => TelegramCallbackEvent.RefreshConnections;

	public override async Task CallbackHandlerAsync(Update update, TelegramRequest request, CancellationToken cancellationToken)
	{
		var chatId = update.CallbackQuery?.Message?.Chat.Id;
		if (chatId is null)
		{
			return;
		}

		if (!await IsAdminAsync(chatId.Value, cancellationToken))
		{
			return;
		}

		await _telegramBot.SendMessage(
			chatId: chatId.Value,
			text: "Обновляю соединения из 3x-ui…",
			cancellationToken: cancellationToken);

		try
		{
			using var scope = _scopeFactory.CreateScope();

			var xuiApiService = scope.ServiceProvider.GetRequiredService<IXUiApiService>();
			var vpnPeerRepository = scope.ServiceProvider.GetRequiredService<IVpnPeerRepository>();
			var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

			var clients = await xuiApiService.GetClients(cancellationToken) ?? [];

			// текущие id из БД
			var existingIds = (await vpnPeerRepository.GetPeersAsync(cancellationToken))
				.Select(x => x.ClientId)
				.ToHashSet();

			// добавляем только новые, чтобы не словить уникальный индекс по ClientId
			var peersToAdd = clients
				.Where(x => !existingIds.Contains(x.Id))
				.Select(x => new VpnPeer
				{
					ClientId = x.Id,
					Name = x.Name,
					IsEnabled = x.Enabled,
					Address = x.Address,
					PublicKey = x.PublicKey,
					CreatedAt = DateOnly.FromDateTime(x.CreatedAt.UtcDateTime),
				})
				.ToArray();

			if (peersToAdd.Length > 0)
			{
				vpnPeerRepository.AddRange(peersToAdd);
				await unitOfWork.SaveChangesAsync(cancellationToken);
			}

			var skipped = clients.Length - peersToAdd.Length;

			await _telegramBot.SendMessage(
				chatId: chatId.Value,
				text: $"Готово.\nВсего в 3x-ui: {clients.Length}\nДобавлено: {peersToAdd.Length}\nПропущено (уже были): {skipped}",
				cancellationToken: cancellationToken);
		}
		catch (OperationCanceledException)
		{
			// отмена — не ошибка
			await _telegramBot.SendMessage(
				chatId: chatId.Value,
				text: "Операция отменена.",
				cancellationToken: cancellationToken);
		}
		catch (Exception)
		{
			// Не светим детали исключения в чат
			await _telegramBot.SendMessage(
				chatId: chatId.Value,
				text: "Не удалось обновить соединения. Проверьте логи приложения.",
				cancellationToken: cancellationToken);
		}
	}
}