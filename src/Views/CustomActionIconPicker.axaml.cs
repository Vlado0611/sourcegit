using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace SourceGit.Views
{
    public partial class CustomActionIconPicker : UserControl
    {
        public static readonly StyledProperty<string> IconProperty =
            AvaloniaProperty.Register<CustomActionIconPicker, string>(nameof(Icon), string.Empty, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

        public string Icon
        {
            get => GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public CustomActionIconPicker()
        {
            InitializeComponent();
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            IconList.ItemsSource ??= CustomActionIcon.GetBuiltinIconKeys();
        }

        private void OnIconClicked(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string key })
            {
                Icon = key;
                PickerButton.Flyout?.Hide();
            }

            e.Handled = true;
        }

        private void OnDefaultClicked(object _, RoutedEventArgs e)
        {
            Icon = string.Empty;
            PickerButton.Flyout?.Hide();
            e.Handled = true;
        }

        private async void OnCustomFileClicked(object _, RoutedEventArgs e)
        {
            e.Handled = true;

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
                return;

            var options = new FilePickerOpenOptions()
            {
                FileTypeFilter = [new FilePickerFileType("Image Files") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.ico"] }],
                AllowMultiple = false,
            };

            try
            {
                var selected = await topLevel.StorageProvider.OpenFilePickerAsync(options);
                if (selected is { Count: 1 })
                {
                    Icon = Models.CustomAction.IconFilePrefix + selected[0].Path.LocalPath;
                    PickerButton.Flyout?.Hide();
                }
            }
            catch (Exception ex)
            {
                if (topLevel is Window owner)
                    await new Alert().ShowAsync(owner, $"Failed to select icon file: {ex.Message}", true);
            }
        }
    }
}
