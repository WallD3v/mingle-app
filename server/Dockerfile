FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Mingle.Server.csproj ./
RUN dotnet restore Mingle.Server.csproj
COPY . .
RUN dotnet publish Mingle.Server.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENV TCP_PORT=58081
EXPOSE 58081
ENTRYPOINT ["dotnet", "Mingle.Server.dll"]
