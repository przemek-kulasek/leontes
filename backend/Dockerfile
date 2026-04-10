FROM mcr.microsoft.com/dotnet/sdk:10.0
WORKDIR /app
COPY backend/ backend/
RUN dotnet restore backend/
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_LAUNCH_PROFILE=""
ENTRYPOINT ["dotnet", "watch", "run", "--project", "backend/src/Leontes.Api"]
