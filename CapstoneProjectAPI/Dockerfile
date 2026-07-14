# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["CapstoneProjectAPI.csproj", "./"]
RUN dotnet restore "CapstoneProjectAPI.csproj"
COPY . .
WORKDIR "/src/"
RUN dotnet build "CapstoneProjectAPI.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "CapstoneProjectAPI.csproj" -c Release -o /app/publish

# Final stage/image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CapstoneProjectAPI.dll"]
