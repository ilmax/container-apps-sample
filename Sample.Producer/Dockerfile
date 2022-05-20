FROM mcr.microsoft.com/dotnet/aspnet:6.0-alpine AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["Sample.Producer/Sample.Producer.csproj", "Sample.Producer/"]
RUN dotnet restore "Sample.Producer/Sample.Producer.csproj"
COPY . .
WORKDIR "/src/Sample.Producer"
RUN dotnet build "Sample.Producer.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Sample.Producer.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Sample.Producer.dll"]