using System.Globalization;
using System.Text;

namespace SkVP.Services;

/// <summary>
/// Zápis kalendára vo formáte iCalendar (RFC 5545) – otvorí ho Outlook, Google Kalendár aj kalendár Windows.
/// Texty sa escapujú, dlhé riadky sa zalamujú po 75 bajtoch (UTF-8) a koniec riadku je CRLF, ako vyžaduje norma.
/// </summary>
public sealed class Ics
{
    private readonly StringBuilder _sb = new();
    private readonly string _razitko = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    public Ics(string nazov)
    {
        Riadok("BEGIN:VCALENDAR");
        Riadok("VERSION:2.0");
        Riadok("PRODID:-//SkVP//Kalendar//SK");
        Riadok("CALSCALE:GREGORIAN");
        Riadok("METHOD:PUBLISH");
        Riadok("X-WR-CALNAME:" + Text(nazov));
    }

    /// <summary>Jedna udalosť. Celodenná udalosť končí posledným dňom (vrátane), ostatné časom konca (inak o hodinu).</summary>
    public void Udalost(string uid, string nazov, DateTime zaciatok, DateTime? koniec, bool celyDen, string? popis = null,
        string? kategoria = null, string? miesto = null, string? url = null, string? pravidlo = null, int? pripomienkaMinut = null,
        bool zrusena = false, bool volno = false)
    {
        Riadok("BEGIN:VEVENT");
        Riadok($"UID:{uid}");
        Riadok($"DTSTAMP:{_razitko}");
        if (celyDen)
        {
            var posledny = (koniec ?? zaciatok).Date < zaciatok.Date ? zaciatok.Date : (koniec ?? zaciatok).Date;
            Riadok("DTSTART;VALUE=DATE:" + zaciatok.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
            Riadok("DTEND;VALUE=DATE:" + posledny.AddDays(1).ToString("yyyyMMdd", CultureInfo.InvariantCulture));
        }
        else
        {
            var k = koniec is { } x && x > zaciatok ? x : zaciatok.AddHours(1);
            Riadok("DTSTART:" + zaciatok.ToString("yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture));
            Riadok("DTEND:" + k.ToString("yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture));
        }

        Riadok("SUMMARY:" + Text(nazov));
        if (!string.IsNullOrWhiteSpace(popis))
        {
            Riadok("DESCRIPTION:" + Text(popis));
        }

        if (!string.IsNullOrWhiteSpace(kategoria))
        {
            Riadok("CATEGORIES:" + Text(kategoria));
        }

        if (!string.IsNullOrWhiteSpace(miesto))
        {
            Riadok("LOCATION:" + Text(miesto));
        }

        if (!string.IsNullOrWhiteSpace(url))
        {
            Riadok("URL:" + url);
        }

        if (pravidlo is not null)
        {
            Riadok("RRULE:" + pravidlo);
        }

        Riadok("STATUS:" + (zrusena ? "CANCELLED" : "CONFIRMED"));
        if (volno)
        {
            Riadok("TRANSP:TRANSPARENT");
        }

        if (pripomienkaMinut is { } minut)
        {
            Riadok("BEGIN:VALARM");
            Riadok("ACTION:DISPLAY");
            Riadok("DESCRIPTION:" + Text(nazov));
            Riadok($"TRIGGER:-PT{minut}M");
            Riadok("END:VALARM");
        }

        Riadok("END:VEVENT");
    }

    public override string ToString() => _sb + "END:VCALENDAR\r\n";

    /// <summary>Text pre iCalendar: spätná lomka, bodkočiarka, čiarka a koniec riadku sa zapíšu so spätnou lomkou.</summary>
    public static string Text(string text) =>
        text.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\r\n", "\\n").Replace("\n", "\\n").Replace("\r", "");

    /// <summary>Zapíše riadok; dlhší ako 75 bajtov zalomí (pokračovanie začína medzerou), znaky sa nerozdelia.</summary>
    private void Riadok(string riadok)
    {
        var bajty = 0;
        for (var i = 0; i < riadok.Length; i++)
        {
            var dlzka = char.IsSurrogatePair(riadok, i) ? 4 : Encoding.UTF8.GetByteCount(riadok, i, 1);
            if (bajty + dlzka > 75)
            {
                _sb.Append("\r\n ");
                bajty = 1;
            }

            _sb.Append(riadok[i]);
            if (dlzka == 4)
            {
                _sb.Append(riadok[++i]);
            }

            bajty += dlzka;
        }

        _sb.Append("\r\n");
    }
}
