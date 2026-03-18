FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

ARG VERSION=0.0.0

WORKDIR /src

# Copy solution and project files for layer-cached restore
COPY Fennec.sln .
COPY Fennec.Api/Fennec.Api.csproj Fennec.Api/
COPY Fennec.Shared/Fennec.Shared.csproj Fennec.Shared/

RUN dotnet restore Fennec.Api/Fennec.Api.csproj

# Copy source and publish
COPY Fennec.Api/ Fennec.Api/
COPY Fennec.Shared/ Fennec.Shared/

RUN dotnet publish Fennec.Api/Fennec.Api.csproj \
    -c Release \
    -p:Version=${VERSION} \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Fennec.Api.dll"]
