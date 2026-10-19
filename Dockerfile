# שלב build: בנייה ו-publish עם ה-SDK המלא
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# קודם רק קובצי ה-csproj, כדי שה-restore יישמר ב-cache כל עוד התלויות לא השתנו
COPY src/Library.Domain/Library.Domain.csproj src/Library.Domain/
COPY src/Library.Application/Library.Application.csproj src/Library.Application/
COPY src/Library.Infrastructure/Library.Infrastructure.csproj src/Library.Infrastructure/
COPY src/Library.API/Library.API.csproj src/Library.API/
RUN dotnet restore src/Library.API/Library.API.csproj

# עכשיו שאר הקוד, ו-publish של ה-API בלבד (בלי פרויקט הבדיקות)
COPY src/ src/
RUN dotnet publish src/Library.API/Library.API.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# שלב runtime: רק ה-runtime של ASP.NET, בלי SDK וקוד מקור
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# ה-file target של NLog כותב ל-${basedir}/logs, כלומר /app/logs. המשתמש app לא יכול ליצור תיקייה ב-/app, לכן יוצרים אותה מראש
RUN mkdir -p /app/logs && chown app:app /app/logs
USER app

# Render מעביר את הפורט במשתנה PORT; בלעדיו (הרצה מקומית) 8080
EXPOSE 8080
ENTRYPOINT ["sh", "-c", "ASPNETCORE_HTTP_PORTS=${PORT:-8080} exec dotnet Library.API.dll"]
