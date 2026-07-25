namespace NoMercy.Analyzers
{
    using System.Reflection;
    using System.Resources;

    internal static class Resources
    {
        private static readonly ResourceManager Manager =
            new ResourceManager("NoMercy.Analyzers.Resources", Assembly.GetExecutingAssembly());

        public static string RequireNamedArgumentsTitle =>
            Manager.GetString(nameof(RequireNamedArgumentsTitle))!;

        public static string RequireNamedArgumentsMessage =>
            Manager.GetString(nameof(RequireNamedArgumentsMessage))!;

        public static string RequireNamedArgumentsDescription =>
            Manager.GetString(nameof(RequireNamedArgumentsDescription))!;

        public static string RequireNamedArgumentsMessageFormat =>
            Manager.GetString(nameof(RequireNamedArgumentsMessageFormat))!;

        public static string CallbackParameterShouldBeRenamedTitle =>
            Manager.GetString(nameof(CallbackParameterShouldBeRenamedTitle))!;

        public static string CallbackParameterShouldBeRenamedMessageFormat =>
            Manager.GetString(nameof(CallbackParameterShouldBeRenamedMessageFormat))!;

        public static string CallbackParameterShouldBeRenamedDescription =>
            Manager.GetString(nameof(CallbackParameterShouldBeRenamedDescription))!;

        public static string RequireNamedTitle =>
            Manager.GetString(nameof(RequireNamedTitle))!;

        public static string CallbackParameterTitle =>
            Manager.GetString(nameof(CallbackParameterTitle))!;
    }
}