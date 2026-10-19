FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY WS01-01.csproj .
RUN dotnet restore WS01-01.csproj

COPY . .

RUN dotnet publish WS01-01.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -o /publish \
  /p:PublishSingleFile=true

FROM mcr.microsoft.com/dotnet/runtime-deps:8.0-bookworm-slim AS final

WORKDIR /app

COPY --from=build /publish/WS01-01 .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

ENTRYPOINT ["./WS01-01"]
