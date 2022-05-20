FROM mcr.microsoft.com/dotnet/runtime:6.0-alpine AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["Sample.Consumer/Sample.Consumer.csproj", "Sample.Consumer/"]
RUN dotnet restore "Sample.Consumer/Sample.Consumer.csproj"
COPY . .
WORKDIR "/src/Sample.Consumer"
RUN dotnet build "Sample.Consumer.csproj" -c Release -o /app/build --no-restore

FROM build AS publish
RUN dotnet publish "Sample.Consumer.csproj" -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Sample.Consumer.dll"]