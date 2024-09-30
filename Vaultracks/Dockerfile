FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 8080
VOLUME ["/data"]

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
# Prepare for building - install Powershell and Node.js
RUN apt-get update
RUN curl -fsSL https://deb.nodesource.com/setup_lts.x | bash -
RUN apt-get install -y nodejs wget
RUN wget https://github.com/PowerShell/PowerShell/releases/download/v7.4.5/powershell_7.4.5-1.deb_amd64.deb
RUN dpkg -i powershell_7.4.5-1.deb_amd64.deb
RUN apt-get install -f
RUN rm powershell_7.4.5-1.deb_amd64.deb
# Start building
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Vaultracks/Vaultracks.csproj", "Vaultracks/"]
RUN dotnet restore "Vaultracks/Vaultracks.csproj"
COPY . .
WORKDIR "/src/Vaultracks"
RUN dotnet build "Vaultracks.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
RUN dotnet publish "Vaultracks.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Vaultracks.dll"]