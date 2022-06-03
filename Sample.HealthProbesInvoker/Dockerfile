#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["Sample.HealthProbesInvoker/Sample.HealthProbesInvoker.csproj", "Sample.HealthProbesInvoker/"]
RUN dotnet restore "Sample.HealthProbesInvoker/Sample.HealthProbesInvoker.csproj"
COPY . .
WORKDIR "/src/Sample.HealthProbesInvoker"
RUN dotnet build "Sample.HealthProbesInvoker.csproj" -c Release -o /app/build --no-restore

FROM build AS publish
RUN dotnet publish "Sample.HealthProbesInvoker.csproj" -c Release -o /app/publish --no-build

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Sample.HealthProbesInvoker.dll"]