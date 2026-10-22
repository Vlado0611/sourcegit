using Avalonia.Data.Converters;

namespace SourceGit.Converters
{
    public static class CustomActionConverters
    {
        public static readonly FuncValueConverter<Models.CustomActionScope, bool> IsRepositoryScope =
            new(v => v == Models.CustomActionScope.Repository);
    }
}
