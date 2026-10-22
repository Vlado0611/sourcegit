using System;
using System.Collections.Generic;
using System.Globalization;

using Avalonia;
using Avalonia.Controls;
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

        public static readonly StyledProperty<bool> UseGraphColorProperty =
            AvaloniaProperty.Register<CommitBranchLabel, bool>(nameof(UseGraphColor));

        public bool UseGraphColor
        {
            get => GetValue(UseGraphColorProperty);
            set => SetValue(UseGraphColorProperty, value);
        }

        public Models.Decorator VisibleDecorator
        {
            get => _groups.Count > 0 ? _groups[0].Decorator : null;
        }

        public List<Models.Decorator> HiddenDecorators
        {
            get
            {
                var hidden = new List<Models.Decorator>();
                for (var i = 1; i < _groups.Count; i++)
                    hidden.Add(_groups[i].Decorator);
                return hidden;
            }
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

                var x = _pillRect.X + PillPadding;
                if (_leadingIcon != null)
                {
                    using (context.PushTransform(Matrix.CreateTranslation(x, cy - 5)))
                        context.DrawGeometry(fg, null, _leadingIcon);
                    x += IconSize + IconGap;
                }

                if (_text != null)
                {
                    using (context.PushClip(new Rect(x, _pillRect.Y, _textWidth, _pillRect.Height)))
                        context.DrawText(_text, new Point(x, cy - _text.Height * 0.5));
                    x += _textWidth;
                }

                foreach (var icon in _trailingIcons)
                {
                    x += IconGap;
                    using (context.PushTransform(Matrix.CreateTranslation(x, cy - 5)))
                        context.DrawGeometry(fg, null, icon);
                    x += IconSize;
                }
            }

            if (_hasChip)
            {
                context.DrawRectangle(ChipBrush, null, new RoundedRect(_chipRect, new CornerRadius(9)));
                context.DrawText(_chipText, new Point(_chipRect.X + ChipPadding, cy - _chipText.Height * 0.5));
            }

            var lineStart = _hasChip ? _chipRect.Right : (_hasPill ? _pillRect.Right : Bounds.Width);
            if (lineStart < Bounds.Width)
            {
                var pen = new Pen(_lineBrush, 2, DashStyle.Dash);
                context.DrawLine(pen, new Point(lineStart, cy), new Point(Bounds.Width, cy));
            }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ShowTagsProperty ||
                change.Property == UseCompactBranchNamesProperty)
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

        private void RebuildGroups()
        {
            if (DataContext is Models.Commit commit)
                _groups = Models.DecoratorGroup.Build(commit.Decorators, ShowTags, UseCompactBranchNames);
            else
                _groups = [];

            ToolTip.SetTip(this, _groups.Count > 0 ? _groups[0].Decorator.Name : null);

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
            _trailingIcons.Clear();

            if (_groups.Count == 0 || DataContext is not Models.Commit commit)
                return;

            var width = Bounds.Width;
            var cy = Bounds.Height * 0.5;
            var fg = Foreground;
            var fontSize = FontSize;
            var typeface = new Typeface(FontFamily);
            var laneBrush = UseGraphColor ? Models.CommitGraph.Pens[commit.Color].Brush : Brushes.Gray;
            var group = _groups[0];

            _lineBrush = laneBrush;
            _pillBrush = group.Decorator.Type == Models.DecoratorType.Tag ? Brushes.Gray : laneBrush;

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

            var leadingIcon = CommitRefsIconCache.Instance.GetIcon(group.Decorator.Type);
            var trailingIcons = new List<Geometry>();
            if (group.IsLocal)
                trailingIcons.Add(LocalIcon);
            if (group.IsRemote)
                trailingIcons.Add(CommitRefsIconCache.Instance.GetIcon(Models.DecoratorType.RemoteBranchHead));

            var label = new FormattedText(
                group.Decorator.Name,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                group.IsHead ? new Typeface(FontFamily, FontStyle.Normal, FontWeight.Bold) : typeface,
                fontSize,
                fg);

            var naturalTextWidth = label.WidthIncludingTrailingWhitespace;
            var textMax = available - FixedWidth(leadingIcon != null, trailingIcons.Count);

            // Not enough room: give the text more space by dropping trailing icons, then the leading icon.
            while (textMax < naturalTextWidth && textMax < MinTextWidth && trailingIcons.Count > 0)
            {
                trailingIcons.RemoveAt(trailingIcons.Count - 1);
                textMax = available - FixedWidth(leadingIcon != null, trailingIcons.Count);
            }

            if (textMax < naturalTextWidth && textMax < MinTextWidth && leadingIcon != null)
            {
                leadingIcon = null;
                textMax = available - FixedWidth(false, trailingIcons.Count);
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
            _trailingIcons.AddRange(trailingIcons);

            var pillWidth = FixedWidth(leadingIcon != null, trailingIcons.Count) + _textWidth;
            _pillRect = new Rect(right - pillWidth, cy - PillHeight * 0.5, pillWidth, PillHeight);
            _hasPill = true;
        }

        private static double FixedWidth(bool hasLeadingIcon, int trailingIconCount)
        {
            var w = PillPadding * 2 + trailingIconCount * (IconGap + IconSize);
            if (hasLeadingIcon)
                w += IconSize + IconGap;
            return w;
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
        private List<Geometry> _trailingIcons = [];
        private IBrush _pillBrush = Brushes.Gray;
        private IBrush _lineBrush = Brushes.Gray;
    }
}
