using Microsoft.Win32;
using SPTextureMerger.Core;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace SPTextureMerger;

public partial class MainWindow : Window
{
    private readonly List<TextureColumnDefinition> _columns = [];
    private readonly List<TextureSetRowDefinition> _rows = [];
    private List<ValidationMessage> _messages = [];
    private string? _currentProjectPath;
    private string? _outputDirectory;
    private string _outputFormat = "png";
    private bool _isRefreshing;
    private bool _isMerging;

    public MainWindow()
    {
        InitializeComponent();
        OutputBaseNameTextBox.Text = "TextureSet";
        AddDefaultContent();
        RefreshUi();
    }

    private string OutputBaseName
    {
        get => string.IsNullOrWhiteSpace(OutputBaseNameTextBox.Text) ? "TextureSet" : OutputBaseNameTextBox.Text.Trim();
        set => OutputBaseNameTextBox.Text = value;
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        GlassWindowHelper.Apply(this);
    }

    private void AddDefaultContent()
    {
        var column = new TextureColumnDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Albedo",
            Behavior = MergeBehavior.RgbaCopy
        };
        _columns.Add(column);
        _rows.Add(new TextureSetRowDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Texture Set 1",
            TexturePaths = { [column.Id] = null }
        });
    }

    private void AddRow_Click(object sender, RoutedEventArgs e)
    {
        var row = new TextureSetRowDefinition
        {
            Id = Guid.NewGuid(),
            Name = $"Texture Set {_rows.Count + 1}"
        };
        foreach (var column in _columns)
        {
            row.TexturePaths[column.Id] = null;
        }

        _rows.Add(row);
        RefreshUi();
    }

    private void AddColumn_Click(object sender, RoutedEventArgs e)
    {
        var name = _columns.Count switch
        {
            0 => "Albedo",
            1 => "Normal",
            _ => $"Map{_columns.Count + 1}"
        };

        var column = new TextureColumnDefinition
        {
            Id = Guid.NewGuid(),
            Name = name,
            Behavior = name.Equals("Normal", StringComparison.OrdinalIgnoreCase)
                ? MergeBehavior.NormalReplaceNormalize
                : MergeBehavior.RgbaCopy
        };

        _columns.Add(column);
        foreach (var row in _rows)
        {
            row.TexturePaths[column.Id] = null;
        }

        RefreshUi();
    }

    private void RefreshUi()
    {
        if (TextureGrid is null)
        {
            return;
        }

        if (_isRefreshing)
        {
            return;
        }

        _isRefreshing = true;
        try
        {
            _messages = [];
            BuildGrid();
            UpdateStatus();
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private void BuildGrid()
    {
        TextureGrid.Children.Clear();
        TextureGrid.RowDefinitions.Clear();
        TextureGrid.ColumnDefinitions.Clear();

        EmptyStateTextBlock.Visibility = _rows.Count == 0 || _columns.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        TextureGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(230) });
        TextureGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
        foreach (var _ in _columns)
        {
            TextureGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(290) });
        }

        TextureGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto, MinHeight = 164 });
        AddHeaderCell("Texture Set", 0, 0);
        AddHeaderCell("Mask Guide", 0, 1);
        for (var i = 0; i < _columns.Count; i++)
        {
            AddColumnHeader(_columns[i], i + 2);
        }

        for (var rowIndex = 0; rowIndex < _rows.Count; rowIndex++)
        {
            var row = _rows[rowIndex];
            TextureGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(214) });
            AddRowHeader(row, rowIndex + 1, rowIndex);
            AddSlot(rowIndex + 1, 1, row, null, "Mask", row.MaskPath);
            for (var columnIndex = 0; columnIndex < _columns.Count; columnIndex++)
            {
                var column = _columns[columnIndex];
                row.TexturePaths.TryGetValue(column.Id, out var path);
                AddSlot(rowIndex + 1, columnIndex + 2, row, column, column.Name, path);
            }
        }
    }

    private void AddHeaderCell(string title, int row, int column)
    {
        var border = CreateCellBorder(false);
        border.Child = new TextBlock
        {
            Text = title,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("MutedTextBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        Grid.SetRow(border, row);
        Grid.SetColumn(border, column);
        TextureGrid.Children.Add(border);
    }

    private void AddColumnHeader(TextureColumnDefinition column, int gridColumn)
    {
        var border = CreateCellBorder(HasMessage(null, column.Id, ValidationSeverity.Error));
        var stack = new StackPanel { Margin = new Thickness(10, 8, 10, 8) };

        var name = new TextBox
        {
            Text = column.Name,
            ToolTip = "Output file suffix, for example Albedo or Normal."
        };
        name.LostFocus += (_, _) =>
        {
            column.Name = name.Text.Trim();
            RefreshUi();
        };
        name.KeyDown += (_, args) =>
        {
            if (args.Key == Key.Enter)
            {
                column.Name = name.Text.Trim();
                Keyboard.ClearFocus();
                RefreshUi();
            }
        };

        var behavior = new ComboBox { Margin = new Thickness(0, 8, 0, 0) };
        foreach (var value in Enum.GetValues<MergeBehavior>())
        {
            var item = new ComboBoxItem
            {
                Content = DisplayBehavior(value),
                Tag = value,
                IsSelected = value == column.Behavior
            };
            behavior.Items.Add(item);
        }

        behavior.SelectionChanged += (_, _) =>
        {
            if (_isRefreshing || behavior.SelectedItem is not ComboBoxItem item || item.Tag is not MergeBehavior selected)
            {
                return;
            }

            column.Behavior = selected;
            RefreshUi();
        };

        var remove = new Button
        {
            Content = "Delete Column",
            Style = (Style)FindResource("InlineButton"),
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 6, 0, 0)
        };
        remove.Click += (_, _) =>
        {
            _columns.Remove(column);
            foreach (var row in _rows)
            {
                row.TexturePaths.Remove(column.Id);
            }

            RefreshUi();
        };

        stack.Children.Add(name);
        stack.Children.Add(behavior);
        stack.Children.Add(remove);
        border.Child = stack;
        Grid.SetRow(border, 0);
        Grid.SetColumn(border, gridColumn);
        TextureGrid.Children.Add(border);
    }

    private void AddRowHeader(TextureSetRowDefinition row, int gridRow, int rowIndex)
    {
        var border = CreateCellBorder(HasMessage(row.Id, null, ValidationSeverity.Error));
        var stack = new StackPanel { Margin = new Thickness(12) };
        var name = new TextBox { Text = row.Name };
        name.LostFocus += (_, _) =>
        {
            row.Name = name.Text.Trim();
            RefreshUi();
        };
        name.KeyDown += (_, args) =>
        {
            if (args.Key == Key.Enter)
            {
                row.Name = name.Text.Trim();
                Keyboard.ClearFocus();
                RefreshUi();
            }
        };

        var buttons = new WrapPanel();
        var up = SmallButton("↑");
        up.IsEnabled = rowIndex > 0;
        up.ToolTip = CreateToolTip("Move this row up. Rows lower in the list override rows above when masks overlap.");
        ToolTipService.SetShowOnDisabled(up, true);
        up.Click += (_, _) =>
        {
            MoveRow(rowIndex, rowIndex - 1);
        };
        var down = SmallButton("↓");
        down.IsEnabled = rowIndex < _rows.Count - 1;
        down.ToolTip = CreateToolTip("Move this row down. Rows lower in the list override rows above when masks overlap.");
        ToolTipService.SetShowOnDisabled(down, true);
        down.Click += (_, _) =>
        {
            MoveRow(rowIndex, rowIndex + 1);
        };
        var delete = SmallButton("Delete");
        delete.Click += (_, _) =>
        {
            _rows.Remove(row);
            RefreshUi();
        };

        buttons.Children.Add(up);
        buttons.Children.Add(down);
        buttons.Children.Add(delete);
        stack.Children.Add(name);
        stack.Children.Add(new Border { Height = 10, Opacity = 0 });
        stack.Children.Add(buttons);
        border.Child = stack;
        Grid.SetRow(border, gridRow);
        Grid.SetColumn(border, 0);
        TextureGrid.Children.Add(border);
    }

    private void AddSlot(
        int gridRow,
        int gridColumn,
        TextureSetRowDefinition row,
        TextureColumnDefinition? column,
        string title,
        string? path)
    {
        var hasError = HasMessage(row.Id, column?.Id, ValidationSeverity.Error);
        var border = CreateCellBorder(hasError);
        border.AllowDrop = true;
        border.PreviewDragOver += (_, args) =>
        {
            if (args.Data.GetDataPresent(DataFormats.FileDrop))
            {
                args.Effects = DragDropEffects.Copy;
                args.Handled = true;
            }
        };
        border.Drop += (_, args) =>
        {
            if (!args.Data.GetDataPresent(DataFormats.FileDrop))
            {
                return;
            }

            var files = (string[])args.Data.GetData(DataFormats.FileDrop)!;
            var file = files.FirstOrDefault(ImageIo.IsSupportedInput);
            if (file is null)
            {
                ShowToast("Unsupported File", "Choose a PNG, TIFF, BMP, or JPG image.", ToastKind.Warning);
                return;
            }

            SetPath(row, column, file);
        };

        var stack = new StackPanel { Margin = new Thickness(8) };
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        var preview = new Border
        {
            Height = 96,
            Margin = new Thickness(0, 8, 0, 6),
            Background = new SolidColorBrush(Color.FromArgb(42, 255, 255, 255)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(92, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            ClipToBounds = true
        };

        var image = new Image
        {
            Stretch = Stretch.Uniform,
            Source = TryCreateThumbnail(path)
        };
        preview.Child = image;
        stack.Children.Add(preview);

        stack.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(path) ? "Drop an image or browse" : Path.GetFileName(path),
            Foreground = (Brush)FindResource(string.IsNullOrWhiteSpace(path) ? "MutedTextBrush" : "TextBrush"),
            FontSize = 12,
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        var buttons = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        var browse = SmallButton("Browse");
        browse.Click += (_, _) => BrowseImage(row, column);
        var clear = SmallButton("Clear");
        clear.Click += (_, _) => SetPath(row, column, null);
        buttons.Children.Add(browse);
        buttons.Children.Add(clear);

        var previewButton = SmallButton("Preview");
        previewButton.IsEnabled = !string.IsNullOrWhiteSpace(path) && File.Exists(path);
        previewButton.Click += (_, _) =>
        {
            if (column is null)
            {
                ShowMaskPreview(row);
            }
            else
            {
                ShowTexturePreview(path, $"{row.Name} - {column.Name}");
            }
        };
        buttons.Children.Add(previewButton);

        stack.Children.Add(buttons);
        border.Child = stack;
        Grid.SetRow(border, gridRow);
        Grid.SetColumn(border, gridColumn);
        TextureGrid.Children.Add(border);
    }

    private Border CreateCellBorder(bool isError)
    {
        return new Border
        {
            Margin = new Thickness(4),
            Background = new SolidColorBrush(Color.FromArgb(46, 255, 255, 255)),
            BorderBrush = isError
                ? (Brush)FindResource("ErrorBrush")
                : new SolidColorBrush(Color.FromArgb(92, 255, 255, 255)),
            BorderThickness = new Thickness(isError ? 2 : 1),
            CornerRadius = new CornerRadius(16)
        };
    }

    private Button SmallButton(string content)
    {
        return new Button
        {
            Content = content,
            Style = (Style)FindResource("InlineButton")
        };
    }

    private static ToolTip CreateToolTip(string text)
    {
        return new ToolTip
        {
            Content = text
        };
    }

    private void BrowseImage(TextureSetRowDefinition row, TextureColumnDefinition? column)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Images|*.png;*.tif;*.tiff;*.bmp;*.jpg;*.jpeg|All files|*.*",
            Multiselect = false,
            Title = column is null ? "Select Mask Guide" : $"Select {column.Name} Texture"
        };

        if (dialog.ShowDialog(this) == true)
        {
            SetPath(row, column, dialog.FileName);
        }
    }

    private BitmapSource? TryCreateThumbnail(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        var fullPath = path;
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 144;
            image.UriSource = new Uri(fullPath, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    private void SetPath(TextureSetRowDefinition row, TextureColumnDefinition? column, string? path)
    {
        if (column is null)
        {
            row.MaskPath = path;
        }
        else
        {
            row.TexturePaths[column.Id] = path;
        }

        RefreshUi();
    }

    private void MoveRow(int sourceIndex, int targetIndex)
    {
        var row = _rows[sourceIndex];
        _rows.RemoveAt(sourceIndex);
        _rows.Insert(targetIndex, row);
        RefreshUi();
    }

    private void ShowMaskPreview(TextureSetRowDefinition row)
    {
        if (string.IsNullOrWhiteSpace(row.MaskPath) || !File.Exists(row.MaskPath))
        {
            return;
        }

        var maskPath = row.MaskPath;
        try
        {
            var mask = MaskProcessor.Build(ImageIo.LoadRgba(maskPath));
            var preview = MaskProcessor.CreatePreview(mask);
            ShowPreviewWindow($"Mask Preview - {row.Name}", ImageIo.CreateBitmapSource(preview));
        }
        catch (Exception ex)
        {
            ShowToast("Preview Failed", ex.Message, ToastKind.Error);
        }
    }

    private void ShowTexturePreview(string? path, string title)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        try
        {
            ShowPreviewWindow(title, ImageIo.CreateBitmapSource(ImageIo.LoadRgba(path)));
        }
        catch (Exception ex)
        {
            ShowToast("Preview Failed", ex.Message, ToastKind.Error);
        }
    }

    private void ShowPreviewWindow(string title, ImageSource source)
    {
        var window = new Window
        {
            Owner = this,
            Title = title,
            Width = 680,
            Height = 620,
            MinWidth = 420,
            MinHeight = 360,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = (Brush)FindResource("AppBackgroundBrush"),
            Content = new Border
            {
                Margin = new Thickness(18),
                Padding = new Thickness(16),
                Background = (Brush)FindResource("PanelBrush"),
                BorderBrush = (Brush)FindResource("StrokeBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(22),
                Child = new Image
                {
                    Source = source,
                    Stretch = Stretch.Uniform
                }
            }
        };
        window.ShowDialog();
    }

    private bool HasMessage(Guid? rowId, Guid? columnId, ValidationSeverity severity)
    {
        return _messages.Any(message =>
            message.Severity == severity &&
            (rowId.HasValue ? message.RowId == rowId : message.RowId is null) &&
            (columnId.HasValue ? message.ColumnId == columnId : message.ColumnId is null) &&
            (rowId.HasValue || columnId.HasValue));
    }

    private void UpdateStatus()
    {
        OutputDirectoryTextBlock.Text = string.IsNullOrWhiteSpace(_outputDirectory)
            ? "No output folder selected"
            : _outputDirectory;

        var errorCount = _messages.Count(message => message.Severity == ValidationSeverity.Error);
        MergeButton.IsEnabled = !_isMerging;

        if (_isMerging)
        {
            StatusTextBlock.Foreground = (Brush)FindResource("MutedTextBrush");
            StatusTextBlock.Text = "Merging...";
        }
        else if (errorCount > 0)
        {
            StatusTextBlock.Foreground = (Brush)FindResource("MutedTextBrush");
            StatusTextBlock.Text = "Configuration needs attention. Click Merge for details.";
        }
        else
        {
            StatusTextBlock.Foreground = (Brush)FindResource("MutedTextBrush");
            StatusTextBlock.Text = "Ready.";
        }
    }

    private MergeProject ToProject()
    {
        return new MergeProject
        {
            OutputBaseName = OutputBaseName,
            OutputDirectory = _outputDirectory,
            OutputFormat = _outputFormat,
            Columns = _columns
                .Select(column => new TextureColumnDefinition
                {
                    Id = column.Id,
                    Name = column.Name,
                    Behavior = column.Behavior
                })
                .ToList(),
            Rows = _rows
                .Select(row => new TextureSetRowDefinition
                {
                    Id = row.Id,
                    Name = row.Name,
                    MaskPath = row.MaskPath,
                    TexturePaths = _columns.ToDictionary(
                        column => column.Id,
                        column =>
                        {
                            row.TexturePaths.TryGetValue(column.Id, out var path);
                            return path;
                        })
                })
                .ToList()
        };
    }

    private void LoadProject(MergeProject project)
    {
        _columns.Clear();
        _columns.AddRange(project.Columns);
        _rows.Clear();
        _rows.AddRange(project.Rows);
        _outputDirectory = project.OutputDirectory;
        _outputFormat = string.Equals(project.OutputFormat, "tiff", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(project.OutputFormat, "tif", StringComparison.OrdinalIgnoreCase)
            ? "tiff"
            : "png";
        OutputBaseName = project.OutputBaseName;
        SetOutputFormatSelection();

        foreach (var row in _rows)
        {
            foreach (var column in _columns)
            {
                row.TexturePaths.TryAdd(column.Id, null);
            }
        }

        RefreshUi();
    }

    private async void Merge_Click(object sender, RoutedEventArgs e)
    {
        var project = ToProject();
        var validation = MergeEngine.Validate(project);
        _messages = validation.Messages;
        BuildGrid();
        UpdateStatus();

        if (!validation.CanMerge)
        {
            var errors = validation.Messages
                .Where(message => message.Severity == ValidationSeverity.Error)
                .Select(message => message.Message)
                .ToArray();
            ShowToast("Cannot Merge", $"{errors.Length} error(s). {errors.FirstOrDefault()}", ToastKind.Error, 7000);
            return;
        }

        var existingOutputs = MergeEngine.GetPlannedOutputPaths(project)
            .Where(File.Exists)
            .ToArray();
        if (existingOutputs.Length > 0)
        {
            ShowToast("Overwriting Output", $"{existingOutputs.Length} existing file(s) will be overwritten.", ToastKind.Warning, 4500);
        }

        _isMerging = true;
        MergeButton.IsEnabled = false;
        MergeProgressBar.Visibility = Visibility.Visible;
        MergeProgressBar.IsIndeterminate = true;
        StatusTextBlock.Foreground = (Brush)FindResource("MutedTextBrush");
        StatusTextBlock.Text = "Merging...";
        ShowToast("Merging", "Generating output textures.", ToastKind.Info, 2500);

        try
        {
            var mergeResult = await Task.Run(() => MergeEngine.Merge(project, new MergeOptions { Overwrite = true, OutputFormat = project.OutputFormat }));
            var warningSuffix = mergeResult.Warnings.Count > 0 ? $", {mergeResult.Warnings.Count} warning(s)" : string.Empty;
            ShowToast("Merge Complete", $"Wrote {mergeResult.OutputPaths.Count} file(s){warningSuffix}.", ToastKind.Success, 6500);
            foreach (var warning in mergeResult.Warnings.Take(2))
            {
                ShowToast("Merge Warning", warning, ToastKind.Warning, 7000);
            }

            StatusTextBlock.Text = $"Merge complete: {mergeResult.OutputPaths.Count} file(s).";
        }
        catch (Exception ex)
        {
            ShowToast("Merge Failed", ex.Message, ToastKind.Error, 8000);
            StatusTextBlock.Foreground = (Brush)FindResource("ErrorBrush");
            StatusTextBlock.Text = ex.Message;
        }
        finally
        {
            _isMerging = false;
            MergeProgressBar.IsIndeterminate = false;
            MergeProgressBar.Visibility = Visibility.Collapsed;
            RefreshUi();
        }
    }

    private void ChooseOutputDirectory_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Output Folder",
            InitialDirectory = Directory.Exists(_outputDirectory) ? _outputDirectory : Environment.CurrentDirectory
        };

        if (dialog.ShowDialog(this) == true)
        {
            _outputDirectory = dialog.FolderName;
            RefreshUi();
        }
    }

    private void OpenProject_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "SPTextureMerger Project|*.sptm.json;*.json|All files|*.*",
            Title = "Open Merge Config"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            var project = ProjectFileService.Load(dialog.FileName);
            _currentProjectPath = dialog.FileName;
            LoadProject(project);
        }
        catch (Exception ex)
        {
            ShowToast("Open Failed", ex.Message, ToastKind.Error);
        }
    }

    private void SaveProject_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_currentProjectPath))
        {
            SaveProjectAs();
            return;
        }

        SaveProject(_currentProjectPath);
    }

    private void SaveProjectAs()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "SPTextureMerger Project|*.sptm.json|JSON|*.json",
            FileName = $"{FileNameSanitizer.ForFileName(OutputBaseName, "TextureSet")}.sptm.json",
            Title = "Save Merge Config"
        };

        if (dialog.ShowDialog(this) == true)
        {
            _currentProjectPath = dialog.FileName;
            SaveProject(dialog.FileName);
        }
    }

    private void SaveProject(string path)
    {
        try
        {
            ProjectFileService.Save(path, ToProject());
            StatusTextBlock.Foreground = (Brush)FindResource("MutedTextBrush");
            StatusTextBlock.Text = $"Config saved: {path}";
            ShowToast("Config Saved", path, ToastKind.Success);
        }
        catch (Exception ex)
        {
            ShowToast("Save Failed", ex.Message, ToastKind.Error);
        }
    }

    private void OutputBaseName_TextChanged(object sender, TextChangedEventArgs e)
    {
        // Keep typing responsive. The output name is read when merging or saving.
        if (StatusTextBlock is not null && _messages.Count > 0)
        {
            _messages = [];
            UpdateStatus();
        }
    }

    private void OutputFormat_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isRefreshing ||
            TextureGrid is null ||
            OutputFormatComboBox.SelectedItem is not ComboBoxItem item ||
            item.Tag is not string format)
        {
            return;
        }

        _outputFormat = format;
        UpdateStatus();
    }

    private void SetOutputFormatSelection()
    {
        foreach (var item in OutputFormatComboBox.Items.OfType<ComboBoxItem>())
        {
            item.IsSelected = string.Equals((string?)item.Tag, _outputFormat, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string DisplayBehavior(MergeBehavior behavior)
    {
        return behavior switch
        {
            MergeBehavior.RgbaCopy => "RGBA Copy",
            MergeBehavior.NormalReplaceNormalize => "Normal Replace Normalize",
            MergeBehavior.DataCopy => "Data Copy",
            _ => behavior.ToString()
        };
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximized();
            return;
        }

        DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        ToggleMaximized();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ToggleMaximized()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private async void ShowToast(string title, string message, ToastKind kind, int milliseconds = 4500)
    {
        if (ToastHost is null)
        {
            return;
        }

        var accent = kind switch
        {
            ToastKind.Success => Color.FromRgb(45, 214, 145),
            ToastKind.Warning => Color.FromRgb(255, 205, 91),
            ToastKind.Error => Color.FromRgb(255, 109, 134),
            _ => Color.FromRgb(97, 213, 255)
        };

        var toast = new Border
        {
            Width = 360,
            MaxWidth = 420,
            Margin = new Thickness(0, 0, 0, 10),
            Padding = new Thickness(14, 12, 14, 12),
            CornerRadius = new CornerRadius(16),
            Opacity = 0,
            Background = new SolidColorBrush(Color.FromArgb(178, 255, 255, 255)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(150, accent.R, accent.G, accent.B)),
            BorderThickness = new Thickness(1.2),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 28,
                ShadowDepth = 8,
                Opacity = 0.18,
                Color = Colors.Black
            }
        };

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = FontWeights.SemiBold,
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.FromRgb(42, 38, 68)),
            TextWrapping = TextWrapping.Wrap
        });
        stack.Children.Add(new TextBlock
        {
            Text = message,
            Margin = new Thickness(0, 4, 0, 0),
            FontSize = 12.5,
            Foreground = new SolidColorBrush(Color.FromRgb(74, 68, 102)),
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxHeight = 84
        });
        toast.Child = stack;

        ToastHost.Children.Insert(0, toast);
        toast.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)));

        await Task.Delay(milliseconds);

        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(220));
        fade.Completed += (_, _) => ToastHost.Children.Remove(toast);
        toast.BeginAnimation(OpacityProperty, fade);
    }
}

internal enum ToastKind
{
    Info,
    Success,
    Warning,
    Error
}
