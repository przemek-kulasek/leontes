FROM mcr.microsoft.com/dotnet/sdk:10.0
WORKDIR /app
COPY backend/ backend/
RUN dotnet restore backend/
EXPOSE 8080
ENTRYPOINT ["dotnet", "watch", "run", "--project", "backend/src/Leontes.Api"]
