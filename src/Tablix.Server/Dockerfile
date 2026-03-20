FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src

COPY Tablix.Core/Tablix.Core.csproj Tablix.Core/
COPY Tablix.Server/Tablix.Server.csproj Tablix.Server/
RUN dotnet restore Tablix.Server/Tablix.Server.csproj -a $TARGETARCH

COPY . .
RUN dotnet publish Tablix.Server/Tablix.Server.csproj -a $TARGETARCH -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 9100
EXPOSE 9102

ENTRYPOINT ["dotnet", "Tablix.Server.dll"]
