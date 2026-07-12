FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY TwoRooms.slnx .
COPY TwoRooms/TwoRooms.csproj TwoRooms/
COPY TwoRooms.Client/TwoRooms.Client.csproj TwoRooms.Client/
RUN dotnet restore TwoRooms/TwoRooms.csproj

COPY . .
RUN dotnet publish TwoRooms/TwoRooms.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
# Render assigns the listen port via $PORT at container start; ASPNETCORE_HTTP_PORTS
# has to be set at runtime (not build time) to pick it up.
CMD ASPNETCORE_HTTP_PORTS=$PORT dotnet TwoRooms.dll
