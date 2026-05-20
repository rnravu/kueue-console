FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# copy csproj and restore as distinct layers
COPY ["KueueConsole.Web.csproj", "./"]
RUN dotnet restore "KueueConsole.Web.csproj"

# copy everything else and publish
COPY . .
RUN dotnet publish "KueueConsole.Web.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80

ENTRYPOINT ["dotnet", "KueueConsole.Web.dll"]
