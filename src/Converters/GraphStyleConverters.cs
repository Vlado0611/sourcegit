using Avalonia.Data.Converters;

namespace SourceGit.Converters
{
    public static class GraphStyleConverters
    {
        public static readonly FuncValueConverter<Models.GraphStyle, string> ToName =
            new FuncValueConverter<Models.GraphStyle, string>(v =>
            {
                return v switch
                {
                    Models.GraphStyle.BranchColumn => App.Text("Preferences.General.GraphStyle.BranchColumn"),
                    _ => App.Text("Preferences.General.GraphStyle.Classic"),
                };
            });

        public static readonly FuncValueConverter<Models.GraphStyle, double> ToRowHeight =
            new FuncValueConverter<Models.GraphStyle, double>(v => v == Models.GraphStyle.BranchColumn ? 30.0 : 26.0);

        public static readonly FuncValueConverter<Models.GraphStyle, bool> IsClassic =
            new FuncValueConverter<Models.GraphStyle, bool>(v => v == Models.GraphStyle.Classic);

        public static readonly FuncValueConverter<Models.GraphStyle, bool> IsBranchColumn =
            new FuncValueConverter<Models.GraphStyle, bool>(v => v == Models.GraphStyle.BranchColumn);
    }
}
