using System;
using System.Collections.Generic;
using System.IO;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace SourceGit.Views
{
    public class CustomActionIcon : Control
    {
        public static readonly StyledProperty<string> IconProperty =
            AvaloniaProperty.Register<CustomActionIcon, string>(nameof(Icon), string.Empty);

        public static readonly StyledProperty<IBrush> ForegroundProperty =
            AvaloniaProperty.Register<CustomActionIcon, IBrush>(nameof(Foreground), Brushes.Gray);

        public string Icon
        {
            get => GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public IBrush Foreground
        {
            get => GetValue(ForegroundProperty);
            set => SetValue(ForegroundProperty, value);
        }

        static CustomActionIcon()
        {
            AffectsRender<CustomActionIcon>(IconProperty, ForegroundProperty);
        }

        public static List<string> GetBuiltinIconKeys()
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            if (Application.Current?.Resources is { } res)
                Collect(res, keys, []);

            var list = new List<string>(keys);
            list.Sort(StringComparer.Ordinal);
            return list;
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            var bounds = new Rect(Bounds.Size);
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;

            var icon = Icon;
            if (!string.IsNullOrEmpty(icon) && icon.StartsWith(Models.CustomAction.IconFilePrefix, StringComparison.Ordinal))
            {
                var bitmap = LoadBitmap(icon.Substring(Models.CustomAction.IconFilePrefix.Length));
                if (bitmap != null)
                {
                    var size = bitmap.Size;
                    if (size.Width > 0 && size.Height > 0)
                    {
                        var scale = Math.Min(bounds.Width / size.Width, bounds.Height / size.Height);
                        var w = size.Width * scale;
                        var h = size.Height * scale;
                        var dest = new Rect((bounds.Width - w) / 2, (bounds.Height - h) / 2, w, h);
                        context.DrawImage(bitmap, new Rect(size), dest);
                        return;
                    }
                }

                icon = null;
            }

            var geo = FindGeometry(icon) ?? FindGeometry("Icons.Action");
            if (geo == null)
                return;

            var gb = geo.Bounds;
            if (gb.Width <= 0 || gb.Height <= 0)
                return;

            var s = Math.Min(bounds.Width / gb.Width, bounds.Height / gb.Height);
            var tx = (bounds.Width - gb.Width * s) / 2 - gb.X * s;
            var ty = (bounds.Height - gb.Height * s) / 2 - gb.Y * s;
            using (context.PushTransform(new Matrix(s, 0, 0, s, tx, ty)))
                context.DrawGeometry(Foreground, null, geo);
        }

        private static Geometry FindGeometry(string key)
        {
            if (string.IsNullOrEmpty(key) || Application.Current == null)
                return null;

            if (Application.Current.TryFindResource(key, out var value) && value is Geometry geo)
                return geo;

            return null;
        }

        private static void Collect(IResourceDictionary dict, HashSet<string> keys, HashSet<object> visited)
        {
            if (!visited.Add(dict))
                return;

            foreach (var kv in dict)
            {
                // Values may be deferred, so resolve them through the application instead of reading kv.Value.
                if (kv.Key is string key &&
                    key.StartsWith("Icons.", StringComparison.Ordinal) &&
                    Application.Current.TryFindResource(key, out var value) &&
                    value is Geometry)
                    keys.Add(key);
            }

            foreach (var child in dict.MergedDictionaries)
            {
                // ResourceInclude (and its subclass MergeResourceInclude) is not a dictionary; its content is in Loaded.
                if (child is ResourceInclude include)
                {
                    if (include.Loaded is { } loaded)
                        Collect(loaded, keys, visited);
                }
                else if (child is IResourceDictionary d)
                {
                    Collect(d, keys, visited);
                }
            }
        }

        private static Bitmap LoadBitmap(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            DateTime stamp;
            try
            {
                stamp = File.GetLastWriteTimeUtc(path);
            }
            catch
            {
                return null;
            }

            var key = (path, stamp);
            if (_bitmaps.TryGetValue(key, out var cached))
                return cached;

            Bitmap bitmap = null;
            try
            {
                if (File.Exists(path))
                    bitmap = new Bitmap(path);
            }
            catch
            {
                bitmap = null;
            }

            _bitmaps[key] = bitmap;
            return bitmap;
        }

        private static readonly Dictionary<(string, DateTime), Bitmap> _bitmaps = [];
    }
}
