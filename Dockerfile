FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY TCPNoiseObservatory/TCPNoiseObservatory.csproj TCPNoiseObservatory/
RUN dotnet restore TCPNoiseObservatory/TCPNoiseObservatory.csproj

COPY . .

RUN dotnet publish TCPNoiseObservatory/TCPNoiseObservatory.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "TCPNoiseObservatory.dll"]