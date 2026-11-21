namespace AutoShorts
{
    // Вбудований Google OAuth-клієнт (тип «Desktop app»). Під час збирання на GitHub
    // ці значення підставляються із секретів репозиторію GOOGLE_CLIENT_ID і GOOGLE_CLIENT_SECRET.
    // Для «Desktop app» Google не вважає client secret таємницею — його можна вбудовувати в програму.
    static class BuiltInGoogle
    {
        public const string ClientId = "";
        public const string ClientSecret = "";
    }
}
