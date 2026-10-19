FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["WS01-02.csproj", "./"]
RUN dotnet restore "WS01-02.csproj" -r linux-x64

COPY . .
RUN dotnet publish "WS01-02.csproj" -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime-deps:9.0-noble-chiseled AS final
WORKDIR /app
COPY --from=build /app/publish/WS01-02 ./
EXPOSE 8080
ENTRYPOINT ["./WS01-02"]