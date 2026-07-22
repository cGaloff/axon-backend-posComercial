# ---------- Build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copiar csproj y restaurar (mejor cache de capas)
COPY src/Axon.Domain/Axon.Domain.csproj                 src/Axon.Domain/
COPY src/Axon.Application/Axon.Application.csproj        src/Axon.Application/
COPY src/Axon.Infrastructure/Axon.Infrastructure.csproj src/Axon.Infrastructure/
COPY src/Axon.API/Axon.API.csproj                        src/Axon.API/
RUN dotnet restore src/Axon.API/Axon.API.csproj

# Copiar el resto y publicar
COPY . .
RUN dotnet publish src/Axon.API/Axon.API.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---------- Runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Dependencias nativas para QuestPDF/SkiaSharp (generación de PDFs)
RUN apt-get update \
    && apt-get install -y --no-install-recommends libfontconfig1 libfreetype6 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Axon.API.dll"]
