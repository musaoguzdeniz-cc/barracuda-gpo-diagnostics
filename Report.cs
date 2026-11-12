using System.Diagnostics;
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace Barracuda;

internal enum Scope { Computer, User }
internal enum RowKind { Setting, Gpo }

internal sealed record PolicyRow(Scope Scope, RowKind Kind, string Area, string Name, string Value, string Source, string Xml)
{
    public string Display => $"{Name}\n{(Scope == Scope.Computer ? "Bilgisayar" : "Kullanıcı")}  ·  {Area}  ·  {Value}";
    public bool Contains(string text) =>
        new[] { Area, Name, Value, Source, Xml }.Any(value => value.Contains(text, StringComparison.OrdinalIgnoreCase));
}

internal sealed record PolicyReport(IReadOnlyList<PolicyRow> Rows, IReadOnlyList<Scope> Scopes, string Note)
{
    private const long MaxReportBytes = 32L * 1024 * 1024;
    private static readonly string[] ValueElements =
        ["State", "Setting", "Value", "SettingNumber", "SettingBoolean", "SettingString", "SettingStrings"];

    public static PolicyReport Read(string path)
    {
        using var input = File.OpenRead(path);
        if (input.Length > MaxReportBytes) throw new InvalidDataException("XML raporu 32 MB sınırını aşıyor.");
        using var reader = XmlReader.Create(input, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaxReportBytes,
        });
        var root = XDocument.Load(reader).Root;
        if (root?.Name.LocalName != "Rsop" || root.Name.NamespaceName != "http://www.microsoft.com/GroupPolicy/Rsop")
            throw new InvalidDataException("Bir gpresult /x RSoP raporu seç.");
        var rows = new List<PolicyRow>();
        var scopes = new List<Scope>();
        foreach (var (tag, scope) in new[] { ("ComputerResults", Scope.Computer), ("UserResults", Scope.User) })
        {
            var section = Child(root, tag);
            if (section == null) continue;
            scopes.Add(scope);
            ReadSection(section, scope, rows);
        }
        if (scopes.Count == 0) throw new InvalidDataException("Raporda kullanıcı veya bilgisayar kapsamı bulunamadı.");
        return new PolicyReport(rows, scopes, "");
    }

    private static void ReadSection(XElement section, Scope scope, List<PolicyRow> rows)
    {
        var gpos = new Dictionary<Guid, string>();
        foreach (var gpo in section.Elements().Where(node => node.Name.LocalName == "GPO"))
        {
            var name = Value(gpo, "Name");
            if (name.Length == 0) continue;
            if (Identifier(gpo) is { } id) gpos.TryAdd(id, name);
            var state = (Value(gpo, "Enabled"), Value(gpo, "IsValid"), Value(gpo, "AccessDenied"), Value(gpo, "FilterAllowed")) switch
            {
                (_, _, "true", _) => "Erişim reddedildi",
                (_, _, _, "false") => "Filtreye takıldı",
                ("false", _, _, _) => "Devre dışı",
                (_, "false", _, _) => "Geçersiz",
                _ => "Raporda listelendi",
            };
            rows.Add(new PolicyRow(scope, RowKind.Gpo, "Grup İlkesi Nesnesi", name, state, name, gpo.ToString()));
        }

        foreach (var extensionData in section.Elements().Where(node => node.Name.LocalName == "ExtensionData"))
        {
            var extension = Child(extensionData, "Extension");
            if (extension == null) continue;
            var area = First(Value(extensionData, "Name"), Value(extension, "Name"), "Grup İlkesi ayarları");
            var pending = new Stack<XElement>(extension.Elements().Reverse());
            while (pending.TryPop(out var node))
            {
                if (node.Name.LocalName is "Name" or "GPO" or "WinningGPO") continue;
                var properties = Child(node, "Properties");
                var name = First(Value(node, "Name"), Value(node, "DisplayName"), Value(node, "KeyName"),
                    Value(node, "ValueName"), Attribute(node, "name"), Attribute(properties, "name"),
                    Attribute(properties, "key"));
                var fields = node.Elements()
                    .Select(SettingValue)
                    .Concat(new[] { Attribute(properties, "value"), Attribute(node, "value") })
                    .Where(value => value.Length != 0)
                    .Distinct()
                    .ToArray();
                var reference = Child(node, "WinningGPO") ?? Child(node, "GPO");
                var isSetting = node.Name.LocalName == "Policy"
                    || (name.Length > 0 && (fields.Length > 0 || reference != null || properties != null));
                if (isSetting)
                {
                    var source = First(Value(reference, "Name"), Value(reference, "DisplayName"), Value(node, "WinningGPOName"));
                    if (source.Length == 0)
                    {
                        var direct = reference is { HasElements: false } ? reference.Value.Trim() : "";
                        var id = Identifier(reference) ?? (Guid.TryParse(direct, out var guid) ? guid : (Guid?)null);
                        source = id is { } key ? gpos.GetValueOrDefault(key, "") : direct;
                    }
                    rows.Add(new PolicyRow(scope, RowKind.Setting, area, First(name, node.Name.LocalName),
                        fields.Length > 0 ? string.Join(" · ", fields) : "Ayrıntıya bak", source, node.ToString()));
                    continue;
                }
                foreach (var child in node.Elements().Reverse()) pending.Push(child);
            }
        }
    }

    private static string SettingValue(XElement field)
    {
        if (ValueElements.Contains(field.Name.LocalName)) return field.Value.Trim();
        if (field.Name.LocalName is not ("DropDownList" or "CheckBox" or "Numeric" or "Text" or "ListBox" or "MultiText"))
            return "";
        var label = Value(field, "Name");
        var value = First(Value(field, "Value"), Value(field, "State"), field.HasElements ? "" : field.Value.Trim());
        return label.Length > 0 && value.Length > 0 ? $"{label} = {value}" : value;
    }

    private static Guid? Identifier(XElement? element) =>
        element?.Descendants().Where(node => node.Name.LocalName is "Identifier" or "Id")
            .Select(node => Guid.TryParse(node.Value.Trim(), out var id) ? (Guid?)id : null)
            .FirstOrDefault(id => id.HasValue);

    private static XElement? Child(XElement? node, string name) => node?.Elements().FirstOrDefault(child => child.Name.LocalName == name);
    private static string Value(XElement? node, string name) => Child(node, name)?.Value.Trim() ?? "";
    private static string Attribute(XElement? node, string name) => node?.Attributes()
        .FirstOrDefault(attribute => attribute.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value.Trim() ?? "";
    private static string First(params string[] values) => values.FirstOrDefault(value => value.Length != 0) ?? "";

    public static async Task<PolicyReport> CaptureAsync()
    {
        var directory = Directory.CreateTempSubdirectory("BarracudaGpo-");
        var combined = Path.Combine(directory.FullName, "combined.xml");
        var user = Path.Combine(directory.FullName, "user.xml");
        var computer = Path.Combine(directory.FullName, "computer.xml");
        try
        {
            var both = await RunAsync(combined, null);
            if (both.ExitCode == 0) return Read(combined);
            var reports = new List<PolicyReport>();
            foreach (var (file, scope) in new[] { (user, "user"), (computer, "computer") })
            {
                var result = await RunAsync(file, scope);
                if (result.ExitCode == 0) reports.Add(Read(file));
            }
            if (reports.Count == 0)
                throw new InvalidOperationException($"gpresult rapor üretemedi. {both.Output}");
            var scopes = reports.SelectMany(report => report.Scopes).Distinct().ToArray();
            var note = scopes.Contains(Scope.Computer) ? "" : "Bilgisayar kapsamı alınamadı. Bu kapsam için yönetici yetkisiyle rapor alabilirsin.";
            return new PolicyReport(reports.SelectMany(report => report.Rows).ToArray(), scopes, note);
        }
        finally
        {
            foreach (var path in new[] { combined, user, computer })
                try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            try { directory.Delete(); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(string path, string? scope)
    {
        var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "gpresult.exe"))
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
        };
        if (scope != null)
        {
            start.ArgumentList.Add("/scope");
            start.ArgumentList.Add(scope);
        }
        start.ArgumentList.Add("/x");
        start.ArgumentList.Add(path);
        using var process = Process.Start(start) ?? throw new IOException("gpresult başlatılamadı.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill();
            await process.WaitForExitAsync();
            throw new TimeoutException("gpresult 45 saniye içinde tamamlanmadı.");
        }
        var text = First((await stderr).Trim(), (await stdout).Trim());
        return (process.ExitCode, text.Length > 500 ? text[..500] : text);
    }
}
