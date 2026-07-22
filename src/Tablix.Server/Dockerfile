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
RUN set -eux; \
    find /etc/apt -type f \( -name "sources.list" -o -name "*.sources" \) -exec sed -i 's|http://archive.ubuntu.com/ubuntu|https://archive.ubuntu.com/ubuntu|g; s|http://security.ubuntu.com/ubuntu|https://security.ubuntu.com/ubuntu|g' {} +; \
    printf '%s\n' \
        'Acquire::Retries "5";' \
        'Acquire::http::Timeout "30";' \
        'Acquire::https::Timeout "30";' \
        > /etc/apt/apt.conf.d/80-retries; \
    apt-get update; \
    apt-get install -y --no-install-recommends curl; \
    rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .

EXPOSE 9100
EXPOSE 9102

ENTRYPOINT ["dotnet", "Tablix.Server.dll"]
