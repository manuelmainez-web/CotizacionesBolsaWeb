FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY CotizacionesBolsaWeb.csproj .
RUN dotnet restore "CotizacionesBolsaWeb.csproj"
COPY . .
RUN dotnet publish "CotizacionesBolsaWeb.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 10000
ENTRYPOINT ["/bin/sh", "-c", "dotnet CotizacionesBolsaWeb.dll --urls http://+:${PORT:-10000}"]
