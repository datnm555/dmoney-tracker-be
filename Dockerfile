FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props global.json NuGet.config .editorconfig ./
COPY src/SharedKernel/SharedKernel.csproj src/SharedKernel/
COPY src/Domain/Domain.csproj src/Domain/
COPY src/Application/Application.csproj src/Application/
COPY src/Infrastructure/Infrastructure.csproj src/Infrastructure/
COPY src/Web.Api/Web.Api.csproj src/Web.Api/
RUN dotnet restore src/Web.Api/Web.Api.csproj
COPY src/ src/
RUN dotnet publish src/Web.Api/Web.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Web.Api.dll"]
