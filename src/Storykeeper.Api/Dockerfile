FROM mcr.microsoft.com/dotnet/sdk:10.0

WORKDIR /app
COPY src/Storykeeper.Api/Storykeeper.Api.csproj src/Storykeeper.Api/
RUN dotnet restore src/Storykeeper.Api/Storykeeper.Api.csproj
COPY src/Storykeeper.Api/ src/Storykeeper.Api/

EXPOSE 8080
ENTRYPOINT ["dotnet", "run", "--project", "src/Storykeeper.Api/Storykeeper.Api.csproj", "--no-restore", "--no-launch-profile", "--urls", "http://0.0.0.0:8080"]
