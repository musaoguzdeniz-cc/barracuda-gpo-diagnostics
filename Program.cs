using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace Barracuda;

internal static class Program
{
    [STAThread]
    public static int Main() => new Application().Run(new PolicyWindow());
}

internal sealed class PolicyWindow : Window
{
    private static readonly (string Name, string[] Terms)[] Topics =
    [
        ("Tüm konular", []),
        ("Microsoft Store", ["microsoft store", "windowsstore", "store application", "mağaza"]),
        ("Windows Update", ["windows update", "windowsupdate", "windows güncelle", "windows güncellemeleri"]),
        ("Proxy", ["proxy", "internet settings", "internet ayarları"]),
    ];

    private readonly TextBlock status = AppUi.Text("Canlı sonucu oku veya gpresult /x ile oluşturulmuş bir XML aç.", 13, resource: "BMuted");
    private readonly TextBlock summary = AppUi.Text("Rapor yüklenmedi", 16, true);
    private readonly TextBlock heading = AppUi.Text("Ayar ayrıntısı", 20, true);
    private readonly TextBlock metadata = AppUi.Text("", 13, resource: "BSecondary");
    private readonly TextBlock origin = AppUi.Text("", 14, true);
    private readonly TextBox xml = new()
    {
        IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        FontFamily = new System.Windows.Media.FontFamily("Cascadia Mono"), FontSize = 12,
    };
    private readonly ListBox rows = new() { DisplayMemberPath = nameof(PolicyRow.Display) };
    private readonly TextBox search = new() { MinWidth = 230 };
    private readonly ComboBox scope = new() { ItemsSource = new[] { "Tüm kapsamlar", "Bilgisayar", "Kullanıcı" }, SelectedIndex = 0, MinWidth = 120 };
    private readonly ComboBox type = new() { ItemsSource = new[] { "Ayarlar", "GPO’lar" }, SelectedIndex = 0, MinWidth = 85 };
    private readonly ComboBox topic = new() { ItemsSource = Topics.Select(topic => topic.Name), SelectedIndex = 0, MinWidth = 135 };
    private readonly Button live;
    private readonly Button open;
    private PolicyReport? report;

    public PolicyWindow()
    {
        var root = AppUi.Layout(this, "Grup İlkesi Teşhis Aracı", "Group Policy Diagnostics", status);
        var actions = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
        live = AppUi.Button("Canlı sonucu oku", async (_, _) => await LoadAsync(PolicyReport.CaptureAsync, "Canlı gpresult"), true);
        open = AppUi.Button("XML rapor aç", async (_, _) =>
        {
            var dialog = new OpenFileDialog { Filter = "RSoP XML raporu (*.xml)|*.xml", CheckFileExists = true };
            if (dialog.ShowDialog(this) == true)
                await LoadAsync(() => Task.Run(() => PolicyReport.Read(dialog.FileName)), Path.GetFileName(dialog.FileName));
        });
        actions.Children.Add(live);
        actions.Children.Add(open);
        AppUi.Top(root, actions);

        var filters = new WrapPanel { Margin = new Thickness(0, 0, 0, 9) };
        var filterNames = new[] { "Ayar, değer veya GPO ara", "Kapsam", "Görünüm", "Konu" };
        var controls = new FrameworkElement[] { search, scope, type, topic };
        for (var i = 0; i < controls.Length; i++)
        {
            var column = new StackPanel { Margin = new Thickness(0, 0, 9, 5) };
            column.Children.Add(AppUi.Text(filterNames[i], 12, resource: "BMuted"));
            System.Windows.Automation.AutomationProperties.SetName(controls[i], filterNames[i]);
            column.Children.Add(controls[i]);
            filters.Children.Add(column);
        }
        AppUi.Top(root, filters);

        var results = new DockPanel();
        summary.Margin = new Thickness(0, 0, 0, 7);
        AppUi.Top(results, summary);
        results.Children.Add(rows);
        var detail = new DockPanel();
        AppUi.Top(detail, heading);
        metadata.Margin = new Thickness(0, 7, 0, 5);
        AppUi.Top(detail, metadata);
        origin.Margin = new Thickness(0, 0, 0, 12);
        AppUi.Top(detail, origin);
        detail.Children.Add(xml);
        root.Children.Add(AppUi.Columns(AppUi.Card(results), AppUi.Card(detail)));

        search.TextChanged += (_, _) => UpdateResults();
        scope.SelectionChanged += (_, _) => UpdateResults();
        type.SelectionChanged += (_, _) => { topic.IsEnabled = type.SelectedIndex == 0; UpdateResults(); };
        topic.SelectionChanged += (_, _) => UpdateResults();
        rows.SelectionChanged += (_, _) => UpdateDetail();
    }

    private async Task LoadAsync(Func<Task<PolicyReport>> work, string source)
    {
        live.IsEnabled = open.IsEnabled = false;
        status.Text = "Rapor okunuyor…";
        try
        {
            report = await work();
            scope.SelectedIndex = 0;
            type.SelectedIndex = report.Rows.Any(row => row.Kind == RowKind.Setting) ? 0 : 1;
            topic.SelectedIndex = 0;
            search.Clear();
            UpdateResults();
            var scopes = string.Join(", ", report.Scopes.Select(item => item == Scope.Computer ? "Bilgisayar" : "Kullanıcı"));
            var note = report.Note.Length > 0 ? $"  ·  {report.Note}" : "";
            status.Text = $"{source}  ·  {scopes}{note}";
        }
        catch (Exception error)
        {
            status.Text = $"Rapor açılamadı. {error.Message}";
        }
        finally { live.IsEnabled = open.IsEnabled = true; }
    }

    private void UpdateResults()
    {
        if (report == null) return;
        var selected = rows.SelectedItem;
        var kind = type.SelectedIndex == 1 ? RowKind.Gpo : RowKind.Setting;
        var target = scope.SelectedIndex switch { 1 => (Scope?)Scope.Computer, 2 => Scope.User, _ => null };
        var terms = kind == RowKind.Setting && topic.SelectedIndex >= 0 ? Topics[topic.SelectedIndex].Terms : [];
        var text = search.Text.Trim();
        var matches = report.Rows.Where(row => row.Kind == kind
            && (target == null || target == row.Scope)
            && (text.Length == 0 || row.Contains(text))
            && (terms.Length == 0 || terms.Any(row.Contains))).ToArray();
        rows.ItemsSource = matches;
        summary.Text = matches.Length == 0 ? "Bu görünümde kayıt yok" : $"{matches.Length} kayıt";
        rows.SelectedItem = matches.Contains(selected) ? selected : matches.FirstOrDefault();
        UpdateDetail();
    }

    private void UpdateDetail()
    {
        if (rows.SelectedItem is not PolicyRow row)
        {
            heading.Text = "Ayar ayrıntısı";
            metadata.Text = origin.Text = xml.Text = "";
            return;
        }
        heading.Text = row.Name;
        metadata.Text = $"{(row.Scope == Scope.Computer ? "Bilgisayar" : "Kullanıcı")}  ·  {row.Area}";
        origin.Text = row.Kind == RowKind.Setting
            ? $"{row.Value}\nKaynak GPO · {(row.Source.Length > 0 ? row.Source : "Rapor belirtmiyor")}"
            : row.Value;
        xml.Text = row.Xml;
        xml.ScrollToHome();
    }
}
