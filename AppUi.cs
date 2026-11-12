using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace Barracuda;

internal static class AppUi
{
    private sealed record Palette(string Name, int Base, int Primary, int Secondary, int Ink, int OnPrimary);

    private static readonly Palette[] Themes =
    [
        new("Obsidian", 0x111217, 0xEF233C, 0xDFE5EB, 0xDFE5EB, 0x08090B),
        new("Royal Blue", 0x21232E, 0x105BFF, 0xFE8500, 0xF1F4FA, 0xFFFFFF),
        new("Bubblegum", 0xF0F0F0, 0x105BFF, 0xFFC1CC, 0x202538, 0xFFFFFF),
        new("Aqua Jade", 0x171F44, 0x74FFED, 0xFE81D1, 0xEAF0FF, 0x171F44),
    ];

    public static DockPanel Layout(Window window, string title, string app, TextBlock status)
    {
        window.Title = $"Barracuda {title}";
        window.Width = 1130;
        window.Height = 740;
        window.MinWidth = 820;
        window.MinHeight = 510;
        window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        window.FontFamily = new FontFamily("Segoe UI");
        window.FontSize = 14;
        window.SetResourceReference(Control.BackgroundProperty, "BBase");
        window.SetResourceReference(Control.ForegroundProperty, "BInk");
        var resources = (ResourceDictionary)XamlReader.Parse(Styles);
        window.Resources.MergedDictionaries.Add(resources);

        var themeFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Barracuda Systems", app, "theme.txt");
        var selected = Themes[0];
        try
        {
            if (File.Exists(themeFile))
            {
                var saved = File.ReadAllText(themeFile).Trim();
                selected = Themes.FirstOrDefault(palette => palette.Name == saved) ?? selected;
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            status.Text = "Tema ayarı okunamadı. Obsidian kullanılıyor.";
        }
        Apply(window, selected);
        var root = new DockPanel { Margin = new Thickness(18, 14, 18, 12) };
        var heading = new DockPanel { Margin = new Thickness(0, 0, 0, 13) };
        var picker = new ComboBox { ItemsSource = Themes.Select(palette => palette.Name), SelectedItem = selected.Name, Width = 126 };
        System.Windows.Automation.AutomationProperties.SetName(picker, "Görünüm teması");
        picker.SelectionChanged += (_, _) =>
        {
            if (picker.SelectedItem is not string name) return;
            Apply(window, Themes.First(palette => palette.Name == name));
            string? temporary = null;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(themeFile)!);
                temporary = themeFile + "." + Guid.NewGuid().ToString("N");
                File.WriteAllText(temporary, name);
                File.Move(temporary, themeFile, true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                status.Text = "Tema bu oturumda değişti. Seçim dosyaya kaydedilemedi.";
                if (temporary != null)
                    try { File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        };
        var appearance = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var themeLabel = Text("Tema", 13, resource: "BMuted");
        themeLabel.Margin = new Thickness(0, 0, 7, 0);
        themeLabel.VerticalAlignment = VerticalAlignment.Center;
        appearance.Children.Add(themeLabel);
        appearance.Children.Add(picker);
        DockPanel.SetDock(appearance, Dock.Right);
        heading.Children.Add(appearance);
        var brand = Text("Barracuda", 22, true, "BAccentText");
        brand.Margin = new Thickness(0, 0, 12, 0);
        DockPanel.SetDock(brand, Dock.Left);
        heading.Children.Add(brand);
        heading.Children.Add(Text(title, 20, true));
        Top(root, heading);
        status.Margin = new Thickness(0, 9, 0, 0);
        DockPanel.SetDock(status, Dock.Bottom);
        root.Children.Add(status);
        window.Content = root;
        return root;
    }

    public static TextBlock Text(string text, double size = 14, bool bold = false, string resource = "BInk")
    {
        var block = new TextBlock { Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap };
        if (bold) block.FontWeight = FontWeights.SemiBold;
        block.SetResourceReference(TextBlock.ForegroundProperty, resource);
        return block;
    }

    public static Button Button(string text, RoutedEventHandler click, bool primary = false)
    {
        var button = new Button { Content = text, Margin = new Thickness(0, 0, 7, 5) };
        if (primary)
        {
            button.SetResourceReference(Control.BackgroundProperty, "BAccent");
            button.SetResourceReference(Control.ForegroundProperty, "BOnAccent");
        }
        button.Click += click;
        return button;
    }

    public static Border Card(UIElement content)
    {
        var card = new Border
        {
            Child = content, Padding = new Thickness(13), CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
        };
        card.SetResourceReference(Border.BackgroundProperty, "BPanel");
        card.SetResourceReference(Border.BorderBrushProperty, "BLine");
        return card;
    }

    public static void Top(DockPanel parent, UIElement child)
    {
        DockPanel.SetDock(child, Dock.Top);
        parent.Children.Add(child);
    }

    public static Grid Columns(UIElement left, UIElement right)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.13, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(11) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.Children.Add(left);
        Grid.SetColumn(right, 2);
        grid.Children.Add(right);
        return grid;
    }

    private static void Apply(Window window, Palette theme)
    {
        var light = theme.Name == "Bubblegum";
        var panel = light ? 0xFFFFFF : Mix(theme.Base, theme.Ink, 7);
        var colors = new Dictionary<string, int>
        {
            ["BBase"] = theme.Base, ["BPanel"] = panel, ["BField"] = Mix(panel, theme.Base, 35),
            ["BInk"] = theme.Ink, ["BMuted"] = Mix(theme.Base, theme.Ink, 74),
            ["BLine"] = Mix(theme.Base, theme.Ink, 23), ["BAccent"] = theme.Primary,
            ["BOnAccent"] = theme.OnPrimary, ["BSelected"] = Mix(panel, theme.Primary, 19),
            ["BAccentText"] = Mix(theme.Primary, theme.Ink, theme.Name == "Royal Blue" ? 40 : 15),
            ["BSecondary"] = light ? Mix(theme.Secondary, theme.Ink, 58) : theme.Secondary,
        };
        foreach (var (name, rgb) in colors)
        {
            var brush = new SolidColorBrush(Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb));
            brush.Freeze();
            window.Resources[name] = brush;
        }
    }

    private static int Mix(int a, int b, int percent)
    {
        int Channel(int shift) => (((a >> shift) & 255) * (100 - percent) + ((b >> shift) & 255) * percent) / 100;
        return (Channel(16) << 16) | (Channel(8) << 8) | Channel(0);
    }

    private const string Styles = """
        <ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
          <Style TargetType="Button">
            <Setter Property="Background" Value="{DynamicResource BPanel}"/>
            <Setter Property="Foreground" Value="{DynamicResource BInk}"/>
            <Setter Property="BorderBrush" Value="{DynamicResource BLine}"/>
            <Setter Property="BorderThickness" Value="1"/>
            <Setter Property="Padding" Value="12,7"/>
            <Setter Property="Template">
              <Setter.Value>
                <ControlTemplate TargetType="Button">
                  <Border x:Name="Surface" Background="{TemplateBinding Background}"
                          BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}"
                          Padding="{TemplateBinding Padding}" CornerRadius="6">
                    <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                  </Border>
                  <ControlTemplate.Triggers>
                    <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Surface" Property="Opacity" Value="0.82"/></Trigger>
                    <Trigger Property="IsEnabled" Value="False"><Setter TargetName="Surface" Property="Opacity" Value="0.48"/></Trigger>
                  </ControlTemplate.Triggers>
                </ControlTemplate>
              </Setter.Value>
            </Setter>
          </Style>
          <Style TargetType="TextBox">
            <Setter Property="Background" Value="{DynamicResource BField}"/>
            <Setter Property="Foreground" Value="{DynamicResource BInk}"/>
            <Setter Property="BorderBrush" Value="{DynamicResource BLine}"/>
            <Setter Property="Padding" Value="7"/>
          </Style>
          <Style TargetType="ListBox">
            <Setter Property="Background" Value="{DynamicResource BPanel}"/>
            <Setter Property="Foreground" Value="{DynamicResource BInk}"/>
            <Setter Property="BorderThickness" Value="0"/>
            <Setter Property="ScrollViewer.HorizontalScrollBarVisibility" Value="Disabled"/>
          </Style>
          <Style TargetType="ListBoxItem">
            <Setter Property="Foreground" Value="{DynamicResource BInk}"/>
            <Setter Property="Padding" Value="7,5"/>
            <Setter Property="HorizontalContentAlignment" Value="Stretch"/>
            <Setter Property="Template">
              <Setter.Value>
                <ControlTemplate TargetType="ListBoxItem">
                  <Border x:Name="Row" Padding="{TemplateBinding Padding}" CornerRadius="4">
                    <ContentPresenter/>
                  </Border>
                  <ControlTemplate.Triggers>
                    <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Row" Property="Background" Value="{DynamicResource BField}"/></Trigger>
                    <Trigger Property="IsSelected" Value="True"><Setter TargetName="Row" Property="Background" Value="{DynamicResource BSelected}"/></Trigger>
                  </ControlTemplate.Triggers>
                </ControlTemplate>
              </Setter.Value>
            </Setter>
          </Style>
        </ResourceDictionary>
        """;
}
