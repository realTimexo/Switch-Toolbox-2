using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;

namespace SwitchToolbox.CrossPlatform;

internal sealed class MainWindow : Window
{
    private readonly TreeView _tree = new();
    private readonly TextBlock _status = new();
    private readonly HashSet<string> _loadedDirectories = new(StringComparer.Ordinal);

    public MainWindow()
    {
        Title = "Switch Toolbox 2 — Cross-platform preview";
        Width = 1080;
        Height = 720;
        MinWidth = 760;
        MinHeight = 480;

        var layout = new DockPanel { Margin = new Thickness(16) };
        Content = layout;

        var title = new TextBlock
        {
            Text = "Switch Toolbox 2",
            FontSize = 24,
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
            Margin = new Thickness(0, 0, 0, 12)
        };
        DockPanel.SetDock(title, Dock.Top);
        layout.Children.Add(title);

        var footer = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 4,
            Margin = new Thickness(0, 12, 0, 0)
        };
        footer.Children.Add(new TextBlock
        {
            Text = "Portierung in Arbeit: Dateiformat-Editoren und Game-File-Verarbeitung sind in diesem ersten Meilenstein noch nicht angebunden.",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        });
        _status.Text = "Öffne einen Ordner oder eine Datei.";
        footer.Children.Add(_status);
        DockPanel.SetDock(footer, Dock.Bottom);
        layout.Children.Add(footer);

        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 0, 0, 12)
        };
        var openFolder = new Button { Content = "Ordner öffnen…" };
        openFolder.Click += async (_, _) => await ChooseFolderAsync();
        var openFile = new Button { Content = "Datei öffnen…" };
        openFile.Click += async (_, _) => await ChooseFileAsync();
        toolbar.Children.Add(openFolder);
        toolbar.Children.Add(openFile);
        DockPanel.SetDock(toolbar, Dock.Top);
        layout.Children.Add(toolbar);

        _tree.Items.Add(new TreeViewItem
        {
            Header = "Öffne einen Ordner, um Dateien und Unterordner anzuzeigen.",
            IsEnabled = false
        });
        layout.Children.Add(_tree);
    }

    private async Task ChooseFolderAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Ordner öffnen",
            AllowMultiple = false
        });
        var path = folders.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
            return;

        _loadedDirectories.Clear();
        _tree.Items.Clear();
        _tree.Items.Add(CreateDirectoryNode(path));
        _status.Text = path;
    }

    private async Task ChooseFileAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Datei öffnen",
            AllowMultiple = false
        });
        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
            return;

        _status.Text = $"Ausgewählt: {path} — Dateiformat-Editor folgt in einem späteren Portierungsschritt.";
    }

    private TreeViewItem CreateDirectoryNode(string path)
    {
        var item = new TreeViewItem
        {
            Header = Path.GetFileName(Path.TrimEndingDirectorySeparator(path)),
            Tag = path
        };
        item.Items.Add(new TreeViewItem { Header = " " });
        item.Expanded += (_, _) => PopulateDirectory(item, path);
        return item;
    }

    private void PopulateDirectory(TreeViewItem item, string path)
    {
        if (!_loadedDirectories.Add(path))
            return;

        item.Items.Clear();
        try
        {
            foreach (var childDirectory in Directory.EnumerateDirectories(path).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
                item.Items.Add(CreateDirectoryNode(childDirectory));

            foreach (var file in Directory.EnumerateFiles(path).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
                item.Items.Add(new TreeViewItem
                {
                    Header = Path.GetFileName(file),
                    Tag = file
                });

            if (item.Items.Count == 0)
                item.Items.Add(new TreeViewItem { Header = "(leer)", IsEnabled = false });
        }
        catch (UnauthorizedAccessException)
        {
            item.Items.Add(new TreeViewItem { Header = "(Zugriff verweigert)", IsEnabled = false });
        }
        catch (IOException)
        {
            item.Items.Add(new TreeViewItem { Header = "(Ordner konnte nicht gelesen werden)", IsEnabled = false });
        }
    }
}
