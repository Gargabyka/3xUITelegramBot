using Domain.Enums;
using Domain.Extensions;

namespace Telegram.Common.Helper;

/// <summary>
/// Тексты инструкций по подключению для каждого профиля
/// </summary>
public static class ConnectionInstructions
{
    public static string Get(VpnProfileType profile) => profile.IsVless() ? Vless(profile) : AmneziaWg();

    private static string Vless(VpnProfileType profile)
    {
        var note = profile switch
        {
            VpnProfileType.VlessXhttp => "\n\nПрофиль XHTTP требует свежую версию приложения. Если соединение не поднимается, обновите приложение.",
            VpnProfileType.VlessXhttpCdn => "\n\nЭтот профиль идёт через CDN: первое подключение и скорость могут быть чуть медленнее, это нормально.",
            _ => string.Empty,
        };

        return $"""
                📖 Как подключить {profile.Title()}

                1️⃣ Установите приложение:
                • Android: v2rayNG или Hiddify
                • iPhone / iPad: Streisand или V2Box
                • Windows / macOS / Linux: Hiddify

                2️⃣ Добавьте соединение одним из способов:
                • отсканируйте QR-код с карточки соединения (в приложении: «Добавить» → «Сканировать QR»);
                • или нажмите «Скачать ссылку», скопируйте текст из файла и в приложении выберите «Импорт из буфера обмена».

                3️⃣ Выберите добавленное соединение и нажмите «Подключить».

                Если интернет пропал после включения, проверьте, что соединение включено в карточке (не отключено администратором).{note}
                """;
    }

    private static string AmneziaWg() => """
                                         📖 Как подключить AmneziaWG

                                         1️⃣ Установите приложение AmneziaVPN (есть для Android, iPhone, Windows, macOS и Linux).
                                         На Android также подходит AmneziaWG.

                                         ⚠️ Обычное приложение WireGuard не подойдёт: конфигурация содержит параметры защиты от блокировок, которые оно не понимает.

                                         2️⃣ Нажмите «Скачать конфиг» в карточке соединения — придёт файл amnezia.conf.

                                         3️⃣ В приложении выберите «Добавить соединение» → «Файл с настройками» и укажите этот файл
                                         (или отсканируйте QR-код с карточки, если он есть).

                                         4️⃣ Нажмите «Подключиться».

                                         Конфигурацию не передавайте другим: она привязана к вашему соединению.
                                         """;
}
