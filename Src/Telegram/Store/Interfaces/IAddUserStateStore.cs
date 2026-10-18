namespace Telegram.Store.Interfaces;

public interface IAddUserStateStore
{
	public void Begin(long chatId);

	public bool IsWaitingContact(long chatId);

	public void Clear(long chatId);
}