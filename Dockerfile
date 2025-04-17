# Use the official .NET 8 SDK image to build the app
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy everything and restore dependencies
COPY . ./
RUN dotnet restore
RUN dotnet publish -c Release -o /out

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Copy published output from build stage
COPY --from=build /out ./

# Expose port (standard HTTP port for Render or local use)
EXPOSE 80

# Run the app
ENTRYPOINT ["dotnet", "PdfFieldExtractor.dll"]
