namespace UnscRelay.Core;

public sealed record TerminalTarget(string Id, string Name, string Host, int Port, string Username, bool Connected, string Protocol);
public static class MultiSend
{
    public static TerminalTarget[] Validate(IEnumerable<TerminalTarget> available, string[] ids, string text)
    {
        if (ids.Length is < 1 or > 30 || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
            throw new ArgumentException("Seleziona da 1 a 30 terminali distinti.");
        if (string.IsNullOrWhiteSpace(text) || text.Length > 32768 || text.Any(c => char.IsControl(c) && c is not ('\n' or '\r' or '\t')))
            throw new ArgumentException("Testo non valido: massimo 32768 caratteri, senza sequenze di controllo.");
        var targets = available.ToDictionary(t => t.Id, StringComparer.Ordinal);
        return ids.Select(id => targets.TryGetValue(id, out var t) && t.Connected && t.Protocol == "ssh"
            ? t : throw new InvalidOperationException("Un terminale selezionato non e piu connesso. Nessun invio eseguito.")).ToArray();
    }
}
