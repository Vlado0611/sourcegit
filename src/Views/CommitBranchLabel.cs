using System;
using System.Collections.Generic;
using System.Globalization;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace SourceGit.Views
{
    public class CommitBranchLabel : Control
    {
        public enum HitPart
        {
            None,
            Label,
            More,
        }

        public static readonly StyledProperty<FontFamily> FontFamilyProperty =
            TextBlock.FontFamilyProperty.AddOwner<CommitBranchLabel>();

        public FontFamily FontFamily
        {
            get => GetValue(FontFamilyProperty);
            set => SetValue(FontFamilyProperty, value);
        }

        public static readonly StyledProperty<double> FontSizeProperty =
           TextBlock.FontSizeProperty.AddOwner<CommitBranchLabel>();

        public double FontSize
        {
            get => GetValue(FontSizeProperty);
            set => SetValue(FontSizeProperty, value);
        }

        public static readonly StyledProperty<IBrush> BackgroundProperty =
            AvaloniaProperty.Register<CommitBranchLabel, IBrush>(nameof(Background), Brushes.Transparent);

        public IBrush Background
        {
            get => GetValue(BackgroundProperty);
            set => SetValue(BackgroundProperty, value);
        }

        public static readonly StyledProperty<IBrush> ForegroundProperty =
            AvaloniaProperty.Register<CommitBranchLabel, IBrush>(nameof(Foreground), Brushes.White);

        public IBrush Foreground
        {
            get => GetValue(ForegroundProperty);
            set => SetValue(ForegroundProperty, value);
        }

        public static readonly StyledProperty<IBrush> ChipBrushProperty =
            AvaloniaProperty.Register<CommitBranchLabel, IBrush>(nameof(ChipBrush), Brushes.Gray);

        public IBrush ChipBrush
        {
            get => GetValue(ChipBrushProperty);
            set => SetValue(ChipBrushProperty, value);
        }

        public static readonly StyledProperty<bool> ShowTagsProperty =
            AvaloniaProperty.Register<CommitBranchLabel, bool>(nameof(ShowTags), true);

        public bool ShowTags
        {
            get => GetValue(ShowTagsProperty);
            set => SetValue(ShowTagsProperty, value);
        }

        public static readonly StyledProperty<bool> UseCompactBranchNamesProperty =
            AvaloniaProperty.Register<CommitBranchLabel, bool>(nameof(UseCompactBranchNames));

        public bool UseCompactBranchNames
        {
            get => GetValue(UseCompactBranchNamesProperty);
            set => SetValue(UseCompactBranchNamesProperty, value);
        }

        public static readonly StyledProperty<bool> HasSingleRemoteProperty =
            AvaloniaProperty.Register<CommitBranchLabel, bool>(nameof(HasSingleRemote));

        public bool HasSingleRemote
        {
            get => GetValue(HasSingleRemoteProperty);
            set => SetValue(HasSingleRemoteProperty, value);
        }

        public static readonly StyledProperty<HashSet<string>> BitbucketRemotesProperty =
            AvaloniaProperty.Register<CommitBranchLabel, HashSet<string>>(nameof(BitbucketRemotes));

        public HashSet<string> BitbucketRemotes
        {
            get => GetValue(BitbucketRemotesProperty);
            set => SetValue(BitbucketRemotesProperty, value);
        }

        public static readonly StyledProperty<bool> UseGraphColorProperty =
            AvaloniaProperty.Register<CommitBranchLabel, bool>(nameof(UseGraphColor));

        public bool UseGraphColor
        {
            get => GetValue(UseGraphColorProperty);
            set => SetValue(UseGraphColorProperty, value);
        }

        public Models.Decorator VisibleDecorator
        {
            get => VisibleGroup?.Decorator;
        }

        public List<Models.Decorator> HiddenDecorators
        {
            get => HiddenGroups.ConvertAll(g => g.Decorator);
        }

        // The group drawn on the pill (its RemoteDecorators are the remotes merged into it), or null.
        public Models.DecoratorGroup VisibleGroup
        {
            get => _groups.Count > 0 ? _groups[0] : null;
        }

        // The groups behind the +N chip, in display order.
        public List<Models.DecoratorGroup> HiddenGroups
        {
            get => _groups.Count > 1 ? _groups.GetRange(1, _groups.Count - 1) : [];
        }

        public HitPart HitTest(Point p)
        {
            EnsureLayout();

            if (_hasPill && _pillRect.Contains(p))
                return HitPart.Label;

            if (_hasChip && _chipRect.Contains(p))
                return HitPart.More;

            return HitPart.None;
        }

        public override void Render(DrawingContext context)
        {
            var bg = Background;
            if (bg != null)
                context.FillRectangle(bg, new Rect(Bounds.Size));

            if (_groups.Count == 0)
                return;

            EnsureLayout();

            var fg = Foreground;
            var cy = Bounds.Height * 0.5;

            if (_hasPill)
            {
                context.DrawRectangle(_pillBrush, null, new RoundedRect(_pillRect, new CornerRadius(9)));

                var pillFg = _pillForeground;
                var x = _pillRect.X + PillPadding;
                if (_leadingIcon != null)
                {
                    using (context.PushTransform(Matrix.CreateTranslation(x, cy - IconSize * 0.5)))
                        context.DrawGeometry(pillFg, null, _leadingIcon);
                    x += IconSize + IconGap;
                }

                if (_text != null)
                {
                    using (context.PushClip(new Rect(x, _pillRect.Y, _textWidth, _pillRect.Height)))
                        context.DrawText(_text, new Point(x, cy - _text.Height * 0.5));
                    x += _textWidth;
                }

                foreach (var item in _trailing)
                {
                    x += IconGap;
                    using (context.PushTransform(Matrix.CreateTranslation(x, cy - IconSize * 0.5)))
                        context.DrawGeometry(pillFg, null, item.Icon);
                    x += IconSize;

                    if (item.Text != null)
                    {
                        x += RemoteNameGap;
                        context.DrawText(item.Text, new Point(x, cy - item.Text.Height * 0.5));
                        x += item.Text.WidthIncludingTrailingWhitespace;
                    }
                }
            }

            if (_hasChip)
            {
                context.DrawRectangle(ChipBrush, null, new RoundedRect(_chipRect, new CornerRadius(9)));
                context.DrawText(_chipText, new Point(_chipRect.X + ChipPadding, cy - _chipText.Height * 0.5));
            }

            var lineStart = _hasChip ? _chipRect.Right : (_hasPill ? _pillRect.Right : Bounds.Width);
            if (lineStart < Bounds.Width)
                context.DrawLine(_linePen, new Point(lineStart, cy), new Point(Bounds.Width, cy));
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ShowTagsProperty ||
                change.Property == UseCompactBranchNamesProperty ||
                change.Property == IsVisibleProperty)
            {
                RebuildGroups();
            }
            else if (change.Property == FontFamilyProperty ||
                change.Property == FontSizeProperty ||
                change.Property == ForegroundProperty ||
                change.Property == BackgroundProperty ||
                change.Property == ChipBrushProperty ||
                change.Property == UseGraphColorProperty ||
                change.Property == HasSingleRemoteProperty ||
                change.Property == BitbucketRemotesProperty ||
                change.Property == BoundsProperty)
            {
                _layoutDirty = true;
                InvalidateVisual();
            }
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            RebuildGroups();
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);

            // Only the pill carries the tooltip; the dashed line and the +N chip don't.
            var tip = HitTest(e.GetPosition(this)) == HitPart.Label ? _toolTip : null;
            if (!ReferenceEquals(ToolTip.GetTip(this), tip))
                ToolTip.SetTip(this, tip);
        }

        protected override void OnPointerExited(PointerEventArgs e)
        {
            base.OnPointerExited(e);

            if (ToolTip.GetTip(this) != null)
                ToolTip.SetTip(this, null);
        }

        private void RebuildGroups()
        {
            // Hidden in Classic style: don't spend any work on rows nobody sees. A rebuild runs
            // again when the label becomes visible.
            if (!IsVisible)
            {
                if (_groups.Count > 0)
                {
                    _groups = [];
                    _toolTip = null;
                    _layoutDirty = true;
                }

                return;
            }

            if (DataContext is Models.Commit commit)
                _groups = Models.DecoratorGroup.Build(commit.Decorators, ShowTags, UseCompactBranchNames);
            else
                _groups = [];

            _toolTip = null;
            if (_groups.Count > 0)
            {
                var group = _groups[0];
                if (group.RemoteDecorators.Count == 0)
                {
                    _toolTip = group.Decorator.Name;
                }
                else
                {
                    var names = new List<string>() { group.Decorator.Name };
                    foreach (var remote in group.RemoteDecorators)
                        names.Add(remote.Name);
                    _toolTip = string.Join(Environment.NewLine, names);
                }
            }

            if (ToolTip.GetTip(this) != null)
                ToolTip.SetTip(this, null);

            _layoutDirty = true;
            InvalidateVisual();
        }

        private void EnsureLayout()
        {
            if (!_layoutDirty)
                return;

            _layoutDirty = false;
            _hasPill = false;
            _hasChip = false;
            _text = null;
            _textWidth = 0;
            _leadingIcon = null;
            _trailing.Clear();

            if (_groups.Count == 0 || DataContext is not Models.Commit commit)
                return;

            var width = Bounds.Width;
            var cy = Bounds.Height * 0.5;
            var fg = Foreground;
            var fontSize = FontSize;
            var typeface = new Typeface(FontFamily);
            var group = _groups[0];

            // Dimmed rows use the same translucent gray as dimmed lanes; the text keeps the theme
            // foreground there since the pill color depends on what is underneath. Solid pills pick
            // black or white text, whichever contrasts more with the pill color.
            if (UseGraphColor)
            {
                _linePen = Models.CommitGraph.DashedPens[commit.Color];
                _pillBrush = group.Decorator.Type == Models.DecoratorType.Tag ? Brushes.Gray : Models.CommitGraph.Pens[commit.Color].Brush;
                _pillForeground = ContrastingForeground(_pillBrush);
            }
            else
            {
                _linePen = CommitGraph.DimmedDashedPen;
                _pillBrush = CommitGraph.DimmedBrush;
                _pillForeground = fg;
            }

            // Right-aligned: [pill][gap][+N chip][right padding]
            var right = width - RightPadding;
            if (_groups.Count > 1)
            {
                _chipText = new FormattedText(
                    $"+{_groups.Count - 1}",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    fontSize,
                    fg);

                var chipWidth = _chipText.WidthIncludingTrailingWhitespace + ChipPadding * 2;
                _chipRect = new Rect(right - chipWidth, cy - PillHeight * 0.5, chipWidth, PillHeight);
                _hasChip = true;
                right = _chipRect.X - ChipGap;
            }

            var available = right - LeftPadding;
            if (available < PillPadding * 2)
                return;

            var pillFg = _pillForeground;
            var iconCache = CommitRefsIconCache.Instance;
            var bitbucketRemotes = BitbucketRemotes;
            var leadingIcon = iconCache.GetIcon(group.Decorator.Type);
            if (group.Decorator.Type == Models.DecoratorType.RemoteBranchHead)
            {
                var decoratorName = group.Decorator.Name;
                var slash = decoratorName.IndexOf('/');
                if (slash > 0)
                    leadingIcon = iconCache.GetRemoteIcon(decoratorName.Substring(0, slash), bitbucketRemotes);
            }

            // Trailing marks only add what the leading icon doesn't already say: a laptop for local
            // branches (not for detached HEAD) and a cloud per merged remote (not for remote-only
            // refs, whose leading icon is the cloud). With several remotes configured the remote
            // name follows each cloud, like CommitRefsPresenter does in compact mode.
            var trailing = new List<TrailingItem>();
            if (group.Decorator.Type is Models.DecoratorType.CurrentBranchHead or Models.DecoratorType.LocalBranchHead)
                trailing.Add(new TrailingItem(LocalIcon, null));

            if (group.Remotes.Count > 0)
            {
                if (HasSingleRemote)
                {
                    trailing.Add(new TrailingItem(iconCache.GetRemoteIcon(group.Remotes[0], bitbucketRemotes), null));
                }
                else
                {
                    var remoteTypeface = new Typeface(FontFamily, FontStyle.Italic, FontWeight.Bold);
                    foreach (var remote in group.Remotes)
                    {
                        var remoteText = new FormattedText(
                            remote,
                            CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            remoteTypeface,
                            fontSize,
                            pillFg);
                        trailing.Add(new TrailingItem(iconCache.GetRemoteIcon(remote, bitbucketRemotes), remoteText));
                    }
                }
            }

            var label = new FormattedText(
                group.Decorator.Name,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                group.IsHead ? new Typeface(FontFamily, FontStyle.Normal, FontWeight.Bold) : typeface,
                fontSize,
                pillFg);

            var naturalTextWidth = label.WidthIncludingTrailingWhitespace;
            var textMax = available - FixedWidth(leadingIcon != null, trailing);

            // Not enough room: give the text more space by dropping trailing marks, then the leading icon.
            while (textMax < naturalTextWidth && textMax < MinTextWidth && trailing.Count > 0)
            {
                trailing.RemoveAt(trailing.Count - 1);
                textMax = available - FixedWidth(leadingIcon != null, trailing);
            }

            if (textMax < naturalTextWidth && textMax < MinTextWidth && leadingIcon != null)
            {
                leadingIcon = null;
                textMax = available - FixedWidth(false, trailing);
            }

            textMax = Math.Max(0, textMax);
            if (naturalTextWidth > textMax)
            {
                label.MaxTextWidth = Math.Max(1, textMax);
                label.MaxLineCount = 1;
                label.Trimming = TextTrimming.CharacterEllipsis;
                _textWidth = textMax;
            }
            else
            {
                _textWidth = naturalTextWidth;
            }

            _text = label;
            _leadingIcon = leadingIcon;
            _trailing.AddRange(trailing);

            var pillWidth = FixedWidth(leadingIcon != null, trailing) + _textWidth;
            _pillRect = new Rect(right - pillWidth, cy - PillHeight * 0.5, pillWidth, PillHeight);
            _hasPill = true;
        }

        private static double FixedWidth(bool hasLeadingIcon, List<TrailingItem> trailing)
        {
            var w = PillPadding * 2;
            foreach (var item in trailing)
                w += item.Width;
            if (hasLeadingIcon)
                w += IconSize + IconGap;
            return w;
        }

        // Black or white, whichever has the higher WCAG contrast ratio against the pill color.
        private static IBrush ContrastingForeground(IBrush pill)
        {
            if (pill is not ISolidColorBrush solid)
                return Brushes.White;

            var c = solid.Color;
            var l = 0.2126 * Linearize(c.R) + 0.7152 * Linearize(c.G) + 0.0722 * Linearize(c.B);

            // (l + 0.05) / 0.05 > 1.05 / (l + 0.05)  <=>  l > ~0.179
            return l > 0.179 ? Brushes.Black : Brushes.White;
        }

        private static double Linearize(byte channel)
        {
            var v = channel / 255.0;
            return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }

        private class TrailingItem(Geometry icon, FormattedText text)
        {
            public Geometry Icon { get; } = icon;
            public FormattedText Text { get; } = text;
            public double Width { get; } = IconGap + IconSize + (text != null ? RemoteNameGap + text.WidthIncludingTrailingWhitespace : 0);
        }

        private static Geometry LocalIcon
        {
            get
            {
                if (_localIcon == null)
                {
                    var geo = (App.Current.FindResource("Icons.Local") as StreamGeometry)!.Clone();
                    var bounds = geo.Bounds;
                    var translation = Matrix.CreateTranslation(-(Vector)bounds.Position);
                    var scale = Math.Min(IconSize / bounds.Width, IconSize / bounds.Height);
                    var transform = translation * Matrix.CreateScale(scale, scale);
                    if (geo.Transform == null || geo.Transform.Value == Matrix.Identity)
                        geo.Transform = new MatrixTransform(transform);
                    else
                        geo.Transform = new MatrixTransform(geo.Transform.Value * transform);
                    _localIcon = geo;
                }

                return _localIcon;
            }
        }

        private const double PillHeight = 18;
        private const double PillPadding = 6;
        private const double IconSize = 10;
        private const double IconGap = 4;
        private const double ChipPadding = 6;
        private const double ChipGap = 4;
        private const double RightPadding = 6;
        private const double LeftPadding = 4;
        private const double MinTextWidth = 16;
        private const double RemoteNameGap = 2;

        private static Geometry _localIcon = null;

        private List<Models.DecoratorGroup> _groups = [];
        private bool _layoutDirty = true;
        private bool _hasPill = false;
        private bool _hasChip = false;
        private Rect _pillRect;
        private Rect _chipRect;
        private FormattedText _text = null;
        private FormattedText _chipText = null;
        private double _textWidth = 0;
        private Geometry _leadingIcon = null;
        private List<TrailingItem> _trailing = [];
        private IBrush _pillBrush = Brushes.Gray;
        private IBrush _pillForeground = Brushes.White;
        private IPen _linePen = CommitGraph.DimmedDashedPen;
        private string _toolTip = null;
    }
}
