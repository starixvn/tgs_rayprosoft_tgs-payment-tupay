#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
ARG BuildConfiguration=Release
RUN --mount=type=secret,id=nuget,dst=/pat cp /pat ~/.nuget/NuGet/NuGet.Config
COPY ["nuget.config", "."]
COPY ["Tgs.Payment.TuPay/Tgs.Payment.TuPay.csproj", "Tgs.Payment.TuPay/"]
COPY ["TuPay.Core/TuPay.Core.csproj", "TuPay.Core/"]
COPY ["TuPay.Client/TuPay.Client.csproj", "TuPay.Client/"]
RUN dotnet restore "Tgs.Payment.TuPay/Tgs.Payment.TuPay.csproj"
COPY . .
WORKDIR "/src/Tgs.Payment.TuPay"
RUN dotnet build "Tgs.Payment.TuPay.csproj" -c $BuildConfiguration -o /app/build

FROM build AS publish
ARG BuildConfiguration
RUN dotnet publish "Tgs.Payment.TuPay.csproj" -c $BuildConfiguration -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Tgs.Payment.TuPay.dll"]